using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using Microsoft.Extensions.Logging;

namespace BarretApi.Core.Services;

public sealed class SocialPostService(
    IEnumerable<ISocialPlatformClient> platformClients,
    ITextShorteningService textShorteningService,
    ITextSplitterService textSplitterService,
    IImageDownloadService imageDownloadService,
    IImageResizer imageResizer,
    IHashtagService hashtagService,
    ILogger<SocialPostService> logger,
    IScheduledSocialPostRepository? scheduledSocialPostRepository = null,
    IScheduledPostImageStore? scheduledPostImageStore = null)
{
    private readonly IReadOnlyDictionary<string, ISocialPlatformClient> _clients =
        platformClients.ToDictionary(c => c.PlatformName, StringComparer.OrdinalIgnoreCase);
    private readonly ITextShorteningService _textShorteningService = textShorteningService;
    private readonly ITextSplitterService _textSplitterService = textSplitterService;
    private readonly IImageDownloadService _imageDownloadService = imageDownloadService;
    private readonly IImageResizer _imageResizer = imageResizer;
    private readonly IHashtagService _hashtagService = hashtagService;
    private readonly ILogger<SocialPostService> _logger = logger;
    private readonly IScheduledSocialPostRepository? _scheduledSocialPostRepository = scheduledSocialPostRepository;
    private readonly IScheduledPostImageStore? _scheduledPostImageStore = scheduledPostImageStore;

    public async Task<string> ScheduleAsync(
        SocialPost post,
        CancellationToken cancellationToken = default)
    {
        if (_scheduledSocialPostRepository is null)
        {
            throw new InvalidOperationException("Scheduled social post repository is not configured.");
        }

        if (!post.ScheduledForUtc.HasValue)
        {
            throw new InvalidOperationException("ScheduledForUtc is required when scheduling a post.");
        }

        var now = DateTimeOffset.UtcNow;
        var scheduledForUtc = post.ScheduledForUtc.Value.ToUniversalTime();
        if (scheduledForUtc <= now)
        {
            throw new InvalidOperationException("ScheduledForUtc must be in the future.");
        }

        var scheduledPostId = post.ScheduledPostId ?? Guid.NewGuid().ToString("N");

        var uploadedImages = new List<StoredImageData>();
        for (var i = 0; i < post.Images.Count; i++)
        {
            var image = post.Images[i];
            if (_scheduledPostImageStore is null)
            {
                throw new InvalidOperationException("Scheduled post image store is not configured but images were provided.");
            }

            var blobName = await _scheduledPostImageStore.UploadAsync(
                scheduledPostId, i, image.Content, image.ContentType, cancellationToken);
            uploadedImages.Add(new StoredImageData
            {
                BlobName = blobName,
                ContentType = image.ContentType,
                AltText = image.AltText,
                FileName = image.FileName
            });
        }

        var record = new ScheduledSocialPostRecord
        {
            ScheduledPostId = scheduledPostId,
            ScheduledForUtc = scheduledForUtc,
            Status = ScheduledPostStatus.Pending,
            Text = post.Text,
            Hashtags = post.Hashtags,
            TargetPlatforms = post.TargetPlatforms,
            ImageUrls = post.ImageUrls,
            UploadedImages = uploadedImages,
            CreatedAtUtc = now,
            LastAttemptedAtUtc = null,
            PublishedAtUtc = null,
            LastErrorCode = null,
            LastErrorMessage = null,
            AttemptCount = 0
        };

        await _scheduledSocialPostRepository.SaveScheduledAsync(record, cancellationToken);

        _logger.LogInformation(
            "Scheduled social post {ScheduledPostId} for {ScheduledForUtc}",
            scheduledPostId,
            scheduledForUtc);

        return scheduledPostId;
    }

    public async Task<IReadOnlyList<PlatformPostResult>> PreviewAsync(
        SocialPost post,
        CancellationToken cancellationToken = default)
    {
        var targetPlatforms = ResolveTargetPlatforms(post.TargetPlatforms);
        var hashtagResult = _hashtagService.ProcessHashtags(post.Text, post.Hashtags);
        var textWithHashtags = hashtagResult.FinalText;

        var tasks = targetPlatforms.Select(async client =>
        {
            var config = await client.GetConfigurationAsync(cancellationToken);
            var processedText = _textShorteningService.Shorten(textWithHashtags, config.MaxCharacters);
            return new PlatformPostResult
            {
                Platform = client.PlatformName,
                Success = true,
                PublishedText = processedText
            };
        });

        return await Task.WhenAll(tasks);
    }

    public async Task<IReadOnlyList<ThreadPostingResult>> PostThreadAsync(
        SocialThread thread,
        CancellationToken cancellationToken = default)
    {
        var correlationId = Guid.NewGuid().ToString("N");
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        });

        var targetPlatforms = ResolveTargetPlatforms(thread.TargetPlatforms);

        _logger.LogInformation(
            "Posting thread ({SegmentCount} segments) to {PlatformCount} platform(s): {Platforms}",
            thread.Segments.Count,
            targetPlatforms.Count,
            string.Join(", ", targetPlatforms.Select(c => c.PlatformName)));

        var tasks = targetPlatforms.Select(client =>
            PostThreadToPlatformAsync(client, thread, cancellationToken));

        return await Task.WhenAll(tasks);
    }

    private async Task<ThreadPostingResult> PostThreadToPlatformAsync(
        ISocialPlatformClient client,
        SocialThread thread,
        CancellationToken cancellationToken)
    {
        try
        {
            var config = await client.GetConfigurationAsync(cancellationToken);
            var segmentPosts = new List<ThreadSegmentPost>();

            foreach (var segment in thread.Segments)
            {
                var hashtagResult = _hashtagService.ProcessHashtags(segment.Text, segment.Hashtags);
                var textWithHashtags = hashtagResult.FinalText;
                var shortenedText = _textShorteningService.Shorten(textWithHashtags, config.MaxCharacters);

                var allImages = new List<ImageData>(segment.Images);
                foreach (var imageUrl in segment.ImageUrls)
                {
                    var imageData = await _imageDownloadService.DownloadAsync(
                        imageUrl.Url, imageUrl.AltText, cancellationToken);
                    allImages.Add(imageData);
                }

                var uploadedImages = new List<UploadedImage>();
                foreach (var imageData in allImages)
                {
                    var prepared = PrepareImageForPlatform(imageData, config);
                    var uploaded = await client.UploadImageAsync(prepared, cancellationToken);
                    uploadedImages.Add(uploaded);
                }

                segmentPosts.Add(new ThreadSegmentPost
                {
                    Text = shortenedText,
                    Images = uploadedImages
                });
            }

            var segmentResults = await client.PostThreadAsync(segmentPosts, cancellationToken);
            var allSucceeded = segmentResults.All(r => r.Success);

            _logger.LogInformation(
                "Thread posted to {Platform}: {SuccessCount}/{TotalCount} segment(s) succeeded",
                client.PlatformName,
                segmentResults.Count(r => r.Success),
                segmentResults.Count);

            return new ThreadPostingResult
            {
                Platform = client.PlatformName,
                Success = allSucceeded,
                SegmentResults = segmentResults
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error posting thread to {Platform}", client.PlatformName);
            return new ThreadPostingResult
            {
                Platform = client.PlatformName,
                Success = false,
                ErrorMessage = ex.Message,
                ErrorCode = "UNKNOWN_ERROR"
            };
        }
    }

    public async Task<IReadOnlyList<PlatformPostResult>> PostAsync(
        SocialPost post,
        CancellationToken cancellationToken = default)
    {
        var correlationId = Guid.NewGuid().ToString("N");
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        });

        var targetPlatforms = ResolveTargetPlatforms(post.TargetPlatforms);

        _logger.LogInformation(
            "Posting to {PlatformCount} platform(s): {Platforms}",
            targetPlatforms.Count,
            string.Join(", ", targetPlatforms.Select(c => c.PlatformName)));

        var hashtagResult = _hashtagService.ProcessHashtags(post.Text, post.Hashtags);
        var textWithHashtags = hashtagResult.FinalText;

        _logger.LogInformation(
            "Processed {HashtagCount} hashtags for post",
            hashtagResult.AllHashtags.Count);

        IReadOnlyList<ImageData> allImages;
        try
        {
            allImages = await ResolveImagesAsync(post, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download {ImageCount} image(s) from URLs", post.ImageUrls.Count);
            return targetPlatforms.Select(client => new PlatformPostResult
            {
                Platform = client.PlatformName,
                Success = false,
                ErrorMessage = $"Image download failed: {ex.Message}",
                ErrorCode = "IMAGE_DOWNLOAD_FAILED",
                Error = ex
            }).ToList();
        }

        _logger.LogInformation(
            "Resolved {ImageCount} image(s) for post",
            allImages.Count);

        var tasks = targetPlatforms.Select(client =>
            PostToPlatformAsync(client, textWithHashtags, allImages, post.AutoThread, cancellationToken));

        var results = await Task.WhenAll(tasks);

        var succeeded = results.Count(r => r.Success);
        _logger.LogInformation(
            "Post completed: {SuccessCount}/{TotalCount} platform(s) succeeded",
            succeeded,
            results.Length);

        return results;
    }

    private async Task<IReadOnlyList<ImageData>> ResolveImagesAsync(
        SocialPost post,
        CancellationToken cancellationToken)
    {
        var images = new List<ImageData>(post.Images);

        foreach (var imageUrl in post.ImageUrls)
        {
            _logger.LogInformation("Downloading image from URL: {Url}", imageUrl.Url);
            var imageData = await _imageDownloadService.DownloadAsync(
                imageUrl.Url,
                imageUrl.AltText,
                cancellationToken);
            images.Add(imageData);
        }

        return images;
    }

    private IReadOnlyList<ISocialPlatformClient> ResolveTargetPlatforms(
        IReadOnlyList<string> requestedPlatforms)
    {
        if (requestedPlatforms.Count == 0)
        {
            return _clients.Values.ToList();
        }

        var resolved = new List<ISocialPlatformClient>();

        foreach (var name in requestedPlatforms)
        {
            if (_clients.TryGetValue(name, out var client))
            {
                resolved.Add(client);
            }
            else
            {
                _logger.LogWarning("Requested platform '{Platform}' is not configured", name);
            }
        }

        return resolved;
    }

    private async Task<PlatformPostResult> PostToPlatformAsync(
        ISocialPlatformClient client,
        string originalText,
        IReadOnlyList<ImageData> images,
        bool autoThread,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Posting to {Platform}", client.PlatformName);

            var config = await client.GetConfigurationAsync(cancellationToken);

            // When auto-threading is requested and the text exceeds the platform limit,
            // split into segments and post as a thread instead of truncating.
            if (autoThread && new System.Globalization.StringInfo(originalText).LengthInTextElements > config.MaxCharacters)
            {
                return await PostAsAutoThreadAsync(client, originalText, images, config, cancellationToken);
            }

            var textToPost = _textShorteningService.Shorten(originalText, config.MaxCharacters);

            if (textToPost != originalText)
            {
                _logger.LogInformation(
                    "Text shortened for {Platform} from {OriginalLength} to {ShortenedLength} grapheme clusters (limit: {Limit})",
                    client.PlatformName,
                    originalText.Length,
                    textToPost.Length,
                    config.MaxCharacters);
            }

            var uploadedImages = new List<UploadedImage>();

            try
            {
                foreach (var imageData in images)
                {
                    var preparedImage = PrepareImageForPlatform(imageData, config);
                    _logger.LogDebug(
                        "Uploading image {FileName} ({ContentType}, {Size} bytes) to {Platform}",
                        preparedImage.FileName,
                        preparedImage.ContentType,
                        preparedImage.Content.Length,
                        client.PlatformName);
                    var uploaded = await client.UploadImageAsync(preparedImage, cancellationToken);
                    uploadedImages.Add(uploaded);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload image to {Platform}", client.PlatformName);
                return new PlatformPostResult
                {
                    Platform = client.PlatformName,
                    Success = false,
                    ErrorMessage = $"Image upload failed: {ex.Message}",
                    ErrorCode = "IMAGE_UPLOAD_FAILED",
                    Error = ex
                };
            }

            var result = await client.PostAsync(textToPost, uploadedImages, cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation(
                    "Successfully posted to {Platform}: {PostId}",
                    client.PlatformName,
                    result.PostId);
            }
            else
            {
                _logger.LogWarning(
                    "Failed to post to {Platform}: {ErrorCode} - {Error}",
                    client.PlatformName,
                    result.ErrorCode,
                    result.ErrorMessage);
            }

            return result with { PublishedText = textToPost };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error posting to {Platform}", client.PlatformName);

            return new PlatformPostResult
            {
                Platform = client.PlatformName,
                Success = false,
                ErrorMessage = ex.Message,
                ErrorCode = "UNKNOWN_ERROR",
                Error = ex
            };
        }
    }

    private async Task<PlatformPostResult> PostAsAutoThreadAsync(
        ISocialPlatformClient client,
        string text,
        IReadOnlyList<ImageData> images,
        PlatformConfiguration config,
        CancellationToken cancellationToken)
    {
        var segments = _textSplitterService.Split(text, config.MaxCharacters);

        _logger.LogInformation(
            "Auto-threading {SegmentCount} segments for {Platform} (limit: {Limit})",
            segments.Count,
            client.PlatformName,
            config.MaxCharacters);

        // Upload images (attached to the first segment only)
        var uploadedImages = new List<UploadedImage>();
        try
        {
            foreach (var imageData in images)
            {
                var prepared = PrepareImageForPlatform(imageData, config);
                var uploaded = await client.UploadImageAsync(prepared, cancellationToken);
                uploadedImages.Add(uploaded);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload image to {Platform} during auto-thread", client.PlatformName);
            return new PlatformPostResult
            {
                Platform = client.PlatformName,
                Success = false,
                ErrorMessage = $"Image upload failed: {ex.Message}",
                ErrorCode = "IMAGE_UPLOAD_FAILED",
                Error = ex
            };
        }

        var segmentPosts = segments.Select((t, i) => new ThreadSegmentPost
        {
            Text = t,
            Images = i == 0 ? uploadedImages : []
        }).ToList();

        var threadResults = await client.PostThreadAsync(segmentPosts, cancellationToken);
        var allSucceeded = threadResults.All(r => r.Success);
        var root = threadResults.FirstOrDefault();

        _logger.LogInformation(
            "Auto-thread posted to {Platform}: {SuccessCount}/{TotalCount} segment(s) succeeded",
            client.PlatformName,
            threadResults.Count(r => r.Success),
            threadResults.Count);

        return new PlatformPostResult
        {
            Platform = client.PlatformName,
            Success = allSucceeded,
            PostId = root?.PostId,
            PostUrl = root?.PostUrl,
            PublishedText = root?.PublishedText,
            ErrorMessage = allSucceeded ? null : threadResults.FirstOrDefault(r => !r.Success)?.ErrorMessage,
            ErrorCode = allSucceeded ? null : threadResults.FirstOrDefault(r => !r.Success)?.ErrorCode,
            ThreadResults = threadResults
        };
    }

    private ImageData PrepareImageForPlatform(ImageData image, PlatformConfiguration config)
    {
        var resizedBytes = _imageResizer.ResizeToFit(image.Content, config.MaxImageSizeBytes);

        if (ReferenceEquals(resizedBytes, image.Content))
        {
            return image;
        }

        _logger.LogInformation(
            "Image converted/resized from {OriginalSize} bytes ({OriginalType}) to {NewSize} bytes (image/jpeg)",
            image.Content.Length,
            image.ContentType,
            resizedBytes.Length);

        return new ImageData
        {
            Content = resizedBytes,
            ContentType = "image/jpeg",
            AltText = image.AltText,
            FileName = Path.ChangeExtension(image.FileName, ".jpg")
        };
    }
}

using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using Microsoft.Extensions.Logging;

namespace BarretApi.Core.Services;

public sealed class SocialPostService(
    IEnumerable<ISocialPlatformClient> platformClients,
    ITextShorteningService textShorteningService,
    IImageDownloadService imageDownloadService,
    IImageResizer imageResizer,
    IHashtagService hashtagService,
    ILogger<SocialPostService> logger)
{
    private readonly IReadOnlyDictionary<string, ISocialPlatformClient> _clients =
        platformClients.ToDictionary(c => c.PlatformName, StringComparer.OrdinalIgnoreCase);
    private readonly ITextShorteningService _textShorteningService = textShorteningService;
    private readonly IImageDownloadService _imageDownloadService = imageDownloadService;
    private readonly IImageResizer _imageResizer = imageResizer;
    private readonly IHashtagService _hashtagService = hashtagService;
    private readonly ILogger<SocialPostService> _logger = logger;

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
            PostToPlatformAsync(client, textWithHashtags, allImages, cancellationToken));

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
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Posting to {Platform}", client.PlatformName);

            var config = await client.GetConfigurationAsync(cancellationToken);
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

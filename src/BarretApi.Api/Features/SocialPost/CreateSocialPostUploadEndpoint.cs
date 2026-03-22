using BarretApi.Core.Models;
using BarretApi.Core.Services;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using SocialPostModel = BarretApi.Core.Models.SocialPost;

namespace BarretApi.Api.Features.SocialPost;

public sealed class CreateSocialPostUploadRequest
{
    public string? Text { get; set; }
    public DateTimeOffset? ScheduledFor { get; set; }
    public List<string>? Hashtags { get; set; }
    public List<string>? Platforms { get; set; }
    public List<IFormFile>? Images { get; set; }
    public List<string>? AltTexts { get; set; }
}

public sealed class CreateSocialPostUploadEndpoint(SocialPostService postService)
    : Endpoint<CreateSocialPostUploadRequest, CreateSocialPostResponse>
{
    private readonly SocialPostService _postService = postService;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/gif",
        "image/webp"
    };

    private const int MaxImageSizeBytes = 1_048_576; // 1 MB

    public override void Configure()
    {
        Post("/api/social-posts/upload");
        AllowFileUploads();

        Summary(s =>
        {
            s.Summary = "Create a social post (multipart upload)";
            s.Description = "Creates a cross-platform post using multipart/form-data. Images are uploaded as files and paired with alt text entries in order.";
            s.ExampleRequest = new CreateSocialPostUploadRequest
            {
                Text = "Hello from BarretApi! #dotnet #aspire",
                Hashtags = ["webapi"],
                Platforms = ["bluesky", "mastodon"],
                ScheduledFor = DateTimeOffset.Parse("2026-03-03T12:00:00Z"),
                AltTexts = ["A descriptive alt text for the uploaded image"]
            };
            s.ResponseExamples[200] = new CreateSocialPostResponse
            {
                Results =
                [
                    new PlatformResult
                    {
                        Platform = "bluesky",
                        Success = true,
                        PostId = "at://did:plc:abc123/app.bsky.feed.post/xyz789",
                        PostUrl = "https://bsky.app/profile/your-handle.bsky.social/post/xyz789",
                        ShortenedText = "Hello from BarretApi! #dotnet #aspire #webapi"
                    },
                    new PlatformResult
                    {
                        Platform = "mastodon",
                        Success = true,
                        PostId = "109876543210",
                        PostUrl = "https://mastodon.social/@you/109876543210",
                        ShortenedText = "Hello from BarretApi! #dotnet #aspire #webapi"
                    }
                ],
                PostedAt = DateTimeOffset.Parse("2026-03-01T12:00:00Z")
            };
            s.ResponseExamples[207] = new CreateSocialPostResponse
            {
                Results =
                [
                    new PlatformResult
                    {
                        Platform = "bluesky",
                        Success = true,
                        PostId = "at://did:plc:abc123/app.bsky.feed.post/xyz789",
                        PostUrl = "https://bsky.app/profile/your-handle.bsky.social/post/xyz789",
                        ShortenedText = "Hello from BarretApi! #dotnet #aspire #webapi"
                    },
                    new PlatformResult
                    {
                        Platform = "mastodon",
                        Success = false,
                        Error = "Image upload failed",
                        ErrorCode = "IMAGE_UPLOAD_FAILED"
                    }
                ],
                PostedAt = DateTimeOffset.Parse("2026-03-01T12:00:00Z")
            };
            s.ResponseExamples[502] = new CreateSocialPostResponse
            {
                Results =
                [
                    new PlatformResult
                    {
                        Platform = "bluesky",
                        Success = false,
                        Error = "Image download failed",
                        ErrorCode = "IMAGE_DOWNLOAD_FAILED"
                    },
                    new PlatformResult
                    {
                        Platform = "mastodon",
                        Success = false,
                        Error = "Service unavailable",
                        ErrorCode = "PLATFORM_ERROR"
                    }
                ],
                PostedAt = DateTimeOffset.Parse("2026-03-01T12:00:00Z")
            };
            s.Responses[200] = "All targeted platforms succeeded.";
            s.Responses[207] = "Partial success: at least one platform succeeded and at least one failed.";
            s.Responses[400] = "Request validation failed (including file count/type/size or alt text mismatch).";
            s.Responses[401] = "Missing or invalid X-Api-Key.";
            s.Responses[502] = "All targeted platforms failed.";
        });
    }

    public override async Task HandleAsync(CreateSocialPostUploadRequest req, CancellationToken ct)
    {
        if (req.ScheduledFor.HasValue && req.ScheduledFor.Value <= DateTimeOffset.UtcNow)
        {
            AddError("ScheduledFor must be in the future.");
            await Send.ErrorsAsync(cancellation: ct);
            return;
        }

        var images = await ConvertToImageDataAsync(req, ct);
        if (images is null)
        {
            return;
        }

        var socialPost = MapToSocialPost(req, images);
        if (socialPost.ScheduledForUtc.HasValue)
        {
            var scheduledPostId = await _postService.ScheduleAsync(socialPost, ct);
            var scheduledResponse = BuildScheduledResponse(
                socialPost.ScheduledForUtc.Value,
                scheduledPostId);
            await Send.ResponseAsync(scheduledResponse, 200, ct);
            return;
        }

        var results = await _postService.PostAsync(socialPost, ct);
        var response = BuildImmediateResponse(results);
        var statusCode = DetermineStatusCode(results);

        await Send.ResponseAsync(response, statusCode, ct);
    }

    private async Task<List<ImageData>?> ConvertToImageDataAsync(
        CreateSocialPostUploadRequest req,
        CancellationToken ct)
    {
        if (req.Images is null || req.Images.Count == 0)
        {
            return [];
        }

        if (req.Images.Count > 4)
        {
            AddError("Maximum of 4 images allowed.");
            await Send.ErrorsAsync(cancellation: ct);
            return null;
        }

        if (req.AltTexts is null || req.AltTexts.Count != req.Images.Count)
        {
            AddError("Alt text count must match image count. Provide one alt text per image.");
            await Send.ErrorsAsync(cancellation: ct);
            return null;
        }

        var images = new List<ImageData>();

        for (var i = 0; i < req.Images.Count; i++)
        {
            var file = req.Images[i];
            var altText = req.AltTexts[i];

            if (string.IsNullOrWhiteSpace(altText))
            {
                AddError($"Alt text for image {i + 1} must not be blank or whitespace.");
                await Send.ErrorsAsync(cancellation: ct);
                return null;
            }

            if (altText.Length > 1_500)
            {
                AddError($"Alt text for image {i + 1} must not exceed 1,500 characters.");
                await Send.ErrorsAsync(cancellation: ct);
                return null;
            }

            if (!AllowedContentTypes.Contains(file.ContentType))
            {
                AddError($"Image {i + 1} has unsupported content type '{file.ContentType}'. Allowed: JPEG, PNG, GIF, WebP.");
                await Send.ErrorsAsync(cancellation: ct);
                return null;
            }

            if (file.Length > MaxImageSizeBytes)
            {
                AddError($"Image {i + 1} exceeds maximum size of 1 MB.");
                await Send.ErrorsAsync(cancellation: ct);
                return null;
            }

            using var stream = new MemoryStream();
            await file.CopyToAsync(stream, ct);

            images.Add(new ImageData
            {
                Content = stream.ToArray(),
                ContentType = file.ContentType,
                AltText = altText,
                FileName = file.FileName
            });
        }

        return images;
    }

    private static SocialPostModel MapToSocialPost(
        CreateSocialPostUploadRequest req,
        List<ImageData> images)
    {
        return new SocialPostModel
        {
            Text = req.Text ?? string.Empty,
            ScheduledForUtc = req.ScheduledFor?.ToUniversalTime(),
            TargetPlatforms = req.Platforms ?? [],
            Hashtags = req.Hashtags ?? [],
            Images = images
        };
    }

    private static CreateSocialPostResponse BuildImmediateResponse(IReadOnlyList<PlatformPostResult> results)
    {
        return new CreateSocialPostResponse
        {
            Results = results.Select(r => new PlatformResult
            {
                Platform = r.Platform,
                Success = r.Success,
                PostId = r.PostId,
                PostUrl = r.PostUrl,
                ShortenedText = r.PublishedText,
                Error = r.Success ? null : r.ErrorMessage,
                ErrorCode = r.Success ? null : r.ErrorCode
            }).ToList(),
            PostedAt = DateTimeOffset.UtcNow,
            Scheduled = false
        };
    }

    private static CreateSocialPostResponse BuildScheduledResponse(
        DateTimeOffset scheduledFor,
        string scheduledPostId)
    {
        return new CreateSocialPostResponse
        {
            Results = [],
            PostedAt = null,
            Scheduled = true,
            ScheduledPostId = scheduledPostId,
            ScheduledFor = scheduledFor
        };
    }

    private static int DetermineStatusCode(IReadOnlyList<PlatformPostResult> results)
    {
        if (results.All(r => r.Success))
        {
            return 200;
        }

        if (results.Any(r => r.Success))
        {
            return 207;
        }

        return 502;
    }
}

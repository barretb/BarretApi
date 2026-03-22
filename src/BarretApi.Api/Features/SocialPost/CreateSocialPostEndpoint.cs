using BarretApi.Core.Services;
using FastEndpoints;
using SocialPostModel = BarretApi.Core.Models.SocialPost;

namespace BarretApi.Api.Features.SocialPost;

public sealed class CreateSocialPostEndpoint(SocialPostService postService)
    : Endpoint<CreateSocialPostRequest, CreateSocialPostResponse>
{
    public override void Configure()
    {
        Post("/api/social-posts");

        Summary(s =>
        {
            s.Summary = "Create a social post (JSON)";
            s.Description = "Creates a cross-platform post using JSON input. Images are supplied as URL references and downloaded server-side before publishing.";
            s.ExampleRequest = new CreateSocialPostRequest
            {
                Text = "Hello from BarretApi! #dotnet #aspire",
                Hashtags = ["webapi"],
                Platforms = ["linkedin", "bluesky", "mastodon"],
                ScheduledFor = DateTimeOffset.Parse("2026-03-03T12:00:00Z"),
                Images =
                [
                    new ImageAttachmentRequest
                    {
                        Url = "https://example.com/photo.jpg",
                        AltText = "A descriptive alt text for the image"
                    }
                ]
            };
            s.ResponseExamples[200] = new CreateSocialPostResponse
            {
                Results =
                [
                    new PlatformResult
                    {
                        Platform = "linkedin",
                        Success = true,
                        PostId = "urn:li:share:123456789",
                        PostUrl = "https://www.linkedin.com/feed/update/urn%3Ali%3Ashare%3A123456789",
                        ShortenedText = "Hello from BarretApi! #dotnet #aspire #webapi"
                    },
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
                PostedAt = DateTimeOffset.Parse("2026-03-01T12:00:00Z"),
                Scheduled = false
            };
            s.ResponseExamples[207] = new CreateSocialPostResponse
            {
                Results =
                [
                    new PlatformResult
                    {
                        Platform = "linkedin",
                        Success = false,
                        Error = "LinkedIn API rejected the content",
                        ErrorCode = "VALIDATION_FAILED"
                    },
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
                        Error = "Authentication failed",
                        ErrorCode = "AUTH_FAILED"
                    }
                ],
                PostedAt = DateTimeOffset.Parse("2026-03-01T12:00:00Z"),
                Scheduled = false
            };
            s.ResponseExamples[502] = new CreateSocialPostResponse
            {
                Results =
                [
                    new PlatformResult
                    {
                        Platform = "linkedin",
                        Success = false,
                        Error = "Authentication failed",
                        ErrorCode = "AUTH_FAILED"
                    },
                    new PlatformResult
                    {
                        Platform = "bluesky",
                        Success = false,
                        Error = "Rate limit exceeded",
                        ErrorCode = "RATE_LIMITED"
                    },
                    new PlatformResult
                    {
                        Platform = "mastodon",
                        Success = false,
                        Error = "Service unavailable",
                        ErrorCode = "PLATFORM_ERROR"
                    }
                ],
                PostedAt = DateTimeOffset.Parse("2026-03-01T12:00:00Z"),
                Scheduled = false
            };
            s.Responses[200] = "All targeted platforms succeeded.";
            s.Responses[207] = "Partial success: at least one platform succeeded and at least one failed.";
            s.Responses[400] = "Request validation failed.";
            s.Responses[401] = "Missing or invalid X-Api-Key.";
            s.Responses[502] = "All targeted platforms failed.";
        });
    }

    public override async Task HandleAsync(CreateSocialPostRequest req, CancellationToken ct)
    {
        var socialPost = MapToSocialPost(req);

        if (socialPost.ScheduledForUtc.HasValue)
        {
            var scheduledPostId = await postService.ScheduleAsync(socialPost, ct);
            var scheduledResponse = BuildScheduledResponse(
                socialPost.ScheduledForUtc.Value,
                scheduledPostId);
            await Send.ResponseAsync(scheduledResponse, 200, ct);
            return;
        }

        var results = await postService.PostAsync(socialPost, ct);
        var response = BuildImmediateResponse(results);
        var statusCode = DetermineStatusCode(results);

        await Send.ResponseAsync(response, statusCode, ct);
    }

    private static SocialPostModel MapToSocialPost(CreateSocialPostRequest req)
    {
        return new SocialPostModel
        {
            Text = req.Text ?? string.Empty,
            ScheduledForUtc = req.ScheduledFor?.ToUniversalTime(),
            TargetPlatforms = req.Platforms ?? [],
            Hashtags = req.Hashtags ?? [],
            ImageUrls = req.Images?.Select(i => new Core.Models.ImageUrl
            {
                Url = i.Url,
                AltText = i.AltText
            }).ToList() ?? []
        };
    }

    private static CreateSocialPostResponse BuildImmediateResponse(IReadOnlyList<Core.Models.PlatformPostResult> results)
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

    private static int DetermineStatusCode(IReadOnlyList<Core.Models.PlatformPostResult> results)
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

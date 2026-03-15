using BarretApi.Api.Features.SocialPost;
using BarretApi.Core.Models;
using BarretApi.Core.Services;
using FastEndpoints;
using Microsoft.Extensions.Logging;

namespace BarretApi.Api.Features.Nasa;

public sealed class NasaApodPostEndpoint(
    NasaApodPostService nasaApodPostService,
    ILogger<NasaApodPostEndpoint> logger)
    : Endpoint<NasaApodPostRequest, NasaApodPostResponse>
{
    public override void Configure()
    {
        Post("/api/social-posts/nasa-apod");

        Summary(s =>
        {
            s.Summary = "Post NASA APOD to social platforms";
            s.Description = "Fetches the NASA Astronomy Picture of the Day and posts it to selected social media platforms.";
            s.ExampleRequest = new NasaApodPostRequest
            {
                Date = "2026-03-08",
                Platforms = ["bluesky", "mastodon"]
            };
            s.Responses[200] = "All targeted platforms succeeded.";
            s.Responses[207] = "Partial success: at least one platform succeeded and at least one failed.";
            s.Responses[400] = "Request validation failed.";
            s.Responses[401] = "Missing or invalid X-Api-Key.";
            s.Responses[422] = "NASA API returned an error or the APOD could not be fetched.";
            s.Responses[502] = "All targeted platforms failed to post.";
        });
    }

    public override async Task HandleAsync(NasaApodPostRequest req, CancellationToken ct)
    {
        logger.LogInformation("NASA APOD post request received for date {Date}", req.Date ?? "today");

        var date = ParseDate(req.Date);
        var platforms = req.Platforms ?? [];

        ApodPostResult result;
        try
        {
            result = await nasaApodPostService.PostAsync(date, platforms, ct);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Failed to fetch APOD from NASA API");
            AddError($"Failed to fetch APOD from NASA API: {ex.Message}");
            await Send.ErrorsAsync(422, ct);
            return;
        }

        var response = BuildResponse(result);
        var statusCode = DetermineStatusCode(result.PlatformResults);
        logger.LogInformation("NASA APOD post completed with status {StatusCode}", statusCode);

        await Send.ResponseAsync(response, statusCode, ct);
    }

    private static DateOnly? ParseDate(string? dateString)
    {
        if (string.IsNullOrWhiteSpace(dateString))
        {
            return null;
        }

        return DateOnly.TryParseExact(dateString, "yyyy-MM-dd", out var parsed)
            ? parsed
            : null;
    }

    private static NasaApodPostResponse BuildResponse(ApodPostResult result)
    {
        var apod = result.ApodEntry;
        return new NasaApodPostResponse
        {
            Title = apod.Title,
            Date = apod.Date.ToString("yyyy-MM-dd"),
            MediaType = apod.MediaType == ApodMediaType.Image ? "image" : "video",
            ImageUrl = apod.Url,
            HdImageUrl = apod.HdUrl,
            Copyright = apod.Copyright,
            ImageAttached = result.ImageAttached,
            ImageResized = result.ImageResized,
            Results = result.PlatformResults.Select(r => new PlatformResult
            {
                Platform = r.Platform,
                Success = r.Success,
                PostId = r.PostId,
                PostUrl = r.PostUrl,
                ShortenedText = r.PublishedText,
                Error = r.Success ? null : r.ErrorMessage,
                ErrorCode = r.Success ? null : r.ErrorCode
            }).ToList(),
            PostedAt = DateTimeOffset.UtcNow
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

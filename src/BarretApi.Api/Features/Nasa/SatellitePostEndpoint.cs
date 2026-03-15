using BarretApi.Api.Features.SocialPost;
using BarretApi.Core.Models;
using BarretApi.Core.Services;
using FastEndpoints;
using Microsoft.Extensions.Logging;

namespace BarretApi.Api.Features.Nasa;

public sealed class SatellitePostEndpoint(
    NasaGibsPostService nasaGibsPostService,
    ILogger<SatellitePostEndpoint> logger)
    : Endpoint<SatellitePostRequest, SatellitePostResponse>
{
    public override void Configure()
    {
        Post("/api/social-posts/satellite");

        Summary(s =>
        {
            s.Summary = "Post satellite image to social platforms";
            s.Description = "Fetches a satellite image from NASA GIBS and posts it to selected social media platforms.";
            s.ExampleRequest = new SatellitePostRequest
            {
                Date = "2026-03-08",
                Layer = "MODIS_Terra_CorrectedReflectance_TrueColor",
                Platforms = ["bluesky", "mastodon"]
            };
            s.Responses[200] = "All targeted platforms succeeded.";
            s.Responses[207] = "Partial success: at least one platform succeeded and at least one failed.";
            s.Responses[400] = "Request validation failed.";
            s.Responses[401] = "Missing or invalid X-Api-Key.";
            s.Responses[422] = "NASA GIBS returned an error or the snapshot could not be fetched.";
            s.Responses[502] = "All targeted platforms failed to post.";
        });
    }

    public override async Task HandleAsync(SatellitePostRequest req, CancellationToken ct)
    {
        logger.LogInformation(
            "Satellite post request received for date {Date}, layer {Layer}",
            req.Date ?? "default",
            req.Layer ?? "default");

        var date = ParseDate(req.Date);
        var platforms = req.Platforms ?? [];

        SatellitePostResult result;
        try
        {
            result = await nasaGibsPostService.PostAsync(
                date,
                req.Layer,
                req.Title,
                req.Description,
                req.BboxSouth,
                req.BboxWest,
                req.BboxNorth,
                req.BboxEast,
                req.ImageWidth,
                req.ImageHeight,
                platforms,
                ct);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "Failed to fetch snapshot from NASA GIBS");
            AddError($"Failed to fetch snapshot from NASA GIBS: {ex.Message}");
            await Send.ErrorsAsync(422, ct);
            return;
        }

        var response = BuildResponse(result);
        var statusCode = DetermineStatusCode(result.PlatformResults);
        logger.LogInformation("Satellite post completed with status {StatusCode}", statusCode);

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

    private static SatellitePostResponse BuildResponse(SatellitePostResult result)
    {
        return new SatellitePostResponse
        {
            Date = result.Date.ToString("yyyy-MM-dd"),
            Layer = result.Layer,
            Title = result.Title,
            WorldviewUrl = result.WorldviewUrl,
            BboxSouth = result.BboxSouth,
            BboxWest = result.BboxWest,
            BboxNorth = result.BboxNorth,
            BboxEast = result.BboxEast,
            ImageWidth = result.ImageWidth,
            ImageHeight = result.ImageHeight,
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

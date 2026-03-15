using BarretApi.Core.Models;
using BarretApi.Core.Services;
using FastEndpoints;
using Microsoft.Extensions.Logging;

namespace BarretApi.Api.Features.SocialPost;

public sealed class RssRandomPostEndpoint(
    RssRandomPostService rssRandomPostService,
    ILogger<RssRandomPostEndpoint> logger)
    : Endpoint<RssRandomPostRequest, RssRandomPostResponse>
{
    public override void Configure()
    {
        Post("/api/social-posts/rss-random");

        Summary(s =>
        {
            s.Summary = "Post a random RSS feed entry";
            s.Description = "Fetches an RSS feed, applies optional filters (tags, recency, platforms), randomly selects one entry, and posts it to the targeted social platforms.";
            s.ExampleRequest = new RssRandomPostRequest
            {
                FeedUrl = "https://example.com/feed.xml",
                Platforms = ["bluesky", "mastodon"],
                ExcludeTags = ["personal"],
                MaxAgeDays = 30
            };
            s.Responses[200] = "All targeted platforms succeeded.";
            s.Responses[207] = "Partial success: at least one platform succeeded and at least one failed.";
            s.Responses[400] = "Request validation failed.";
            s.Responses[401] = "Missing or invalid X-Api-Key.";
            s.Responses[422] = "No eligible entries found after filtering.";
            s.Responses[502] = "All targeted platforms failed or feed could not be read.";
        });
    }

    public override async Task HandleAsync(RssRandomPostRequest req, CancellationToken ct)
    {
        logger.LogInformation("RSS random post request received for feed {FeedUrl}", req.FeedUrl);
        var query = MapToQuery(req);

        RssRandomPostResult result;
        try
        {
            result = await rssRandomPostService.SelectAndPostAsync(query, ct);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("no eligible"))
        {
            logger.LogWarning("No eligible entries for feed {FeedUrl}: {Error}", req.FeedUrl, ex.Message);
            AddError(ex.Message);
            await Send.ErrorsAsync(422, ct);
            return;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Failed to read RSS feed {FeedUrl}", req.FeedUrl);
            AddError($"Failed to read RSS feed: {ex.Message}");
            await Send.ErrorsAsync(502, ct);
            return;
        }

        var response = BuildResponse(result);
        var statusCode = DetermineStatusCode(result.PlatformResults);
        logger.LogInformation("RSS random post completed with status {StatusCode} for feed {FeedUrl}", statusCode, req.FeedUrl);

        await Send.ResponseAsync(response, statusCode, ct);
    }

    private static RssRandomPostQuery MapToQuery(RssRandomPostRequest req)
    {
        return new RssRandomPostQuery
        {
            FeedUrl = req.FeedUrl!,
            Platforms = req.Platforms ?? [],
            ExcludeTags = req.ExcludeTags ?? [],
            MaxAgeDays = req.MaxAgeDays,
            Header = req.Header
        };
    }

    private static RssRandomPostResponse BuildResponse(RssRandomPostResult result)
    {
        return new RssRandomPostResponse
        {
            SelectedTitle = result.SelectedEntry.Title,
            SelectedUrl = result.SelectedEntry.CanonicalUrl,
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

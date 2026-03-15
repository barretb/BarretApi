using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using Microsoft.Extensions.Logging;

namespace BarretApi.Core.Services;

public sealed class RssRandomPostService(
    IBlogFeedReader blogFeedReader,
    SocialPostService socialPostService,
    ILogger<RssRandomPostService> logger)
{
    private readonly IBlogFeedReader _blogFeedReader = blogFeedReader;
    private readonly SocialPostService _socialPostService = socialPostService;
    private readonly ILogger<RssRandomPostService> _logger = logger;

    public async Task<RssRandomPostResult> SelectAndPostAsync(
        RssRandomPostQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching RSS feed from {FeedUrl}", query.FeedUrl);
        var entries = await _blogFeedReader.ReadEntriesAsync(query.FeedUrl, cancellationToken);
        var eligible = entries.ToList();
        _logger.LogInformation("Feed returned {TotalEntries} entries", eligible.Count);

        if (query.ExcludeTags.Count > 0)
        {
            var beforeCount = eligible.Count;
            eligible = eligible
                .Where(e => !e.Tags.Any(t => query.ExcludeTags.Any(et => et.Equals(t, StringComparison.OrdinalIgnoreCase))))
                .ToList();
            _logger.LogInformation(
                "Tag exclusion filter removed {RemovedCount} entries, {RemainingCount} remaining",
                beforeCount - eligible.Count,
                eligible.Count);
        }

        if (query.MaxAgeDays.HasValue)
        {
            var beforeCount = eligible.Count;
            var cutoff = DateTimeOffset.UtcNow.AddDays(-query.MaxAgeDays.Value);
            eligible = eligible
                .Where(e => e.PublishedAtUtc >= cutoff)
                .ToList();
            _logger.LogInformation(
                "Recency filter (MaxAgeDays={MaxAgeDays}) removed {RemovedCount} entries, {RemainingCount} remaining",
                query.MaxAgeDays.Value,
                beforeCount - eligible.Count,
                eligible.Count);
        }

        if (eligible.Count == 0)
        {
            _logger.LogWarning("No eligible entries remain after filtering for feed {FeedUrl}", query.FeedUrl);
            throw new InvalidOperationException("Feed returned no eligible entries.");
        }

        var selectedIndex = Random.Shared.Next(eligible.Count);
        var selectedEntry = eligible[selectedIndex];
        _logger.LogInformation(
            "Selected entry {EntryTitle} ({EntryUrl}) from {EligibleCount} eligible entries",
            selectedEntry.Title,
            selectedEntry.CanonicalUrl,
            eligible.Count);

        var socialPost = BuildSocialPost(selectedEntry, query);
        var platformResults = await _socialPostService.PostAsync(socialPost, cancellationToken);

        var successCount = platformResults.Count(r => r.Success);
        var failCount = platformResults.Count(r => !r.Success);
        _logger.LogInformation(
            "Posting complete: {SuccessCount} succeeded, {FailCount} failed",
            successCount,
            failCount);

        return new RssRandomPostResult
        {
            SelectedEntry = selectedEntry,
            PlatformResults = platformResults
        };
    }

    private static SocialPost BuildSocialPost(BlogFeedEntry entry, RssRandomPostQuery query)
    {
        var hashtags = query.ExcludeTags.Count > 0
            ? entry.Tags
                .Where(t => !query.ExcludeTags.Any(et => et.Equals(t, StringComparison.OrdinalIgnoreCase)))
                .ToList()
            : entry.Tags;

        var header = string.IsNullOrWhiteSpace(query.Header) ? "" : $"{query.Header}\n";

        return new SocialPost
        {
            Text = $"From the archives...\n\n{header}{entry.Title}\n{entry.CanonicalUrl}",
            Hashtags = hashtags,
            ImageUrls = BuildImageUrls(entry),
            TargetPlatforms = query.Platforms
        };
    }

    private static IReadOnlyList<ImageUrl> BuildImageUrls(BlogFeedEntry entry)
    {
        if (!Uri.TryCreate(entry.HeroImageUrl, UriKind.Absolute, out var heroUri))
        {
            return [];
        }

        if (heroUri.Scheme is not ("http" or "https"))
        {
            return [];
        }

        return
        [
            new ImageUrl
            {
                Url = heroUri.ToString(),
                AltText = "blog post hero image"
            }
        ];
    }
}

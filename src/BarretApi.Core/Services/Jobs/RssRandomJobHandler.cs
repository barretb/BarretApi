using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BarretApi.Core.Services.Jobs;

/// <summary>
/// Posts a random entry from the blog feed. Wraps the same service the
/// POST /api/social-posts/rss-random endpoint calls.
/// </summary>
public sealed class RssRandomJobHandler(
    IRssRandomPostService rssRandomPostService,
    IOptions<BlogPromotionOptions> blogPromotionOptions,
    ILogger<RssRandomJobHandler> logger)
    : IScheduledJobHandler
{
    private readonly IRssRandomPostService _rssRandomPostService = rssRandomPostService;
    private readonly BlogPromotionOptions _blogPromotionOptions = blogPromotionOptions.Value;
    private readonly ILogger<RssRandomJobHandler> _logger = logger;

    public sealed record Arguments(
        string? FeedUrl,
        string[]? Platforms,
        string[]? ExcludeTags,
        int? MaxAgeDays,
        string? Header);

    public string JobType => "rss-random";

    public async Task<JobExecutionResult> ExecuteAsync(
        JobExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var arguments = JobArguments.Deserialize<Arguments>(context.ArgumentsJson);

        var feedUrl = !string.IsNullOrWhiteSpace(arguments?.FeedUrl)
            ? arguments.FeedUrl
            : _blogPromotionOptions.FeedUrl;

        if (string.IsNullOrWhiteSpace(feedUrl))
        {
            return JobExecutionResult.Fail(
                "No feedUrl was supplied in the job arguments and BlogPromotion:FeedUrl is not configured.");
        }

        var query = new RssRandomPostQuery
        {
            FeedUrl = feedUrl,
            Platforms = arguments?.Platforms ?? [],
            ExcludeTags = arguments?.ExcludeTags ?? [],
            MaxAgeDays = arguments?.MaxAgeDays,
            Header = arguments?.Header
        };

        var result = await _rssRandomPostService.SelectAndPostAsync(query, cancellationToken);

        _logger.LogInformation(
            "Random RSS post selected {Title} for job {JobName}.",
            result.SelectedEntry.Title,
            context.JobName);

        return PlatformResultSummary.ToResult(result.PlatformResults, result.SelectedEntry.Title);
    }
}

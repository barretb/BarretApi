using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using Microsoft.Extensions.Logging;

namespace BarretApi.Core.Services.Jobs;

/// <summary>
/// Promotes new blog entries from the configured feed. Wraps the same orchestrator
/// the POST /api/social-posts/rss-promotion endpoint calls.
/// </summary>
public sealed class RssPromotionJobHandler(
    IBlogPromotionOrchestrator orchestrator,
    ILogger<RssPromotionJobHandler> logger)
    : IScheduledJobHandler
{
    private readonly IBlogPromotionOrchestrator _orchestrator = orchestrator;
    private readonly ILogger<RssPromotionJobHandler> _logger = logger;

    public sealed record Arguments(string? FeedUrl, string? Header, int? RecentDaysWindow);

    public string JobType => "rss-promotion";

    public async Task<JobExecutionResult> ExecuteAsync(
        JobExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var arguments = JobArguments.Deserialize<Arguments>(context.ArgumentsJson);

        var summary = await _orchestrator.RunAsync(
            arguments?.FeedUrl,
            arguments?.Header,
            arguments?.RecentDaysWindow,
            cancellationToken);

        var text =
            $"{summary.EntriesEvaluated} evaluated, {summary.NewPostsSucceeded}/{summary.NewPostsAttempted} new, " +
            $"{summary.ReminderPostsSucceeded}/{summary.ReminderPostsAttempted} reminders.";

        if (summary.Failures.Count == 0)
        {
            _logger.LogInformation("Blog promotion run {RunId} completed: {Summary}", summary.RunId, text);
            return JobExecutionResult.Ok(text);
        }

        var detail = string.Join(
            "; ",
            summary.Failures.Select(f => $"{f.Platform}/{f.EntryIdentity}: {f.ErrorMessage}"));

        return JobExecutionResult.Fail($"{summary.Failures.Count} promotion failure(s) — {detail}", text);
    }
}

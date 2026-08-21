using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using Microsoft.Extensions.Logging;

namespace BarretApi.Core.Services.Jobs;

/// <summary>
/// Drains one-off scheduled social posts that have come due. Wraps the same processor
/// the POST /api/social-posts/scheduled/process endpoint calls.
/// </summary>
public sealed class ProcessScheduledPostsJobHandler(
    IScheduledSocialPostProcessor processor,
    ILogger<ProcessScheduledPostsJobHandler> logger)
    : IScheduledJobHandler
{
    private readonly IScheduledSocialPostProcessor _processor = processor;
    private readonly ILogger<ProcessScheduledPostsJobHandler> _logger = logger;

    public sealed record Arguments(int? MaxCount);

    public string JobType => "process-scheduled-posts";

    public async Task<JobExecutionResult> ExecuteAsync(
        JobExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var arguments = JobArguments.Deserialize<Arguments>(context.ArgumentsJson);
        var summary = await _processor.ProcessDueAsync(arguments?.MaxCount, cancellationToken);

        _logger.LogInformation(
            "Scheduled post drain {RunId}: {Succeeded} succeeded, {Failed} failed, {Skipped} skipped.",
            summary.RunId,
            summary.SucceededCount,
            summary.FailedCount,
            summary.SkippedCount);

        var text = $"{summary.SucceededCount} published, {summary.FailedCount} failed, {summary.SkippedCount} skipped.";

        return summary.FailedCount > 0
            ? JobExecutionResult.Fail($"{summary.FailedCount} scheduled post(s) failed to publish.", text)
            : JobExecutionResult.Ok(text);
    }
}

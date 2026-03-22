using BarretApi.Core.Interfaces;
using FastEndpoints;
using Microsoft.Extensions.Logging;

namespace BarretApi.Api.Features.SocialPost;

public sealed class ProcessScheduledPostsEndpoint(
    IScheduledSocialPostProcessor scheduledSocialPostProcessor,
    ILogger<ProcessScheduledPostsEndpoint> logger)
    : Endpoint<ProcessScheduledPostsRequest, ProcessScheduledPostsResponse>
{
    private readonly IScheduledSocialPostProcessor _scheduledSocialPostProcessor = scheduledSocialPostProcessor;
    private readonly ILogger<ProcessScheduledPostsEndpoint> _logger = logger;

    public override void Configure()
    {
        Post("/api/social-posts/scheduled/process");

        Summary(s =>
        {
            s.Summary = "Process due scheduled social posts";
            s.Description = "Publishes all scheduled social posts due at the time of processing and returns run summary metrics.";
            s.Responses[200] = "Scheduled processing completed. Some posts may have failed while others succeeded.";
            s.Responses[400] = "Request validation failed.";
            s.Responses[401] = "Missing or invalid X-Api-Key.";
            s.Responses[502] = "All due post attempts failed.";
        });
    }

    public override async Task HandleAsync(ProcessScheduledPostsRequest req, CancellationToken ct)
    {
        try
        {
            var summary = await _scheduledSocialPostProcessor.ProcessDueAsync(req.MaxCount, ct);
            var response = new ProcessScheduledPostsResponse
            {
                RunId = summary.RunId,
                StartedAtUtc = summary.StartedAtUtc,
                CompletedAtUtc = summary.CompletedAtUtc,
                DueCount = summary.DueCount,
                AttemptedCount = summary.AttemptedCount,
                SucceededCount = summary.SucceededCount,
                FailedCount = summary.FailedCount,
                SkippedCount = summary.SkippedCount,
                Failures = summary.Failures
                    .Select(f => new ProcessScheduledPostsFailure
                    {
                        ScheduledPostId = f.ScheduledPostId,
                        ScheduledForUtc = f.ScheduledForUtc,
                        Platforms = f.Platforms,
                        ErrorCode = f.ErrorCode,
                        ErrorMessage = f.ErrorMessage,
                        AttemptedAtUtc = f.AttemptedAtUtc
                    })
                    .ToList()
            };

            var statusCode = summary.AttemptedCount > 0 && summary.SucceededCount == 0
                ? 502
                : 200;

            await Send.ResponseAsync(response, statusCode, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled scheduled post processing failure");
            ThrowError("Scheduled post processing failed.");
            await Send.ErrorsAsync(cancellation: ct);
        }
    }
}

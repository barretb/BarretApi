using BarretApi.Core.Models;

namespace BarretApi.Api.Features.Jobs;

/// <summary>
/// Projects the stored job definition onto the API contract. The ETag and claim
/// timestamp are storage concerns and are deliberately not exposed.
/// </summary>
internal static class JobResponseMapper
{
    public static JobResponse ToResponse(ScheduledJobRecord job)
    {
        return new JobResponse
        {
            Name = job.Name,
            DisplayName = job.DisplayName,
            JobType = job.JobType,
            CronExpression = job.CronExpression,
            TimeZoneId = job.TimeZoneId,
            ArgumentsJson = job.ArgumentsJson,
            IsEnabled = job.IsEnabled,
            IsRunning = job.RunState == JobRunState.Running,
            NextRunUtc = job.NextRunUtc,
            LastRunUtc = job.LastRunUtc,
            LastRunStatus = job.LastRunStatus?.ToString(),
            LastRunError = job.LastRunError,
            LastRunDurationMs = job.LastRunDurationMs,
            ConsecutiveFailureCount = job.ConsecutiveFailureCount,
            MaxRetryCount = job.MaxRetryCount,
            RetryBaseDelaySeconds = job.RetryBaseDelaySeconds,
            CreatedAtUtc = job.CreatedAtUtc,
            UpdatedAtUtc = job.UpdatedAtUtc
        };
    }
}

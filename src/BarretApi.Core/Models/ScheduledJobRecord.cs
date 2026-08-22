namespace BarretApi.Core.Models;

/// <summary>
/// A recurring job definition. <see cref="Name"/> is the storage row key and is unique.
/// </summary>
public sealed class ScheduledJobRecord
{
    public required string Name { get; set; }
    public required string DisplayName { get; set; }
    public required string JobType { get; set; }
    public required string CronExpression { get; set; }
    public string TimeZoneId { get; set; } = "UTC";
    public string? ArgumentsJson { get; set; }
    public bool IsEnabled { get; set; }
    public DateTimeOffset? NextRunUtc { get; set; }
    public DateTimeOffset? LastRunUtc { get; set; }
    public JobRunStatus? LastRunStatus { get; set; }
    public string? LastRunError { get; set; }
    public long? LastRunDurationMs { get; set; }
    public int ConsecutiveFailureCount { get; set; }
    public int MaxRetryCount { get; set; } = 2;
    public int RetryBaseDelaySeconds { get; set; } = 30;
    public JobRunState RunState { get; set; } = JobRunState.Idle;
    public DateTimeOffset? ClaimedAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }

    /// <summary>
    /// Storage concurrency token. Empty for records that have not been persisted yet.
    /// </summary>
    public string ETag { get; set; } = string.Empty;
}

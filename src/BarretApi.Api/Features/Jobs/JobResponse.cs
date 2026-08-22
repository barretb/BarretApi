namespace BarretApi.Api.Features.Jobs;

public sealed class JobResponse
{
    public required string Name { get; init; }
    public required string DisplayName { get; init; }
    public required string JobType { get; init; }
    public required string CronExpression { get; init; }
    public required string TimeZoneId { get; init; }
    public string? ArgumentsJson { get; init; }
    public required bool IsEnabled { get; init; }
    public bool IsRunning { get; init; }
    public DateTimeOffset? NextRunUtc { get; init; }
    public DateTimeOffset? LastRunUtc { get; init; }
    public string? LastRunStatus { get; init; }
    public string? LastRunError { get; init; }
    public long? LastRunDurationMs { get; init; }
    public int ConsecutiveFailureCount { get; init; }
    public int MaxRetryCount { get; init; }
    public int RetryBaseDelaySeconds { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
}

public sealed class JobListResponse
{
    public required IReadOnlyList<JobResponse> Jobs { get; init; }
}

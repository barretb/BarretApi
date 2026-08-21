namespace BarretApi.Core.Models;

/// <summary>
/// One execution of a job, successful or not. Retries within an execution share a single record.
/// </summary>
public sealed class JobRunRecord
{
    public required string RunId { get; set; }
    public required string JobName { get; set; }
    public required string JobType { get; set; }
    public required JobTriggerType TriggerType { get; set; }
    public DateTimeOffset? ScheduledForUtc { get; set; }
    public required DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public long DurationMs { get; set; }
    public required JobRunStatus Status { get; set; }
    public int AttemptCount { get; set; }
    public string? Summary { get; set; }
    public string? ErrorMessage { get; set; }
}

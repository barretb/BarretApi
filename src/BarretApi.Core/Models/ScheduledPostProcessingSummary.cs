namespace BarretApi.Core.Models;

public sealed class ScheduledPostProcessingSummary
{
    public required string RunId { get; init; }
    public required DateTimeOffset StartedAtUtc { get; init; }
    public required DateTimeOffset CompletedAtUtc { get; init; }
    public int DueCount { get; init; }
    public int AttemptedCount { get; init; }
    public int SucceededCount { get; init; }
    public int FailedCount { get; init; }
    public int SkippedCount { get; init; }
    public List<ScheduledPostFailureDetails> Failures { get; init; } = [];
}

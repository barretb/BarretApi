namespace BarretApi.Core.Models;

/// <summary>
/// Everything a handler is told about the run it is performing.
/// <paramref name="ScheduledForUtc"/> is null for manual runs.
/// </summary>
public sealed record JobExecutionContext(
    string JobName,
    string JobType,
    string RunId,
    DateTimeOffset? ScheduledForUtc,
    string? ArgumentsJson);

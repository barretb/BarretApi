using BarretApi.Core.Models;

namespace BarretApi.Api.Features.Jobs;

public sealed class JobRunResponse
{
    public required string RunId { get; init; }
    public required string JobName { get; init; }
    public required string JobType { get; init; }
    public required string TriggerType { get; init; }
    public DateTimeOffset? ScheduledForUtc { get; init; }
    public required DateTimeOffset StartedAtUtc { get; init; }
    public DateTimeOffset? CompletedAtUtc { get; init; }
    public long DurationMs { get; init; }
    public required string Status { get; init; }
    public int AttemptCount { get; init; }
    public string? Summary { get; init; }
    public string? ErrorMessage { get; init; }

    public static JobRunResponse FromRecord(JobRunRecord run)
    {
        return new JobRunResponse
        {
            RunId = run.RunId,
            JobName = run.JobName,
            JobType = run.JobType,
            TriggerType = run.TriggerType.ToString(),
            ScheduledForUtc = run.ScheduledForUtc,
            StartedAtUtc = run.StartedAtUtc,
            CompletedAtUtc = run.CompletedAtUtc,
            DurationMs = run.DurationMs,
            Status = run.Status.ToString(),
            AttemptCount = run.AttemptCount,
            Summary = run.Summary,
            ErrorMessage = run.ErrorMessage
        };
    }
}

public sealed class JobRunListResponse
{
    public required IReadOnlyList<JobRunResponse> Runs { get; init; }
}

public sealed class JobTypesResponse
{
    public required IReadOnlyList<string> JobTypes { get; init; }
}

namespace BarretApi.Core.Models;

public sealed class ScheduledPostFailureDetails
{
    public required string ScheduledPostId { get; init; }
    public required DateTimeOffset ScheduledForUtc { get; init; }
    public required IReadOnlyList<string> Platforms { get; init; }
    public required string ErrorCode { get; init; }
    public required string ErrorMessage { get; init; }
    public required DateTimeOffset AttemptedAtUtc { get; init; }
}

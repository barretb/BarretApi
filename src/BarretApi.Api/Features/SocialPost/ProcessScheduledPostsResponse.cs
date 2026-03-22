namespace BarretApi.Api.Features.SocialPost;

public sealed class ProcessScheduledPostsResponse
{
    public required string RunId { get; init; }
    public required DateTimeOffset StartedAtUtc { get; init; }
    public required DateTimeOffset CompletedAtUtc { get; init; }
    public int DueCount { get; init; }
    public int AttemptedCount { get; init; }
    public int SucceededCount { get; init; }
    public int FailedCount { get; init; }
    public int SkippedCount { get; init; }
    public List<ProcessScheduledPostsFailure> Failures { get; init; } = [];
}

public sealed class ProcessScheduledPostsFailure
{
    public required string ScheduledPostId { get; init; }
    public required DateTimeOffset ScheduledForUtc { get; init; }
    public required IReadOnlyList<string> Platforms { get; init; }
    public required string ErrorCode { get; init; }
    public required string ErrorMessage { get; init; }
    public required DateTimeOffset AttemptedAtUtc { get; init; }
}

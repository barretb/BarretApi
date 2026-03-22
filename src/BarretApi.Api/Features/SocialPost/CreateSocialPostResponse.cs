namespace BarretApi.Api.Features.SocialPost;

public sealed class CreateSocialPostResponse
{
    public required List<PlatformResult> Results { get; init; }
    public DateTimeOffset? PostedAt { get; init; }
    public bool Scheduled { get; init; }
    public string? ScheduledPostId { get; init; }
    public DateTimeOffset? ScheduledFor { get; init; }
}

public sealed class PlatformResult
{
    public required string Platform { get; init; }
    public required bool Success { get; init; }
    public string? PostId { get; init; }
    public string? PostUrl { get; init; }
    public string? ShortenedText { get; init; }
    public string? Error { get; init; }
    public string? ErrorCode { get; init; }
}

namespace BarretApi.Core.Models;

/// <summary>
/// Durable representation of a post scheduled for future publishing.
/// </summary>
public sealed class ScheduledSocialPostRecord
{
    public required string ScheduledPostId { get; init; }
    public required DateTimeOffset ScheduledForUtc { get; init; }
    public required ScheduledPostStatus Status { get; set; }
    public required string Text { get; init; }
    public IReadOnlyList<string> Hashtags { get; init; } = [];
    public IReadOnlyList<string> TargetPlatforms { get; init; } = [];
    public IReadOnlyList<ImageUrl> ImageUrls { get; init; } = [];
    public IReadOnlyList<StoredImageData> UploadedImages { get; init; } = [];
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset? LastAttemptedAtUtc { get; set; }
    public DateTimeOffset? PublishedAtUtc { get; set; }
    public string? LastErrorCode { get; set; }
    public string? LastErrorMessage { get; set; }
    public int AttemptCount { get; set; }
}

public sealed class StoredImageData
{
    public required string ContentBase64 { get; init; }
    public required string ContentType { get; init; }
    public required string AltText { get; init; }
    public string? FileName { get; init; }
}

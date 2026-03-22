namespace BarretApi.Core.Models;

public sealed class SocialPost
{
    public required string Text { get; init; }
    public DateTimeOffset? ScheduledForUtc { get; init; }
    public string? ScheduledPostId { get; init; }
    public IReadOnlyList<string> Hashtags { get; init; } = [];
    public IReadOnlyList<ImageData> Images { get; init; } = [];
    public IReadOnlyList<ImageUrl> ImageUrls { get; init; } = [];
    public IReadOnlyList<string> TargetPlatforms { get; init; } = [];
}

/// <summary>
/// Represents an image to download from a URL before uploading to platforms.
/// </summary>
public sealed class ImageUrl
{
    public required string Url { get; init; }
    public required string AltText { get; init; }
}

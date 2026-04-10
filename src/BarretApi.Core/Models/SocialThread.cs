namespace BarretApi.Core.Models;

public sealed class SocialThread
{
    public IReadOnlyList<ThreadPost> Segments { get; init; } = [];
    public IReadOnlyList<string> TargetPlatforms { get; init; } = [];
}

public sealed class ThreadPost
{
    public required string Text { get; init; }
    public IReadOnlyList<string> Hashtags { get; init; } = [];
    public IReadOnlyList<ImageData> Images { get; init; } = [];
    public IReadOnlyList<ImageUrl> ImageUrls { get; init; } = [];
}

/// <summary>
/// A single thread segment with pre-processed text and pre-uploaded images,
/// ready to be sent to a platform client.
/// </summary>
public sealed class ThreadSegmentPost
{
    public required string Text { get; init; }
    public IReadOnlyList<UploadedImage> Images { get; init; } = [];
}

public sealed class ThreadPostingResult
{
    public required string Platform { get; init; }
    public required bool Success { get; init; }
    public IReadOnlyList<PlatformPostResult> SegmentResults { get; init; } = [];
    public string? ErrorMessage { get; init; }
    public string? ErrorCode { get; init; }
}

namespace BarretApi.Core.Models;

public sealed record PlatformPostResult
{
    public required string Platform { get; init; }
    public required bool Success { get; init; }
    public string? PostId { get; init; }
    public string? PostUrl { get; init; }
    public string? PublishedText { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ErrorCode { get; init; }
    public Exception? Error { get; init; }
}

namespace BarretApi.Core.Models;

/// <summary>
/// Represents a single NASA Astronomy Picture of the Day as returned by the API.
/// </summary>
public sealed class ApodEntry
{
    public required string Title { get; init; }
    public required DateOnly Date { get; init; }
    public required string Explanation { get; init; }
    public required string Url { get; init; }
    public string? HdUrl { get; init; }
    public required ApodMediaType MediaType { get; init; }
    public string? Copyright { get; init; }
    public string? ThumbnailUrl { get; init; }
}

namespace BarretApi.Core.Models;

public sealed class BlogFeedEntry
{
    public required string EntryIdentity { get; init; }
    public string? Guid { get; init; }
    public required string CanonicalUrl { get; init; }
    public required string Title { get; init; }
    public required DateTimeOffset PublishedAtUtc { get; init; }
    public string? Summary { get; init; }
    public string? HeroImageUrl { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
}

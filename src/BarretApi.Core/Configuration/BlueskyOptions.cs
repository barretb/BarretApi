namespace BarretApi.Core.Configuration;

public sealed class BlueskyOptions
{
    public const string SectionName = "Bluesky";

    public required string Handle { get; init; }
    public required string AppPassword { get; init; }
    public string ServiceUrl { get; init; } = "https://bsky.social";
}

namespace BarretApi.Core.Models;

/// <summary>
/// Query parameters for selecting and posting a random RSS feed entry.
/// </summary>
public sealed class RssRandomPostQuery
{
	public required string FeedUrl { get; init; }
	public IReadOnlyList<string> Platforms { get; init; } = [];
	public IReadOnlyList<string> ExcludeTags { get; init; } = [];
	public int? MaxAgeDays { get; init; }
}

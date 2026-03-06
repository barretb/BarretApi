namespace BarretApi.Api.Features.SocialPost;

public sealed class RssRandomPostRequest
{
	public string? FeedUrl { get; init; }
	public List<string>? Platforms { get; init; }
	public List<string>? ExcludeTags { get; init; }
	public int? MaxAgeDays { get; init; }
}

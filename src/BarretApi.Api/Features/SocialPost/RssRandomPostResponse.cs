namespace BarretApi.Api.Features.SocialPost;

public sealed class RssRandomPostResponse
{
	public required string SelectedTitle { get; init; }
	public required string SelectedUrl { get; init; }
	public required List<PlatformResult> Results { get; init; }
	public required DateTimeOffset PostedAt { get; init; }
}

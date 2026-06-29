namespace BarretApi.Api.Features.SocialPost;

public sealed class PostTipOfDayResponse
{
	public required string TipId { get; init; }
	public required string Category { get; init; }
	public required string Tip { get; init; }
	public string? MoreInfoUrl { get; init; }
	public DateTimeOffset? PreviousLastPostedDate { get; init; }
	public required bool TipMarkedPosted { get; init; }
	public required List<PlatformResult> Results { get; init; }
	public required DateTimeOffset PostedAt { get; init; }
}

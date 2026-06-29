namespace BarretApi.Api.Features.SocialPost;

public sealed class AddTipOfDayResponse
{
	public required List<AddedTipOfDayItem> Tips { get; init; }
}

public sealed class AddedTipOfDayItem
{
	public required string TipId { get; init; }
	public required string Category { get; init; }
	public required string Tip { get; init; }
	public string? MoreInfoUrl { get; init; }
	public DateTimeOffset? LastPostedDate { get; init; }
	public required DateTimeOffset CreatedAtUtc { get; init; }
}

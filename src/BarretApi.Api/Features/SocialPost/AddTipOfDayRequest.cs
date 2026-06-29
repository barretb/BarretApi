namespace BarretApi.Api.Features.SocialPost;

public sealed class AddTipOfDayRequest
{
	public string? Category { get; init; }
	public List<AddTipOfDayItem>? Tips { get; init; }
}

public sealed class AddTipOfDayItem
{
	public string? Tip { get; init; }
	public string? MoreInfoUrl { get; init; }
}

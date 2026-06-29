namespace BarretApi.Api.Features.SocialPost;

public sealed class AddTipOfDayRequest
{
	public string? Category { get; init; }
	public string? Tip { get; init; }
	public string? MoreInfoUrl { get; init; }
}

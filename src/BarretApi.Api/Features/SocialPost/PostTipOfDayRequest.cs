namespace BarretApi.Api.Features.SocialPost;

public sealed class PostTipOfDayRequest
{
	public string? Category { get; init; }
	public List<string>? Platforms { get; init; }
	public string? Leader { get; init; }
}

namespace BarretApi.Api.Features.Avatar;

public sealed class AvatarPostRequest
{
	public string? Style { get; init; }

	public string? Seed { get; init; }

	public string? Text { get; init; }

	public string? AltText { get; init; }

	public List<string>? Hashtags { get; init; }

	public List<string>? Platforms { get; init; }
}

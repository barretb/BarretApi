namespace BarretApi.Api.Features.Signboard;

public sealed class GenerateSignboardRequest
{
	public string? Text { get; init; }
	public int? Width { get; init; }
	public int? Height { get; init; }
	public int? Seed { get; init; }
	public List<string>? Platforms { get; init; }
	public string? Caption { get; init; }
	public List<string>? Hashtags { get; init; }
	public string? AltText { get; init; }
}

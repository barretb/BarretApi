namespace BarretApi.Api.Features.Nasa;

public sealed class OhioSatellitePostRequest
{
	public string? Date { get; init; }
	public string? Layer { get; init; }
	public List<string>? Platforms { get; init; }
}

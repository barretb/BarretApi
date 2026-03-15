namespace BarretApi.Api.Features.Nasa;

public sealed class SatellitePostRequest
{
    public string? Date { get; init; }
    public string? Layer { get; init; }
    public List<string>? Platforms { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
    public double? BboxSouth { get; init; }
    public double? BboxWest { get; init; }
    public double? BboxNorth { get; init; }
    public double? BboxEast { get; init; }
    public int? ImageWidth { get; init; }
    public int? ImageHeight { get; init; }
}

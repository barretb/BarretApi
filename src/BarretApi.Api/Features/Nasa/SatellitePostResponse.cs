using BarretApi.Api.Features.SocialPost;

namespace BarretApi.Api.Features.Nasa;

public sealed class SatellitePostResponse
{
    public required string Date { get; init; }
    public required string Layer { get; init; }
    public required string Title { get; init; }
    public required string WorldviewUrl { get; init; }
    public required double BboxSouth { get; init; }
    public required double BboxWest { get; init; }
    public required double BboxNorth { get; init; }
    public required double BboxEast { get; init; }
    public required int ImageWidth { get; init; }
    public required int ImageHeight { get; init; }
    public required bool ImageAttached { get; init; }
    public required bool ImageResized { get; init; }
    public required List<PlatformResult> Results { get; init; }
    public required DateTimeOffset PostedAt { get; init; }
}

using BarretApi.Api.Features.SocialPost;

namespace BarretApi.Api.Features.Nasa;

public sealed class OhioSatellitePostResponse
{
	public required string Date { get; init; }
	public required string Layer { get; init; }
	public required string WorldviewUrl { get; init; }
	public required int ImageWidth { get; init; }
	public required int ImageHeight { get; init; }
	public required bool ImageAttached { get; init; }
	public required bool ImageResized { get; init; }
	public required List<PlatformResult> Results { get; init; }
	public required DateTimeOffset PostedAt { get; init; }
}

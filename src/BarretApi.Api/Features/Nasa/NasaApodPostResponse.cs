using BarretApi.Api.Features.SocialPost;

namespace BarretApi.Api.Features.Nasa;

public sealed class NasaApodPostResponse
{
	public required string Title { get; init; }
	public required string Date { get; init; }
	public required string MediaType { get; init; }
	public required string ImageUrl { get; init; }
	public string? HdImageUrl { get; init; }
	public string? Copyright { get; init; }
	public required bool ImageAttached { get; init; }
	public required bool ImageResized { get; init; }
	public required List<PlatformResult> Results { get; init; }
	public required DateTimeOffset PostedAt { get; init; }
}

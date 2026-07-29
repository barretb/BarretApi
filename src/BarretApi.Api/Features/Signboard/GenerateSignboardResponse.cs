using BarretApi.Api.Features.SocialPost;

namespace BarretApi.Api.Features.Signboard;

public sealed class GenerateSignboardResponse
{
	public int Width { get; init; }
	public int Height { get; init; }
	public int Seed { get; init; }
	public List<PlatformResult> Results { get; init; } = [];
	public DateTimeOffset PostedAt { get; init; }
}

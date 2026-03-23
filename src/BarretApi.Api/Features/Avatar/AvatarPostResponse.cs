using BarretApi.Api.Features.SocialPost;

namespace BarretApi.Api.Features.Avatar;

public sealed class AvatarPostResponse
{
	public required string Style { get; init; }

	public required string Seed { get; init; }

	public required string Format { get; init; }

	public required bool ImageAttached { get; init; }

	public required List<PlatformResult> Results { get; init; }

	public required DateTimeOffset PostedAt { get; init; }
}

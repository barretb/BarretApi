namespace BarretApi.Core.Models;

public sealed class AvatarPostResult
{
	public required string Style { get; init; }

	public required string Seed { get; init; }

	public required string Format { get; init; }

	public required bool ImageAttached { get; init; }

	public required IReadOnlyList<PlatformPostResult> PlatformResults { get; init; }
}

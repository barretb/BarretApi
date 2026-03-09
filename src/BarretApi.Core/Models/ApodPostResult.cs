namespace BarretApi.Core.Models;

/// <summary>
/// Result of posting a NASA APOD to social platforms.
/// </summary>
public sealed class ApodPostResult
{
	public required ApodEntry ApodEntry { get; init; }
	public required IReadOnlyList<PlatformPostResult> PlatformResults { get; init; }
	public required bool ImageAttached { get; init; }
	public required bool ImageResized { get; init; }
}

namespace BarretApi.Core.Models;

/// <summary>
/// Result of posting an Ohio satellite image to social platforms.
/// </summary>
public sealed record OhioSatellitePostResult(
	DateOnly Date,
	string Layer,
	string WorldviewUrl,
	int ImageWidth,
	int ImageHeight,
	bool ImageAttached,
	bool ImageResized,
	IReadOnlyList<PlatformPostResult> PlatformResults);

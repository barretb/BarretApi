namespace BarretApi.Core.Models;

/// <summary>
/// Result of posting a satellite image to social platforms.
/// </summary>
public sealed record SatellitePostResult(
	DateOnly Date,
	string Layer,
	string Title,
	string WorldviewUrl,
	double BboxSouth,
	double BboxWest,
	double BboxNorth,
	double BboxEast,
	int ImageWidth,
	int ImageHeight,
	bool ImageAttached,
	bool ImageResized,
	IReadOnlyList<PlatformPostResult> PlatformResults);

namespace BarretApi.Core.Models;

/// <summary>
/// Parameters for requesting a snapshot from the NASA GIBS Worldview Snapshot API.
/// </summary>
public sealed record GibsSnapshotRequest(
	string Layer,
	DateOnly Date,
	double BboxSouth,
	double BboxWest,
	double BboxNorth,
	double BboxEast,
	int ImageWidth,
	int ImageHeight);

namespace BarretApi.Core.Models;

/// <summary>
/// Represents a snapshot image retrieved from the NASA GIBS Worldview Snapshot API.
/// </summary>
public sealed record GibsSnapshotEntry(
	byte[] ImageBytes,
	DateOnly Date,
	string Layer,
	int Width,
	int Height,
	string ContentType);

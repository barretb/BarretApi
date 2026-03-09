using BarretApi.Core.Models;

namespace BarretApi.Core.Interfaces;

/// <summary>
/// Abstracts communication with the NASA GIBS Worldview Snapshot API.
/// </summary>
public interface INasaGibsClient
{
	/// <summary>
	/// Fetches a snapshot image of Ohio for the given layer and date.
	/// </summary>
	Task<GibsSnapshotEntry> GetSnapshotAsync(string layer, DateOnly date, CancellationToken cancellationToken = default);
}

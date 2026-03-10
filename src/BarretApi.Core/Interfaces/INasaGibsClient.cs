using BarretApi.Core.Models;

namespace BarretApi.Core.Interfaces;

/// <summary>
/// Abstracts communication with the NASA GIBS Worldview Snapshot API.
/// </summary>
public interface INasaGibsClient
{
    /// <summary>
    /// Fetches a snapshot image for the given request parameters.
    /// </summary>
    Task<GibsSnapshotEntry> GetSnapshotAsync(GibsSnapshotRequest request, CancellationToken cancellationToken = default);
}

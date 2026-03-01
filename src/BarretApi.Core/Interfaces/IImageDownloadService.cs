using BarretApi.Core.Models;

namespace BarretApi.Core.Interfaces;

/// <summary>
/// Service for downloading images from URLs and converting them to <see cref="ImageData"/>.
/// </summary>
public interface IImageDownloadService
{
    /// <summary>
    /// Downloads an image from the specified URL with Content-Type validation and size limits.
    /// </summary>
    /// <param name="imageUrl">The URL to download the image from.</param>
    /// <param name="altText">The alt text to associate with the downloaded image.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The downloaded image as <see cref="ImageData"/>.</returns>
    Task<ImageData> DownloadAsync(
        string imageUrl,
        string altText,
        CancellationToken cancellationToken = default);
}

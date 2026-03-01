using BarretApi.Core.Models;

namespace BarretApi.Core.Interfaces;

public interface ISocialPlatformClient
{
    string PlatformName { get; }

    Task<PlatformPostResult> PostAsync(
        string text,
        IReadOnlyList<UploadedImage> images,
        CancellationToken cancellationToken = default);

    Task<UploadedImage> UploadImageAsync(
        ImageData image,
        CancellationToken cancellationToken = default);

    Task<PlatformConfiguration> GetConfigurationAsync(
        CancellationToken cancellationToken = default);
}

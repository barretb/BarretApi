using BarretApi.Core.Models;

namespace BarretApi.Core.Interfaces;

public interface INasaGibsPostService
{
    Task<SatellitePostResult> PostAsync(
        DateOnly? date,
        string? layer,
        string? title,
        string? description,
        double? bboxSouth,
        double? bboxWest,
        double? bboxNorth,
        double? bboxEast,
        int? imageWidth,
        int? imageHeight,
        IReadOnlyList<string> platforms,
        CancellationToken cancellationToken = default);
}

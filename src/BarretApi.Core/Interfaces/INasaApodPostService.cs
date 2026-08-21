using BarretApi.Core.Models;

namespace BarretApi.Core.Interfaces;

public interface INasaApodPostService
{
    Task<ApodPostResult> PostAsync(
        DateOnly? date,
        IReadOnlyList<string> platforms,
        CancellationToken cancellationToken = default);
}

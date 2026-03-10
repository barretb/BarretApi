using BarretApi.Core.Models;

namespace BarretApi.Core.Interfaces;

/// <summary>
/// Abstracts communication with the NASA APOD API.
/// </summary>
public interface INasaApodClient
{
    Task<ApodEntry> GetApodAsync(DateOnly? date, CancellationToken cancellationToken = default);
}

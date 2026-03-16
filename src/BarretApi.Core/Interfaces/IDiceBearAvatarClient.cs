using BarretApi.Core.Models;

namespace BarretApi.Core.Interfaces;

public interface IDiceBearAvatarClient
{
    Task<AvatarResult> GetAvatarAsync(
        string? style = null,
        string? format = null,
        string? seed = null,
        CancellationToken cancellationToken = default);
}

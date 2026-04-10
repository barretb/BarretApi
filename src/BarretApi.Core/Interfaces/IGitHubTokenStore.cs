using BarretApi.Core.Models;

namespace BarretApi.Core.Interfaces;

public interface IGitHubTokenStore
{
    Task<GitHubTokenRecord?> GetTokenAsync(CancellationToken cancellationToken = default);
    Task SaveTokenAsync(GitHubTokenRecord token, CancellationToken cancellationToken = default);
}

using BarretApi.Core.Models;

namespace BarretApi.Core.Interfaces;

public interface IGitHubRepositoryStore
{
    Task<IReadOnlyList<GitHubRepositoryRecord>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<GitHubRepositoryRecord?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task ReplaceAllAsync(string username, IReadOnlyList<GitHubRepositoryRecord> repositories, CancellationToken cancellationToken = default);
}

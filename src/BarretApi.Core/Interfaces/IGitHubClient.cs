using BarretApi.Core.Models;

namespace BarretApi.Core.Interfaces;

public interface IGitHubClient
{
    Task<GitHubTokenRecord> ExchangeCodeForTokenAsync(string code, string redirectUri, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GitHubRepositoryRecord>> GetRepositoriesAsync(CancellationToken cancellationToken = default);
    Task<GitHubIssueResult> CreateIssueAsync(string owner, string repo, string title, string? body, IReadOnlyList<string>? labels, CancellationToken cancellationToken = default);
}

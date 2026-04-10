using BarretApi.Core.Interfaces;

namespace BarretApi.Infrastructure.GitHub;

public sealed class GitHubTokenProvider(IGitHubTokenStore tokenStore)
{
    private readonly IGitHubTokenStore _tokenStore = tokenStore;
    private string? _cachedAccessToken;

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(_cachedAccessToken))
        {
            return _cachedAccessToken;
        }

        var token = await _tokenStore.GetTokenAsync(cancellationToken);
        if (token is null)
        {
            throw new InvalidOperationException(
                "No GitHub token found. Visit /api/github/auth to authorize.");
        }

        _cachedAccessToken = token.AccessToken;
        return _cachedAccessToken;
    }

    public void ClearCache()
    {
        _cachedAccessToken = null;
    }
}

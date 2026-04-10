namespace BarretApi.Core.Configuration;

public sealed class GitHubOptions
{
    public const string SectionName = "GitHub";

    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
    public string ApiBaseUrl { get; init; } = "https://api.github.com";
    public string OAuthBaseUrl { get; init; } = "https://github.com";
    public GitHubTokenStorageOptions TokenStorage { get; init; } = new();
    public GitHubRepoStorageOptions RepoStorage { get; init; } = new();
}

public sealed class GitHubTokenStorageOptions
{
    public string? ConnectionString { get; init; }
    public string AccountEndpoint { get; init; } = string.Empty;
    public string TableName { get; init; } = "githubtokens";
}

public sealed class GitHubRepoStorageOptions
{
    public string? ConnectionString { get; init; }
    public string AccountEndpoint { get; init; } = string.Empty;
    public string TableName { get; init; } = "githubrepositories";
}

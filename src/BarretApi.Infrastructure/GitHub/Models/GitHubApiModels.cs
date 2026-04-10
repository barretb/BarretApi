using System.Text.Json.Serialization;

namespace BarretApi.Infrastructure.GitHub.Models;

internal sealed class GitHubTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; init; } = string.Empty;

    [JsonPropertyName("token_type")]
    public string TokenType { get; init; } = string.Empty;

    [JsonPropertyName("scope")]
    public string Scope { get; init; } = string.Empty;
}

internal sealed class GitHubUserResponse
{
    [JsonPropertyName("login")]
    public string Login { get; init; } = string.Empty;
}

internal sealed class GitHubRepoResponse
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("full_name")]
    public string FullName { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("private")]
    public bool IsPrivate { get; init; }

    [JsonPropertyName("default_branch")]
    public string DefaultBranch { get; init; } = string.Empty;

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; init; } = string.Empty;

    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; init; }
}

internal sealed class GitHubIssueResponse
{
    [JsonPropertyName("number")]
    public int Number { get; init; }

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; init; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; init; } = string.Empty;
}

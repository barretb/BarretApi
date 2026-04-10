namespace BarretApi.Core.Models;

public sealed class GitHubTokenRecord
{
    public string AccessToken { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string Scope { get; init; } = string.Empty;
    public DateTimeOffset UpdatedAtUtc { get; init; }
}

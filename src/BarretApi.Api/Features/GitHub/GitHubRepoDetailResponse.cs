namespace BarretApi.Api.Features.GitHub;

public sealed class GitHubRepoDetailResponse
{
    public string Name { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsPrivate { get; init; }
    public string DefaultBranch { get; init; } = string.Empty;
    public string HtmlUrl { get; init; } = string.Empty;
    public DateTimeOffset UpdatedAtUtc { get; init; }
    public DateTimeOffset SyncedAtUtc { get; init; }
}

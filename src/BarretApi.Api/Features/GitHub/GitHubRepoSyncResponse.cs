namespace BarretApi.Api.Features.GitHub;

public sealed class GitHubRepoSyncResponse
{
    public int Count { get; init; }
    public DateTimeOffset SyncedAtUtc { get; init; }
    public string Username { get; init; } = string.Empty;
}

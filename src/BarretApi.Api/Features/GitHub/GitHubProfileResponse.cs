namespace BarretApi.Api.Features.GitHub;

public sealed class GitHubProfileResponse
{
    public string? Username { get; init; }
    public bool Connected { get; init; }
    public string? Scope { get; init; }
    public DateTimeOffset? ConnectedAtUtc { get; init; }
}

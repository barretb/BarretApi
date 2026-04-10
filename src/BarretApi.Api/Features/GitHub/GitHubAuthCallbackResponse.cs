namespace BarretApi.Api.Features.GitHub;

public sealed class GitHubAuthCallbackResponse
{
    public string Username { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Scope { get; init; } = string.Empty;
}

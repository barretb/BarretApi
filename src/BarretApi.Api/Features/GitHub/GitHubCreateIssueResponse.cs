namespace BarretApi.Api.Features.GitHub;

public sealed class GitHubCreateIssueResponse
{
    public int Number { get; init; }
    public string Title { get; init; } = string.Empty;
    public string HtmlUrl { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
}

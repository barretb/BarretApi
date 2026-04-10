namespace BarretApi.Core.Models;

public sealed class GitHubIssueResult
{
    public int Number { get; init; }
    public string Title { get; init; } = string.Empty;
    public string HtmlUrl { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
}

using FastEndpoints;

namespace BarretApi.Api.Features.GitHub;

public sealed class GitHubCreateIssueRequest
{
    [BindFrom("name")]
    public string Name { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;
    public string? Body { get; init; }
    public List<string>? Labels { get; init; }
}

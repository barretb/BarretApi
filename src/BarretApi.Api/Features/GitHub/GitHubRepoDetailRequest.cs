using FastEndpoints;

namespace BarretApi.Api.Features.GitHub;

public sealed class GitHubRepoDetailRequest
{
    [BindFrom("name")]
    public string Name { get; init; } = string.Empty;
}

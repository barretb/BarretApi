using BarretApi.Core.Interfaces;
using FastEndpoints;

namespace BarretApi.Api.Features.GitHub;

public sealed class GitHubRepoListEndpoint(IGitHubRepositoryStore repoStore)
    : EndpointWithoutRequest<GitHubRepoListResponse>
{
    public override void Configure()
    {
        Get("/api/github/repos");
        Summary(s =>
        {
            s.Summary = "List GitHub repositories";
            s.Description = "Returns all locally stored GitHub repositories.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var repos = await repoStore.GetAllAsync(ct);

        var summaries = repos.Select(r => new GitHubRepoSummary
        {
            Name = r.Name,
            FullName = r.FullName,
            Description = r.Description,
            IsPrivate = r.IsPrivate,
            DefaultBranch = r.DefaultBranch,
            HtmlUrl = r.HtmlUrl,
            UpdatedAtUtc = r.UpdatedAtUtc
        }).ToList();

        var syncedAt = repos.Count > 0 ? repos[0].SyncedAtUtc : (DateTimeOffset?)null;

        await Send.ResponseAsync(new GitHubRepoListResponse
        {
            Repositories = summaries,
            Count = summaries.Count,
            SyncedAtUtc = syncedAt
        }, 200, ct);
    }
}

using BarretApi.Core.Interfaces;
using FastEndpoints;

namespace BarretApi.Api.Features.GitHub;

public sealed class GitHubRepoDetailEndpoint(IGitHubRepositoryStore repoStore)
    : Endpoint<GitHubRepoDetailRequest, GitHubRepoDetailResponse>
{
    public override void Configure()
    {
        Get("/api/github/repos/{name}");
        Summary(s =>
        {
            s.Summary = "Get repository details";
            s.Description = "Returns details for a single stored repository by name.";
        });
    }

    public override async Task HandleAsync(GitHubRepoDetailRequest req, CancellationToken ct)
    {
        var repo = await repoStore.GetByNameAsync(req.Name, ct);

        if (repo is null)
        {
            HttpContext.Response.StatusCode = 404;
            await HttpContext.Response.WriteAsJsonAsync(new
            {
                statusCode = 404,
                message = $"Repository '{req.Name}' not found. Run POST /api/github/repos/sync to refresh."
            }, ct);
            return;
        }

        await Send.ResponseAsync(new GitHubRepoDetailResponse
        {
            Name = repo.Name,
            FullName = repo.FullName,
            Description = repo.Description,
            IsPrivate = repo.IsPrivate,
            DefaultBranch = repo.DefaultBranch,
            HtmlUrl = repo.HtmlUrl,
            UpdatedAtUtc = repo.UpdatedAtUtc,
            SyncedAtUtc = repo.SyncedAtUtc
        }, 200, ct);
    }
}

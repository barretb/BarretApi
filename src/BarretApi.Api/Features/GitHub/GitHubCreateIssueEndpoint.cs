using BarretApi.Core.Interfaces;
using FastEndpoints;

namespace BarretApi.Api.Features.GitHub;

public sealed class GitHubCreateIssueEndpoint(
    IGitHubClient gitHubClient,
    IGitHubRepositoryStore repoStore,
    IGitHubTokenStore tokenStore,
    ILogger<GitHubCreateIssueEndpoint> logger)
    : Endpoint<GitHubCreateIssueRequest, GitHubCreateIssueResponse>
{
    public override void Configure()
    {
        Post("/api/github/repos/{name}/issues");
        Summary(s =>
        {
            s.Summary = "Create a GitHub issue";
            s.Description = "Creates a new issue on the specified GitHub repository.";
        });
    }

    public override async Task HandleAsync(GitHubCreateIssueRequest req, CancellationToken ct)
    {
        var token = await tokenStore.GetTokenAsync(ct);
        if (token is null)
        {
            HttpContext.Response.StatusCode = 401;
            await HttpContext.Response.WriteAsJsonAsync(
                new { statusCode = 401, message = "GitHub authentication required. Visit /api/github/auth to connect." },
                ct);
            return;
        }

        var repo = await repoStore.GetByNameAsync(req.Name, ct);
        if (repo is null)
        {
            HttpContext.Response.StatusCode = 404;
            await HttpContext.Response.WriteAsJsonAsync(
                new { statusCode = 404, message = $"Repository '{req.Name}' not found. Run POST /api/github/repos/sync to refresh." },
                ct);
            return;
        }

        var owner = repo.FullName.Split('/')[0];

        try
        {
            var result = await gitHubClient.CreateIssueAsync(
                owner, repo.Name, req.Title, req.Body, req.Labels, ct);

            logger.LogInformation("Created issue #{Number} on {FullName}", result.Number, repo.FullName);

            await Send.ResponseAsync(new GitHubCreateIssueResponse
            {
                Number = result.Number,
                Title = result.Title,
                HtmlUrl = result.HtmlUrl,
                State = result.State
            }, 201, ct);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("reauthenticate"))
        {
            HttpContext.Response.StatusCode = 401;
            await HttpContext.Response.WriteAsJsonAsync(
                new { statusCode = 401, message = ex.Message },
                ct);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("rate limit"))
        {
            HttpContext.Response.StatusCode = 429;
            await HttpContext.Response.WriteAsJsonAsync(
                new { statusCode = 429, message = ex.Message },
                ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GitHub issue creation failed on {FullName}", repo.FullName);
            HttpContext.Response.StatusCode = 502;
            await HttpContext.Response.WriteAsJsonAsync(
                new { statusCode = 502, message = $"GitHub API error: {ex.Message}" },
                ct);
        }
    }
}

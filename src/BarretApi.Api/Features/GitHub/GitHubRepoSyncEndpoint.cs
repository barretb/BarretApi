using BarretApi.Core.Interfaces;
using FastEndpoints;

namespace BarretApi.Api.Features.GitHub;

public sealed class GitHubRepoSyncEndpoint(
    IGitHubClient gitHubClient,
    IGitHubRepositoryStore repoStore,
    IGitHubTokenStore tokenStore,
    ILogger<GitHubRepoSyncEndpoint> logger)
    : EndpointWithoutRequest<GitHubRepoSyncResponse>
{
    public override void Configure()
    {
        Post("/api/github/repos/sync");
        Summary(s =>
        {
            s.Summary = "Sync GitHub repositories";
            s.Description = "Fetches all repositories from GitHub and stores them locally.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
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

        try
        {
            var repos = await gitHubClient.GetRepositoriesAsync(ct);
            await repoStore.ReplaceAllAsync(token.Username, repos, ct);

            var syncedAt = repos.Count > 0 ? repos[0].SyncedAtUtc : DateTimeOffset.UtcNow;
            logger.LogInformation("Synced {Count} repositories for {Username}", repos.Count, token.Username);

            await Send.ResponseAsync(new GitHubRepoSyncResponse
            {
                Count = repos.Count,
                SyncedAtUtc = syncedAt,
                Username = token.Username
            }, 200, ct);
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
            logger.LogError(ex, "GitHub repository sync failed");
            HttpContext.Response.StatusCode = 502;
            await HttpContext.Response.WriteAsJsonAsync(
                new { statusCode = 502, message = $"GitHub API error: {ex.Message}" },
                ct);
        }
    }
}

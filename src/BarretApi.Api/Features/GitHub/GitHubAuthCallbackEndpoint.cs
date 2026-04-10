using BarretApi.Core.Interfaces;
using FastEndpoints;

namespace BarretApi.Api.Features.GitHub;

public sealed class GitHubAuthCallbackEndpoint(
    IGitHubClient gitHubClient,
    IGitHubTokenStore tokenStore,
    ILogger<GitHubAuthCallbackEndpoint> logger)
    : Endpoint<GitHubAuthCallbackRequest, GitHubAuthCallbackResponse>
{
    public override void Configure()
    {
        Get("/api/github/auth/callback");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "GitHub OAuth callback";
            s.Description = "Receives the authorization code from GitHub, exchanges it for an access token, and persists it to the token store.";
        });
    }

    public override async Task HandleAsync(GitHubAuthCallbackRequest req, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(req.Error))
        {
            logger.LogWarning("GitHub OAuth denied: {Error} - {Description}", req.Error, req.ErrorDescription);
            HttpContext.Response.StatusCode = 400;
            await HttpContext.Response.WriteAsJsonAsync(
                new { statusCode = 400, message = $"GitHub authentication failed: {req.ErrorDescription ?? req.Error}" },
                ct);
            return;
        }

        try
        {
            var redirectUri = BuildRedirectUri();
            var tokenRecord = await gitHubClient.ExchangeCodeForTokenAsync(req.Code!, redirectUri, ct);

            await tokenStore.SaveTokenAsync(tokenRecord, ct);
            logger.LogInformation("GitHub OAuth completed for user {Username}", tokenRecord.Username);

            await Send.ResponseAsync(new GitHubAuthCallbackResponse
            {
                Username = tokenRecord.Username,
                Status = "connected",
                Scope = tokenRecord.Scope
            }, 200, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to exchange GitHub authorization code for token");
            HttpContext.Response.StatusCode = 502;
            await HttpContext.Response.WriteAsJsonAsync(
                new { statusCode = 502, message = $"Token exchange failed: {ex.Message}" },
                ct);
        }
    }

    private string BuildRedirectUri()
    {
        var request = HttpContext.Request;
        return $"{request.Scheme}://{request.Host}/api/github/auth/callback";
    }
}

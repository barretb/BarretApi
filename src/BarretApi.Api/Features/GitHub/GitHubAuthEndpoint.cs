using BarretApi.Core.Configuration;
using FastEndpoints;
using Microsoft.Extensions.Options;

namespace BarretApi.Api.Features.GitHub;

public sealed class GitHubAuthEndpoint(IOptions<GitHubOptions> options)
    : EndpointWithoutRequest
{
    private readonly GitHubOptions _options = options.Value;

    public override void Configure()
    {
        Get("/api/github/auth");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Initiate GitHub OAuth flow";
            s.Description = "Open this URL directly in a browser to start the GitHub OAuth flow. "
                + "When called from an API client (non-browser), returns a JSON object with the authorization URL instead of redirecting.";
            s.ResponseExamples[200] = new { authUrl = "https://github.com/login/oauth/authorize?..." };
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var redirectUri = BuildRedirectUri();
        var state = Guid.NewGuid().ToString("N");

        var authUrl = $"{_options.OAuthBaseUrl}/login/oauth/authorize"
            + $"?client_id={Uri.EscapeDataString(_options.ClientId)}"
            + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
            + $"&scope=repo"
            + $"&state={state}";

        var accept = HttpContext.Request.Headers.Accept.ToString();
        if (accept.Contains("text/html", StringComparison.OrdinalIgnoreCase))
        {
            HttpContext.Response.Redirect(authUrl);
            await HttpContext.Response.CompleteAsync();
        }
        else
        {
            await Send.ResponseAsync(new { authUrl }, cancellation: ct);
        }
    }

    private string BuildRedirectUri()
    {
        var request = HttpContext.Request;
        return $"{request.Scheme}://{request.Host}/api/github/auth/callback";
    }
}

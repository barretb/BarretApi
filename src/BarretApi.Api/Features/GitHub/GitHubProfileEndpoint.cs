using BarretApi.Core.Interfaces;
using FastEndpoints;

namespace BarretApi.Api.Features.GitHub;

public sealed class GitHubProfileEndpoint(IGitHubTokenStore tokenStore)
    : EndpointWithoutRequest<GitHubProfileResponse>
{
    public override void Configure()
    {
        Get("/api/github/profile");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Get GitHub connection status";
            s.Description = "Returns the current GitHub connection status and authenticated username.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var token = await tokenStore.GetTokenAsync(ct);

        if (token is null)
        {
            await Send.ResponseAsync(new GitHubProfileResponse
            {
                Connected = false
            }, 200, ct);
            return;
        }

        await Send.ResponseAsync(new GitHubProfileResponse
        {
            Username = token.Username,
            Connected = true,
            Scope = token.Scope,
            ConnectedAtUtc = token.UpdatedAtUtc
        }, 200, ct);
    }
}

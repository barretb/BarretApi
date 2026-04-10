using FastEndpoints;

namespace BarretApi.Api.Features.GitHub;

public sealed class GitHubAuthCallbackRequest
{
    [QueryParam]
    public string? Code { get; init; }

    [QueryParam]
    public string? State { get; init; }

    [QueryParam]
    public string? Error { get; init; }

    [QueryParam, BindFrom("error_description")]
    public string? ErrorDescription { get; init; }
}

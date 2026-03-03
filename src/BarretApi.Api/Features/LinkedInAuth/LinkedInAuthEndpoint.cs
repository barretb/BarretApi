using BarretApi.Core.Configuration;
using FastEndpoints;
using Microsoft.Extensions.Options;

namespace BarretApi.Api.Features.LinkedInAuth;

public sealed class LinkedInAuthEndpoint(IOptions<LinkedInOptions> options)
	: EndpointWithoutRequest
{
	private readonly LinkedInOptions _options = options.Value;

	public override void Configure()
	{
		Get("/api/linkedin/auth");
		AllowAnonymous();
		Summary(s =>
		{
			s.Summary = "Initiate LinkedIn OAuth flow";
			s.Description = "Open this URL directly in a browser to start the LinkedIn OAuth flow. "
				+ "When called from an API client (non-browser), returns a JSON object with the authorization URL instead of redirecting.";
			s.ResponseExamples[200] = new { authUrl = "https://www.linkedin.com/oauth/v2/authorization?..." };
		});
	}

	public override async Task HandleAsync(CancellationToken ct)
	{
		var redirectUri = BuildRedirectUri();
		var state = Guid.NewGuid().ToString("N");

		var authUrl = $"{_options.OAuthBaseUrl}/oauth/v2/authorization"
			+ $"?response_type=code"
			+ $"&client_id={Uri.EscapeDataString(_options.ClientId)}"
			+ $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
			+ $"&state={state}"
			+ $"&scope={Uri.EscapeDataString("openid profile w_member_social")}";

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
		return $"{request.Scheme}://{request.Host}/api/linkedin/auth/callback";
	}
}

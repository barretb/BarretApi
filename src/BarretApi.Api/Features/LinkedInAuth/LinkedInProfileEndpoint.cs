using System.Net.Http.Headers;
using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using FastEndpoints;
using Microsoft.Extensions.Options;

namespace BarretApi.Api.Features.LinkedInAuth;

public sealed class LinkedInProfileEndpoint(
	IHttpClientFactory httpClientFactory,
	ILinkedInTokenStore tokenStore,
	IOptions<LinkedInOptions> options)
	: EndpointWithoutRequest
{
	private readonly LinkedInOptions _options = options.Value;

	public override void Configure()
	{
		Get("/api/linkedin/profile");
		AllowAnonymous();
		Summary(s =>
		{
			s.Summary = "Get LinkedIn profile info";
			s.Description = "Returns your LinkedIn profile info including the member URN (sub) needed for AuthorUrn configuration.";
		});
	}

	public override async Task HandleAsync(CancellationToken ct)
	{
		var tokens = await tokenStore.GetTokensAsync(ct);
		if (tokens is null)
		{
			await Send.ResponseAsync(new { error = "No LinkedIn tokens found. Visit /api/linkedin/auth first." }, 400, ct);
			return;
		}

		var client = httpClientFactory.CreateClient("LinkedInOAuth");
		using var request = new HttpRequestMessage(System.Net.Http.HttpMethod.Get,
			$"{_options.ApiBaseUrl}/v2/userinfo");
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

		var response = await client.SendAsync(request, ct);
		var body = await response.Content.ReadAsStringAsync(ct);

		if (!response.IsSuccessStatusCode)
		{
			await Send.ResponseAsync(new { error = "Failed to fetch profile", statusCode = (int)response.StatusCode, details = body }, 502, ct);
			return;
		}

		await Send.ResponseAsync(
			System.Text.Json.JsonSerializer.Deserialize<object>(body),
			cancellation: ct);
	}
}

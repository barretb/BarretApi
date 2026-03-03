using System.Net.Http.Json;
using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Infrastructure.LinkedIn.Models;
using FastEndpoints;
using Microsoft.Extensions.Options;

namespace BarretApi.Api.Features.LinkedInAuth;

public sealed class LinkedInAuthCallbackRequest
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

public sealed class LinkedInAuthCallbackResponse
{
	public bool Success { get; init; }
	public string? Message { get; init; }
}

public sealed class LinkedInAuthCallbackEndpoint(
	IHttpClientFactory httpClientFactory,
	IOptions<LinkedInOptions> options,
	ILinkedInTokenStore tokenStore,
	ILogger<LinkedInAuthCallbackEndpoint> logger)
	: Endpoint<LinkedInAuthCallbackRequest, LinkedInAuthCallbackResponse>
{
	private readonly LinkedInOptions _options = options.Value;

	public override void Configure()
	{
		Get("/api/linkedin/auth/callback");
		AllowAnonymous();
		Summary(s =>
		{
			s.Summary = "LinkedIn OAuth callback";
			s.Description = "Receives the authorization code from LinkedIn, exchanges it for access and refresh tokens, and persists them to the token store.";
		});
	}

	public override async Task HandleAsync(LinkedInAuthCallbackRequest req, CancellationToken ct)
	{
		if (!string.IsNullOrWhiteSpace(req.Error))
		{
			logger.LogWarning("LinkedIn OAuth denied: {Error} - {Description}", req.Error, req.ErrorDescription);
			await Send.ResponseAsync(new LinkedInAuthCallbackResponse
			{
				Success = false,
				Message = $"LinkedIn authorization denied: {req.ErrorDescription ?? req.Error}"
			}, 400, ct);
			return;
		}

		if (string.IsNullOrWhiteSpace(req.Code))
		{
			await Send.ResponseAsync(new LinkedInAuthCallbackResponse
			{
				Success = false,
				Message = "No authorization code received from LinkedIn."
			}, 400, ct);
			return;
		}

		try
		{
			var redirectUri = BuildRedirectUri();
			var tokenResponse = await ExchangeCodeAsync(req.Code, redirectUri, ct);

			if (string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
			{
				await Send.ResponseAsync(new LinkedInAuthCallbackResponse
				{
					Success = false,
					Message = "LinkedIn token exchange did not return an access token."
				}, 502, ct);
				return;
			}

			var expiresIn = tokenResponse.ExpiresIn > 0 ? tokenResponse.ExpiresIn : 3600;

			var record = new LinkedInTokenRecord
			{
				AccessToken = tokenResponse.AccessToken,
				RefreshToken = tokenResponse.RefreshToken ?? string.Empty,
				AccessTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(expiresIn),
				RefreshTokenExpiresAtUtc = tokenResponse.RefreshTokenExpiresIn > 0
					? DateTimeOffset.UtcNow.AddSeconds(tokenResponse.RefreshTokenExpiresIn)
					: DateTimeOffset.UtcNow.AddDays(365),
				UpdatedAtUtc = DateTimeOffset.UtcNow
			};

			await tokenStore.SaveTokensAsync(record, ct);
			logger.LogInformation("LinkedIn OAuth tokens acquired and saved to token store");

			await Send.ResponseAsync(new LinkedInAuthCallbackResponse
			{
				Success = true,
				Message = "LinkedIn authorization successful. Tokens have been saved."
			}, 200, ct);
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Failed to exchange LinkedIn authorization code for tokens");
			await Send.ResponseAsync(new LinkedInAuthCallbackResponse
			{
				Success = false,
				Message = $"Token exchange failed: {ex.Message}"
			}, 502, ct);
		}
	}

	private async Task<LinkedInAccessTokenResponse> ExchangeCodeAsync(
		string code,
		string redirectUri,
		CancellationToken cancellationToken)
	{
		var httpClient = httpClientFactory.CreateClient("LinkedInOAuth");

		var form = new Dictionary<string, string>
		{
			["grant_type"] = "authorization_code",
			["code"] = code,
			["redirect_uri"] = redirectUri,
			["client_id"] = _options.ClientId,
			["client_secret"] = _options.ClientSecret
		};

		using var content = new FormUrlEncodedContent(form);
		var response = await httpClient.PostAsync("/oauth/v2/accessToken", content, cancellationToken);

		if (!response.IsSuccessStatusCode)
		{
			var errorBody = await response.Content.ReadFromJsonAsync<LinkedInErrorResponse>(cancellationToken);
			var message = errorBody?.ErrorDescription ?? errorBody?.Message ?? $"HTTP {(int)response.StatusCode}";
			throw new InvalidOperationException($"LinkedIn token exchange failed: {message}");
		}

		return await response.Content.ReadFromJsonAsync<LinkedInAccessTokenResponse>(cancellationToken)
			?? throw new InvalidOperationException("LinkedIn token exchange returned an unreadable response.");
	}

	private string BuildRedirectUri()
	{
		var request = HttpContext.Request;
		return $"{request.Scheme}://{request.Host}/api/linkedin/auth/callback";
	}
}

using System.Net.Http.Json;
using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Infrastructure.LinkedIn.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BarretApi.Infrastructure.LinkedIn;

public sealed class LinkedInTokenProvider(
	HttpClient httpClient,
	IOptions<LinkedInOptions> options,
	ILinkedInTokenStore tokenStore,
	ILogger<LinkedInTokenProvider> logger)
{
	private readonly LinkedInOptions _options = options.Value;
	private readonly SemaphoreSlim _refreshLock = new(1, 1);
	private string? _cachedAccessToken;
	private string? _cachedRefreshToken;
	private DateTimeOffset _cachedTokenExpiresAtUtc = DateTimeOffset.MinValue;

	public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
	{
		if (!string.IsNullOrWhiteSpace(_cachedAccessToken)
			&& _cachedTokenExpiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(2))
		{
			return _cachedAccessToken;
		}

		await _refreshLock.WaitAsync(cancellationToken);
		try
		{
			if (!string.IsNullOrWhiteSpace(_cachedAccessToken)
				&& _cachedTokenExpiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(2))
			{
				return _cachedAccessToken;
			}

			var storedTokens = await tokenStore.GetTokensAsync(cancellationToken);
			if (storedTokens is null)
			{
				throw new InvalidOperationException(
					"No LinkedIn tokens found. Visit /api/linkedin/auth to authorize.");
			}

			if (storedTokens.AccessTokenExpiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(2))
			{
				_cachedAccessToken = storedTokens.AccessToken;
				_cachedRefreshToken = storedTokens.RefreshToken;
				_cachedTokenExpiresAtUtc = storedTokens.AccessTokenExpiresAtUtc;
				return _cachedAccessToken;
			}

			logger.LogInformation("LinkedIn access token expired, attempting refresh");

			if (string.IsNullOrWhiteSpace(storedTokens.RefreshToken))
			{
				throw new InvalidOperationException(
					"LinkedIn access token has expired and no refresh token is available. Visit /api/linkedin/auth to re-authorize.");
			}

			var response = await RefreshTokenAsync(storedTokens.RefreshToken, cancellationToken);

			if (string.IsNullOrWhiteSpace(response.AccessToken))
			{
				throw new InvalidOperationException("LinkedIn token response did not include an access token.");
			}

			var expiresIn = response.ExpiresIn > 0 ? response.ExpiresIn : 3600;
			var newRefreshToken = !string.IsNullOrWhiteSpace(response.RefreshToken)
				? response.RefreshToken
				: storedTokens.RefreshToken;

			var updatedRecord = new LinkedInTokenRecord
			{
				AccessToken = response.AccessToken,
				RefreshToken = newRefreshToken,
				AccessTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(expiresIn),
				RefreshTokenExpiresAtUtc = response.RefreshTokenExpiresIn > 0
					? DateTimeOffset.UtcNow.AddSeconds(response.RefreshTokenExpiresIn)
					: storedTokens.RefreshTokenExpiresAtUtc,
				UpdatedAtUtc = DateTimeOffset.UtcNow
			};

			await tokenStore.SaveTokensAsync(updatedRecord, cancellationToken);
			logger.LogInformation("LinkedIn tokens refreshed and persisted to store");

			_cachedAccessToken = updatedRecord.AccessToken;
			_cachedRefreshToken = updatedRecord.RefreshToken;
			_cachedTokenExpiresAtUtc = updatedRecord.AccessTokenExpiresAtUtc;

			return _cachedAccessToken;
		}
		finally
		{
			_refreshLock.Release();
		}
	}

	private async Task<LinkedInAccessTokenResponse> RefreshTokenAsync(
		string refreshToken,
		CancellationToken cancellationToken)
	{
		var form = new Dictionary<string, string>
		{
			["grant_type"] = "refresh_token",
			["refresh_token"] = refreshToken,
			["client_id"] = _options.ClientId,
			["client_secret"] = _options.ClientSecret
		};

		using var content = new FormUrlEncodedContent(form);
		var httpResponse = await httpClient.PostAsync("/oauth/v2/accessToken", content, cancellationToken);

		if (!httpResponse.IsSuccessStatusCode)
		{
			var errorBody = await httpResponse.Content.ReadFromJsonAsync<LinkedInErrorResponse>(cancellationToken);
			var message = errorBody?.ErrorDescription ?? errorBody?.Message ?? $"HTTP {(int)httpResponse.StatusCode}";
			throw new InvalidOperationException($"LinkedIn token refresh failed: {message}");
		}

		return await httpResponse.Content.ReadFromJsonAsync<LinkedInAccessTokenResponse>(cancellationToken)
			?? throw new InvalidOperationException("LinkedIn token refresh returned an unreadable response.");
	}
}

using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;

namespace BarretApi.Api.Auth;

/// <summary>Browser-bound, expiring, single-use authorization requests. Restarts fail closed.</summary>
public sealed class OAuthStateService(IMemoryCache cache, TimeProvider clock)
{
	private readonly IMemoryCache _cache = cache;
	private readonly TimeProvider _clock = clock;
	private readonly object _gate = new();
	private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

	public string Create(HttpContext context, string provider)
	{
		var state = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
		var binding = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
		_cache.Set(state, new AuthorizationRequest(provider, binding, _clock.GetUtcNow() + Lifetime), Lifetime);
		context.Response.Cookies.Append(CookieName(provider), binding, new CookieOptions
		{
			HttpOnly = true,
			Secure = true,
			SameSite = SameSiteMode.Lax,
			Path = "/",
			MaxAge = Lifetime
		});
		return state;
	}

	public bool TryConsume(HttpContext context, string provider, string? state)
	{
		if (string.IsNullOrWhiteSpace(state) || state.Length != 64)
		{
			return false;
		}

		lock (_gate)
		{
			if (!_cache.TryGetValue<AuthorizationRequest>(state, out var request)
				|| request is null || request.Provider != provider || request.ExpiresAt <= _clock.GetUtcNow()
				|| !context.Request.Cookies.TryGetValue(CookieName(provider), out var binding)
				|| binding != request.Binding)
			{
				return false;
			}

			_cache.Remove(state);
			context.Response.Cookies.Delete(CookieName(provider), new CookieOptions { Secure = true, Path = "/" });
			return true;
		}
	}

	private static string CookieName(string provider)
	{
		return $"__Host-BarretApi-OAuth-{provider}";
	}

	private sealed record AuthorizationRequest(string Provider, string Binding, DateTimeOffset ExpiresAt);
}

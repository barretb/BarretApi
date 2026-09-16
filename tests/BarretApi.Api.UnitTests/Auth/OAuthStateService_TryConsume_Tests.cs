using BarretApi.Api.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Shouldly;

namespace BarretApi.Api.UnitTests.Auth;

public sealed class OAuthStateService_TryConsume_Tests
{
	[Fact]
	public void ConsumesOnlyOnce_GivenMatchingBrowserAndProvider()
	{
		using var cache = new MemoryCache(new MemoryCacheOptions());
		var service = new OAuthStateService(cache, TimeProvider.System);
		var context = new DefaultHttpContext();
		var state = service.Create(context, "github");
		context.Request.Headers.Cookie = context.Response.Headers.SetCookie.ToString().Split(';')[0];

		service.TryConsume(context, "github", state).ShouldBeTrue();
		service.TryConsume(context, "github", state).ShouldBeFalse();
	}

	[Theory]
	[InlineData("missing-cookie")]
	[InlineData("wrong-cookie")]
	[InlineData("wrong-provider")]
	[InlineData("wrong-state")]
	[InlineData("expired")]
	public void RejectsAuthorization_GivenInvalidBinding(string scenario)
	{
		using var cache = new MemoryCache(new MemoryCacheOptions());
		var clock = new TestClock();
		var service = new OAuthStateService(cache, clock);
		var context = new DefaultHttpContext();
		var state = service.Create(context, "github");
		context.Request.Headers.Cookie = context.Response.Headers.SetCookie.ToString().Split(';')[0];
		var provider = "github";
		switch (scenario)
		{
			case "missing-cookie": context.Request.Headers.Remove("Cookie"); break;
			case "wrong-cookie": context.Request.Headers.Cookie = "__Host-BarretApi-OAuth-github=wrong"; break;
			case "wrong-provider": provider = "linkedin"; break;
			case "wrong-state": state = new string('0', 64); break;
			case "expired": clock.Now = clock.Now.AddMinutes(11); break;
		}

		service.TryConsume(context, provider, state).ShouldBeFalse();
	}

	[Fact]
	public async Task ConsumesOnce_GivenConcurrentCallbacks()
	{
		using var cache = new MemoryCache(new MemoryCacheOptions());
		var service = new OAuthStateService(cache, TimeProvider.System);
		var initial = new DefaultHttpContext();
		var state = service.Create(initial, "github");
		var cookie = initial.Response.Headers.SetCookie.ToString().Split(';')[0];

		var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Task.Run(() =>
		{
			var callback = new DefaultHttpContext();
			callback.Request.Headers.Cookie = cookie;
			return service.TryConsume(callback, "github", state);
		})));

		results.Count(success => success).ShouldBe(1);
	}

	private sealed class TestClock : TimeProvider
	{
		public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
		public override DateTimeOffset GetUtcNow()
		{
			return Now;
		}
	}
}

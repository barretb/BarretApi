using BarretApi.Api.Auth;
using BarretApi.Api.Features.GitHub;
using BarretApi.Api.Features.LinkedInAuth;
using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace BarretApi.Api.UnitTests.Features.GitHub;

public sealed class GitHubAuthCallbackEndpoint_Tests
{
	[Fact]
	public async Task ExchangesCodeAndSavesToken_GivenValidBrowserState()
	{
		using var cache = new MemoryCache(new MemoryCacheOptions());
		var stateService = new OAuthStateService(cache, TimeProvider.System);
		var start = new DefaultHttpContext();
		var state = stateService.Create(start, "github");
		var client = Substitute.For<IGitHubClient>();
		var store = Substitute.For<IGitHubTokenStore>();
		var token = new GitHubTokenRecord { AccessToken = "test", Username = "tester", Scope = "repo", UpdatedAtUtc = DateTimeOffset.UtcNow };
		client.ExchangeCodeForTokenAsync("code", Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(token);
		var endpoint = Factory.Create<GitHubAuthCallbackEndpoint>(client, store, Substitute.For<ILogger<GitHubAuthCallbackEndpoint>>(), stateService);
		endpoint.HttpContext.Request.Scheme = "https";
		endpoint.HttpContext.Request.Host = new HostString("localhost");
		endpoint.HttpContext.Request.Headers.Cookie = start.Response.Headers.SetCookie.ToString().Split(';')[0];

		await endpoint.HandleAsync(new GitHubAuthCallbackRequest { Code = "code", State = state }, default);

		await store.Received(1).SaveTokenAsync(token, Arg.Any<CancellationToken>());
		endpoint.HttpContext.Response.StatusCode.ShouldBe(200);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("arbitrary-nonempty-state")]
	public async Task RejectsBeforeTokenExchange_GivenUnverifiedState(string? state)
	{
		using var cache = new MemoryCache(new MemoryCacheOptions());
		var stateService = new OAuthStateService(cache, TimeProvider.System);
		var client = Substitute.For<IGitHubClient>();
		var store = Substitute.For<IGitHubTokenStore>();
		var endpoint = Factory.Create<GitHubAuthCallbackEndpoint>(client, store, Substitute.For<ILogger<GitHubAuthCallbackEndpoint>>(), stateService);

		await endpoint.HandleAsync(new GitHubAuthCallbackRequest { Code = "code", State = state }, default);

		endpoint.HttpContext.Response.StatusCode.ShouldBe(400);
		await client.DidNotReceiveWithAnyArgs().ExchangeCodeForTokenAsync(default!, default!, default);
		await store.DidNotReceiveWithAnyArgs().SaveTokenAsync(default!, default);
	}

	[Fact]
	public async Task RejectsLinkedInBeforeTokenExchange_GivenUnverifiedState()
	{
		using var cache = new MemoryCache(new MemoryCacheOptions());
		var factory = Substitute.For<IHttpClientFactory>();
		var store = Substitute.For<ILinkedInTokenStore>();
		var endpoint = Factory.Create<LinkedInAuthCallbackEndpoint>(factory, Options.Create(new LinkedInOptions()), store,
			Substitute.For<ILogger<LinkedInAuthCallbackEndpoint>>(), new OAuthStateService(cache, TimeProvider.System));

		await endpoint.HandleAsync(new LinkedInAuthCallbackRequest { Code = "code", State = "unverified" }, default);

		endpoint.HttpContext.Response.StatusCode.ShouldBe(400);
		factory.DidNotReceiveWithAnyArgs().CreateClient(default!);
		await store.DidNotReceiveWithAnyArgs().SaveTokensAsync(default!, default);
	}
}

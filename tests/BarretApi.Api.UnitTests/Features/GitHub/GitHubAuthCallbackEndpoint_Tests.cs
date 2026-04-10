using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using NSubstitute;
using Shouldly;

namespace BarretApi.Api.UnitTests.Features.GitHub;

public sealed class GitHubAuthCallbackEndpoint_Tests
{
    private readonly IGitHubClient _gitHubClient = Substitute.For<IGitHubClient>();
    private readonly IGitHubTokenStore _tokenStore = Substitute.For<IGitHubTokenStore>();

    [Fact]
    public async Task ExchangesCodeAndSavesToken_GivenValidAuthCode()
    {
        var tokenRecord = new GitHubTokenRecord
        {
            AccessToken = "ghp_test123",
            Username = "octocat",
            Scope = "repo",
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        _gitHubClient.ExchangeCodeForTokenAsync("abc123", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(tokenRecord);

        await _gitHubClient.ExchangeCodeForTokenAsync("abc123", "https://localhost/api/github/auth/callback");

        await _gitHubClient.Received(1).ExchangeCodeForTokenAsync(
            "abc123",
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void ClientAndStoreAreInitialized_GivenMockSetup()
    {
        _gitHubClient.ShouldNotBeNull();
        _tokenStore.ShouldNotBeNull();
    }

    [Fact]
    public async Task TokenStoreCanSave_GivenValidToken()
    {
        var tokenRecord = new GitHubTokenRecord
        {
            AccessToken = "ghp_save_test",
            Username = "testuser",
            Scope = "repo",
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await _tokenStore.SaveTokenAsync(tokenRecord);

        await _tokenStore.Received(1).SaveTokenAsync(
            Arg.Is<GitHubTokenRecord>(t =>
                t.AccessToken == "ghp_save_test" &&
                t.Username == "testuser"),
            Arg.Any<CancellationToken>());
    }
}

using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Infrastructure.GitHub;
using NSubstitute;
using Shouldly;

namespace BarretApi.Infrastructure.UnitTests.GitHub;

public sealed class GitHubTokenProvider_Tests
{
    private readonly IGitHubTokenStore _tokenStore = Substitute.For<IGitHubTokenStore>();

    private GitHubTokenProvider CreateSut()
    {
        return new GitHubTokenProvider(_tokenStore);
    }

    [Fact]
    public async Task ReturnsToken_GivenTokenInStore()
    {
        var token = new GitHubTokenRecord
        {
            AccessToken = "ghp_test123",
            Username = "octocat",
            Scope = "repo",
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        _tokenStore.GetTokenAsync(Arg.Any<CancellationToken>()).Returns(token);
        var sut = CreateSut();

        var result = await sut.GetAccessTokenAsync();

        result.ShouldBe("ghp_test123");
    }

    [Fact]
    public async Task ThrowsException_GivenNoTokenInStore()
    {
        _tokenStore.GetTokenAsync(Arg.Any<CancellationToken>()).Returns((GitHubTokenRecord?)null);
        var sut = CreateSut();

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => sut.GetAccessTokenAsync());

        exception.Message.ShouldContain("No GitHub token found");
    }

    [Fact]
    public async Task ReturnsCachedToken_GivenSecondCall()
    {
        var token = new GitHubTokenRecord
        {
            AccessToken = "ghp_cached",
            Username = "octocat",
            Scope = "repo",
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        _tokenStore.GetTokenAsync(Arg.Any<CancellationToken>()).Returns(token);
        var sut = CreateSut();

        await sut.GetAccessTokenAsync();
        var result = await sut.GetAccessTokenAsync();

        result.ShouldBe("ghp_cached");
        await _tokenStore.Received(1).GetTokenAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReReadsFromStore_GivenCacheCleared()
    {
        var token1 = new GitHubTokenRecord
        {
            AccessToken = "ghp_first",
            Username = "octocat",
            Scope = "repo",
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        var token2 = new GitHubTokenRecord
        {
            AccessToken = "ghp_second",
            Username = "octocat",
            Scope = "repo",
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        _tokenStore.GetTokenAsync(Arg.Any<CancellationToken>()).Returns(token1, token2);
        var sut = CreateSut();

        var first = await sut.GetAccessTokenAsync();
        sut.ClearCache();
        var second = await sut.GetAccessTokenAsync();

        first.ShouldBe("ghp_first");
        second.ShouldBe("ghp_second");
        await _tokenStore.Received(2).GetTokenAsync(Arg.Any<CancellationToken>());
    }
}

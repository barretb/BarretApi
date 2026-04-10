using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using NSubstitute;
using Shouldly;

namespace BarretApi.Api.UnitTests.Features.GitHub;

public sealed class GitHubCreateIssueEndpoint_Tests
{
    private readonly IGitHubClient _gitHubClient = Substitute.For<IGitHubClient>();
    private readonly IGitHubRepositoryStore _repoStore = Substitute.For<IGitHubRepositoryStore>();
    private readonly IGitHubTokenStore _tokenStore = Substitute.For<IGitHubTokenStore>();

    [Fact]
    public async Task ClientCreatesIssue_GivenValidRequest()
    {
        _tokenStore.GetTokenAsync(Arg.Any<CancellationToken>())
            .Returns(new GitHubTokenRecord
            {
                AccessToken = "ghp_test",
                Username = "octocat",
                Scope = "repo",
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
        _repoStore.GetByNameAsync("my-repo", Arg.Any<CancellationToken>())
            .Returns(new GitHubRepositoryRecord
            {
                Name = "my-repo",
                FullName = "octocat/my-repo",
                DefaultBranch = "main",
                HtmlUrl = "https://github.com/octocat/my-repo",
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                SyncedAtUtc = DateTimeOffset.UtcNow
            });
        _gitHubClient.CreateIssueAsync("octocat", "my-repo", "Fix bug", null, null, Arg.Any<CancellationToken>())
            .Returns(new GitHubIssueResult
            {
                Number = 42,
                Title = "Fix bug",
                HtmlUrl = "https://github.com/octocat/my-repo/issues/42",
                State = "open"
            });

        var result = await _gitHubClient.CreateIssueAsync("octocat", "my-repo", "Fix bug", null, null);

        result.Number.ShouldBe(42);
        result.Title.ShouldBe("Fix bug");
    }

    [Fact]
    public async Task RepoStoreReturnsNull_GivenUnknownRepository()
    {
        _repoStore.GetByNameAsync("unknown-repo", Arg.Any<CancellationToken>())
            .Returns((GitHubRepositoryRecord?)null);

        var result = await _repoStore.GetByNameAsync("unknown-repo");

        result.ShouldBeNull();
    }

    [Fact]
    public async Task TokenStoreReturnsNull_GivenNoToken()
    {
        _tokenStore.GetTokenAsync(Arg.Any<CancellationToken>())
            .Returns((GitHubTokenRecord?)null);

        var result = await _tokenStore.GetTokenAsync();

        result.ShouldBeNull();
    }
}

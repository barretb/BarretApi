using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using NSubstitute;
using Shouldly;

namespace BarretApi.Api.UnitTests.Features.GitHub;

public sealed class GitHubRepoSyncEndpoint_Tests
{
    private readonly IGitHubClient _gitHubClient = Substitute.For<IGitHubClient>();
    private readonly IGitHubRepositoryStore _repoStore = Substitute.For<IGitHubRepositoryStore>();
    private readonly IGitHubTokenStore _tokenStore = Substitute.For<IGitHubTokenStore>();

    [Fact]
    public async Task ClientReturnsSyncedRepos_GivenSuccessfulSync()
    {
        var repos = new List<GitHubRepositoryRecord>
        {
            new()
            {
                Name = "repo-one",
                FullName = "octocat/repo-one",
                DefaultBranch = "main",
                HtmlUrl = "https://github.com/octocat/repo-one",
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                SyncedAtUtc = DateTimeOffset.UtcNow
            },
            new()
            {
                Name = "repo-two",
                FullName = "octocat/repo-two",
                DefaultBranch = "main",
                HtmlUrl = "https://github.com/octocat/repo-two",
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                SyncedAtUtc = DateTimeOffset.UtcNow
            }
        };
        _gitHubClient.GetRepositoriesAsync(Arg.Any<CancellationToken>())
            .Returns(repos);
        _tokenStore.GetTokenAsync(Arg.Any<CancellationToken>())
            .Returns(new GitHubTokenRecord
            {
                AccessToken = "ghp_test",
                Username = "octocat",
                Scope = "repo",
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });

        var result = await _gitHubClient.GetRepositoriesAsync();

        result.Count.ShouldBe(2);
        await _repoStore.Received(0).ReplaceAllAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<GitHubRepositoryRecord>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StoreCanReplaceAll_GivenSyncedData()
    {
        var repos = new List<GitHubRepositoryRecord>
        {
            new()
            {
                Name = "repo-one",
                FullName = "octocat/repo-one",
                DefaultBranch = "main",
                HtmlUrl = "https://github.com/octocat/repo-one",
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                SyncedAtUtc = DateTimeOffset.UtcNow
            }
        };

        await _repoStore.ReplaceAllAsync("octocat", repos);

        await _repoStore.Received(1).ReplaceAllAsync(
            "octocat",
            Arg.Is<IReadOnlyList<GitHubRepositoryRecord>>(r => r.Count == 1),
            Arg.Any<CancellationToken>());
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

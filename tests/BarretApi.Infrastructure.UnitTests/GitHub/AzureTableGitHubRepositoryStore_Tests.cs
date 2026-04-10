using Azure;
using Azure.Data.Tables;
using BarretApi.Core.Models;
using BarretApi.Infrastructure.GitHub;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace BarretApi.Infrastructure.UnitTests.GitHub;

public sealed class AzureTableGitHubRepositoryStore_Tests
{
    private readonly TableClient _tableClient = Substitute.For<TableClient>();
    private readonly ILogger<AzureTableGitHubRepositoryStore> _logger =
        Substitute.For<ILogger<AzureTableGitHubRepositoryStore>>();

    private AzureTableGitHubRepositoryStore CreateSut()
    {
        return new AzureTableGitHubRepositoryStore(_tableClient, _logger);
    }

    [Fact]
    public async Task ReplacesAllRepositories_GivenNewList()
    {
        var repos = new List<GitHubRepositoryRecord>
        {
            new()
            {
                Name = "repo-one",
                FullName = "octocat/repo-one",
                Description = "First repo",
                IsPrivate = false,
                DefaultBranch = "main",
                HtmlUrl = "https://github.com/octocat/repo-one",
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                SyncedAtUtc = DateTimeOffset.UtcNow
            }
        };

        var emptyPageable = Substitute.For<AsyncPageable<TableEntity>>();
        emptyPageable.GetAsyncEnumerator(Arg.Any<CancellationToken>())
            .Returns(AsyncEnumeratorOf<TableEntity>());
        _tableClient.QueryAsync<TableEntity>(
            Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(emptyPageable);

        var sut = CreateSut();

        await sut.ReplaceAllAsync("octocat", repos);

        await _tableClient.Received().SubmitTransactionAsync(
            Arg.Any<IEnumerable<TableTransactionAction>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReturnsAllRepositories_GivenRepositoriesExist()
    {
        var entities = new List<TableEntity>
        {
            CreateRepoEntity("repo-one", "octocat/repo-one", false),
            CreateRepoEntity("repo-two", "octocat/repo-two", true)
        };

        var pageable = Substitute.For<AsyncPageable<TableEntity>>();
        pageable.GetAsyncEnumerator(Arg.Any<CancellationToken>())
            .Returns(AsyncEnumeratorOf(entities.ToArray()));
        _tableClient.QueryAsync<TableEntity>(
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(pageable);

        var sut = CreateSut();

        var result = await sut.GetAllAsync();

        result.Count.ShouldBe(2);
        result[0].Name.ShouldBe("repo-one");
        result[1].Name.ShouldBe("repo-two");
        result[1].IsPrivate.ShouldBeTrue();
    }

    [Fact]
    public async Task ReturnsRepo_GivenRepositoryExists()
    {
        var entity = CreateRepoEntity("my-repo", "octocat/my-repo", false);
        var pageable = Substitute.For<AsyncPageable<TableEntity>>();
        pageable.GetAsyncEnumerator(Arg.Any<CancellationToken>())
            .Returns(AsyncEnumeratorOf(entity));
        _tableClient.QueryAsync<TableEntity>(
            Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(pageable);

        var sut = CreateSut();

        var result = await sut.GetByNameAsync("my-repo");

        result.ShouldNotBeNull();
        result.Name.ShouldBe("my-repo");
        result.FullName.ShouldBe("octocat/my-repo");
    }

    [Fact]
    public async Task ReturnsNull_GivenRepositoryNotFound()
    {
        var emptyPageable = Substitute.For<AsyncPageable<TableEntity>>();
        emptyPageable.GetAsyncEnumerator(Arg.Any<CancellationToken>())
            .Returns(AsyncEnumeratorOf<TableEntity>());
        _tableClient.QueryAsync<TableEntity>(
            Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(emptyPageable);

        var sut = CreateSut();

        var result = await sut.GetByNameAsync("nonexistent-repo");

        result.ShouldBeNull();
    }

    private static TableEntity CreateRepoEntity(string name, string fullName, bool isPrivate)
    {
        return new TableEntity("repos", name)
        {
            ["Name"] = name,
            ["FullName"] = fullName,
            ["Description"] = $"Description for {name}",
            ["IsPrivate"] = isPrivate,
            ["DefaultBranch"] = "main",
            ["HtmlUrl"] = $"https://github.com/{fullName}",
            ["UpdatedAtUtc"] = DateTimeOffset.UtcNow,
            ["SyncedAtUtc"] = DateTimeOffset.UtcNow
        };
    }

    private static async IAsyncEnumerator<T> AsyncEnumeratorOf<T>(params T[] items)
    {
        foreach (var item in items)
        {
            yield return item;
        }

        await Task.CompletedTask;
    }
}

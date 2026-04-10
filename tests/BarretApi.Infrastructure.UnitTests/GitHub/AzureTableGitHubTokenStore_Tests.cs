using Azure;
using Azure.Data.Tables;
using BarretApi.Core.Configuration;
using BarretApi.Infrastructure.GitHub;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace BarretApi.Infrastructure.UnitTests.GitHub;

public sealed class AzureTableGitHubTokenStore_Tests
{
    private readonly TableClient _tableClient = Substitute.For<TableClient>();
    private readonly ILogger<AzureTableGitHubTokenStore> _logger =
        Substitute.For<ILogger<AzureTableGitHubTokenStore>>();

    private AzureTableGitHubTokenStore CreateSut()
    {
        return new AzureTableGitHubTokenStore(_tableClient, _logger);
    }

    [Fact]
    public async Task SavesToken_GivenValidTokenRecord()
    {
        var token = new Core.Models.GitHubTokenRecord
        {
            AccessToken = "ghp_abc123",
            Username = "octocat",
            Scope = "repo",
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        var sut = CreateSut();

        await sut.SaveTokenAsync(token);

        await _tableClient.Received(1).UpsertEntityAsync(
            Arg.Is<TableEntity>(e =>
                e.PartitionKey == "github-tokens" &&
                e.RowKey == "current" &&
                e.GetString("AccessToken") == "ghp_abc123" &&
                e.GetString("Username") == "octocat"),
            TableUpdateMode.Replace,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReturnsToken_GivenTokenExists()
    {
        var entity = new TableEntity("github-tokens", "current")
        {
            ["AccessToken"] = "ghp_existing",
            ["Username"] = "octocat",
            ["Scope"] = "repo",
            ["UpdatedAtUtc"] = DateTimeOffset.Parse("2026-03-25T10:00:00Z")
        };
        _tableClient.GetEntityAsync<TableEntity>(
            "github-tokens", "current", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(Response.FromValue(entity, Substitute.For<Response>()));
        var sut = CreateSut();

        var result = await sut.GetTokenAsync();

        result.ShouldNotBeNull();
        result.AccessToken.ShouldBe("ghp_existing");
        result.Username.ShouldBe("octocat");
        result.Scope.ShouldBe("repo");
    }

    [Fact]
    public async Task ReturnsNull_GivenNoTokenExists()
    {
        _tableClient.GetEntityAsync<TableEntity>(
            "github-tokens", "current", cancellationToken: Arg.Any<CancellationToken>())
            .ThrowsAsync(new RequestFailedException(404, "Not Found"));
        var sut = CreateSut();

        var result = await sut.GetTokenAsync();

        result.ShouldBeNull();
    }
}

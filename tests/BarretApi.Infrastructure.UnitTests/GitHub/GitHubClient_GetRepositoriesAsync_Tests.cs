using System.Net;
using System.Text.Json;
using BarretApi.Core.Configuration;
using BarretApi.Infrastructure.GitHub;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace BarretApi.Infrastructure.UnitTests.GitHub;

public sealed class GitHubClient_GetRepositoriesAsync_Tests
{
    private readonly GitHubTokenProvider _tokenProvider;
    private readonly IOptions<GitHubOptions> _options;
    private readonly ILogger<GitHubClient> _logger = Substitute.For<ILogger<GitHubClient>>();

    public GitHubClient_GetRepositoriesAsync_Tests()
    {
        var tokenStore = Substitute.For<Core.Interfaces.IGitHubTokenStore>();
        tokenStore.GetTokenAsync(Arg.Any<CancellationToken>())
            .Returns(new Core.Models.GitHubTokenRecord
            {
                AccessToken = "ghp_test_token",
                Username = "octocat",
                Scope = "repo",
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
        _tokenProvider = new GitHubTokenProvider(tokenStore);
        _options = Options.Create(new GitHubOptions
        {
            ClientId = "test-client-id",
            ClientSecret = "test-secret",
            ApiBaseUrl = "https://api.github.com",
            OAuthBaseUrl = "https://github.com"
        });
    }

    [Fact]
    public async Task ReturnsRepositories_GivenSinglePage()
    {
        var repos = new[]
        {
            new
            {
                name = "my-repo",
                full_name = "octocat/my-repo",
                description = "A test repo",
                @private = false,
                default_branch = "main",
                html_url = "https://github.com/octocat/my-repo",
                updated_at = DateTimeOffset.UtcNow
            }
        };
        var handler = new FakeHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(repos))
            });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com") };
        var sut = new GitHubClient(httpClient, _options, _tokenProvider, _logger);

        var result = await sut.GetRepositoriesAsync();

        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("my-repo");
        result[0].FullName.ShouldBe("octocat/my-repo");
        result[0].IsPrivate.ShouldBeFalse();
    }

    [Fact]
    public async Task ReturnsAllRepositories_GivenMultiplePages()
    {
        var page1Json = JsonSerializer.Serialize(new[]
        {
            new
            {
                name = "repo-1",
                full_name = "octocat/repo-1",
                default_branch = "main",
                html_url = "https://github.com/octocat/repo-1",
                updated_at = DateTimeOffset.UtcNow
            }
        });
        var page2Json = JsonSerializer.Serialize(new[]
        {
            new
            {
                name = "repo-2",
                full_name = "octocat/repo-2",
                default_branch = "main",
                html_url = "https://github.com/octocat/repo-2",
                updated_at = DateTimeOffset.UtcNow
            }
        });

        var response1 = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(page1Json)
        };
        response1.Headers.Add("Link", "<https://api.github.com/user/repos?page=2>; rel=\"next\"");

        var response2 = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(page2Json)
        };

        var handler = new FakeHttpMessageHandler(response1, response2);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com") };
        var sut = new GitHubClient(httpClient, _options, _tokenProvider, _logger);

        var result = await sut.GetRepositoriesAsync();

        result.Count.ShouldBe(2);
        result[0].Name.ShouldBe("repo-1");
        result[1].Name.ShouldBe("repo-2");
    }

    [Fact]
    public async Task ThrowsException_GivenUnauthorizedResponse()
    {
        var handler = new FakeHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com") };
        var sut = new GitHubClient(httpClient, _options, _tokenProvider, _logger);

        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => sut.GetRepositoriesAsync());

        ex.Message.ShouldContain("reauthenticate");
    }

    [Fact]
    public async Task ThrowsException_GivenRateLimitResponse()
    {
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        response.Headers.Add("X-RateLimit-Reset", DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds().ToString());

        var handler = new FakeHttpMessageHandler(response);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com") };
        var sut = new GitHubClient(httpClient, _options, _tokenProvider, _logger);

        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => sut.GetRepositoriesAsync());

        ex.Message.ShouldContain("rate limit");
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;

        public FakeHttpMessageHandler(params HttpResponseMessage[] responses)
        {
            _responses = new Queue<HttpResponseMessage>(responses);
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_responses.Dequeue());
        }
    }
}

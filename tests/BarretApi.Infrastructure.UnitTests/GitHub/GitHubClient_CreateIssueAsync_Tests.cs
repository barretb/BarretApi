using System.Net;
using System.Text.Json;
using BarretApi.Core.Configuration;
using BarretApi.Infrastructure.GitHub;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace BarretApi.Infrastructure.UnitTests.GitHub;

public sealed class GitHubClient_CreateIssueAsync_Tests
{
    private readonly GitHubTokenProvider _tokenProvider;
    private readonly IOptions<GitHubOptions> _options;
    private readonly ILogger<GitHubClient> _logger = Substitute.For<ILogger<GitHubClient>>();

    public GitHubClient_CreateIssueAsync_Tests()
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
    public async Task ReturnsIssueResult_GivenValidRequest()
    {
        var issueResponse = new
        {
            number = 42,
            title = "Fix login bug",
            html_url = "https://github.com/octocat/my-repo/issues/42",
            state = "open"
        };
        var handler = new FakeHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent(JsonSerializer.Serialize(issueResponse))
            });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com") };
        var sut = new GitHubClient(httpClient, _options, _tokenProvider, _logger);

        var result = await sut.CreateIssueAsync("octocat", "my-repo", "Fix login bug", null, null);

        result.Number.ShouldBe(42);
        result.Title.ShouldBe("Fix login bug");
        result.HtmlUrl.ShouldBe("https://github.com/octocat/my-repo/issues/42");
        result.State.ShouldBe("open");
    }

    [Fact]
    public async Task SendsAllFields_GivenBodyAndLabels()
    {
        var issueResponse = new
        {
            number = 7,
            title = "Add dark mode",
            html_url = "https://github.com/octocat/my-repo/issues/7",
            state = "open"
        };
        var handler = new FakeHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent(JsonSerializer.Serialize(issueResponse))
            });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com") };
        var sut = new GitHubClient(httpClient, _options, _tokenProvider, _logger);

        var result = await sut.CreateIssueAsync("octocat", "my-repo", "Add dark mode",
            "## Description\nPlease add dark mode.", new[] { "enhancement", "ui" });

        result.Number.ShouldBe(7);
        result.Title.ShouldBe("Add dark mode");

        var sentBody = handler.LastRequestBody;
        sentBody.ShouldNotBeNull();
        sentBody.ShouldContain("body");
        sentBody.ShouldContain("labels");
    }

    [Fact]
    public async Task ThrowsUnauthorized_GivenRevokedToken()
    {
        var handler = new FakeHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com") };
        var sut = new GitHubClient(httpClient, _options, _tokenProvider, _logger);

        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => sut.CreateIssueAsync("octocat", "my-repo", "Test", null, null));

        ex.Message.ShouldContain("reauthenticate");
    }

    [Fact]
    public async Task ThrowsApiError_GivenValidationFailed()
    {
        var handler = new FakeHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
            {
                Content = new StringContent("{\"message\":\"Issues are disabled for this repository\"}")
            });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com") };
        var sut = new GitHubClient(httpClient, _options, _tokenProvider, _logger);

        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => sut.CreateIssueAsync("octocat", "my-repo", "Test", null, null));

        ex.Message.ShouldContain("validation error");
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;

        public string? LastRequestBody { get; private set; }

        public FakeHttpMessageHandler(params HttpResponseMessage[] responses)
        {
            _responses = new Queue<HttpResponseMessage>(responses);
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.Content is not null)
            {
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }
            return _responses.Dequeue();
        }
    }
}

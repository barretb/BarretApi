using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Infrastructure.GitHub.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BarretApi.Infrastructure.GitHub;

public sealed partial class GitHubClient(
    HttpClient httpClient,
    IOptions<GitHubOptions> options,
    GitHubTokenProvider tokenProvider,
    ILogger<GitHubClient> logger)
    : IGitHubClient
{
    private readonly GitHubOptions _options = options.Value;

    public async Task<GitHubTokenRecord> ExchangeCodeForTokenAsync(
        string code,
        string redirectUri,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Exchanging GitHub authorization code for access token");

        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post,
            $"{_options.OAuthBaseUrl}/login/oauth/access_token");
        tokenRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        tokenRequest.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["code"] = code,
            ["redirect_uri"] = redirectUri
        });

        var tokenResponse = await httpClient.SendAsync(tokenRequest, cancellationToken);
        tokenResponse.EnsureSuccessStatusCode();

        var tokenResult = await tokenResponse.Content.ReadFromJsonAsync<GitHubTokenResponse>(cancellationToken)
            ?? throw new InvalidOperationException("GitHub token exchange returned an unreadable response.");

        if (string.IsNullOrWhiteSpace(tokenResult.AccessToken))
        {
            throw new InvalidOperationException("GitHub token exchange did not return an access token.");
        }

        using var userRequest = new HttpRequestMessage(HttpMethod.Get,
            $"{_options.ApiBaseUrl}/user");
        userRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenResult.AccessToken);
        userRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        userRequest.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        userRequest.Headers.UserAgent.Add(new ProductInfoHeaderValue("BarretApi", "1.0"));

        var userResponse = await httpClient.SendAsync(userRequest, cancellationToken);
        userResponse.EnsureSuccessStatusCode();

        var userResult = await userResponse.Content.ReadFromJsonAsync<GitHubUserResponse>(cancellationToken)
            ?? throw new InvalidOperationException("GitHub user endpoint returned an unreadable response.");

        logger.LogInformation("GitHub OAuth completed for user {Username}", userResult.Login);

        return new GitHubTokenRecord
        {
            AccessToken = tokenResult.AccessToken,
            Username = userResult.Login,
            Scope = tokenResult.Scope,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    public async Task<IReadOnlyList<GitHubRepositoryRecord>> GetRepositoriesAsync(
        CancellationToken cancellationToken = default)
    {
        var accessToken = await tokenProvider.GetAccessTokenAsync(cancellationToken);
        var allRepos = new List<GitHubRepositoryRecord>();
        var page = 1;
        var syncedAt = DateTimeOffset.UtcNow;

        logger.LogInformation("Starting GitHub repository sync");

        while (true)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"{_options.ApiBaseUrl}/user/repos?type=owner&per_page=100&page={page}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("BarretApi", "1.0"));

            var response = await httpClient.SendAsync(request, cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                tokenProvider.ClearCache();
                throw new InvalidOperationException(
                    "GitHub token is no longer valid. Please reauthenticate via /api/github/auth.");
            }

            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                var resetHeader = response.Headers.TryGetValues("X-RateLimit-Reset", out var values)
                    ? values.FirstOrDefault() : null;
                var resetTime = resetHeader is not null
                    ? DateTimeOffset.FromUnixTimeSeconds(long.Parse(resetHeader))
                    : DateTimeOffset.UtcNow.AddMinutes(60);
                throw new InvalidOperationException(
                    $"GitHub API rate limit exceeded. Resets at {resetTime:O}.");
            }

            response.EnsureSuccessStatusCode();

            var repos = await response.Content.ReadFromJsonAsync<List<GitHubRepoResponse>>(cancellationToken)
                ?? [];

            foreach (var repo in repos)
            {
                allRepos.Add(new GitHubRepositoryRecord
                {
                    Name = repo.Name,
                    FullName = repo.FullName,
                    Description = repo.Description,
                    IsPrivate = repo.IsPrivate,
                    DefaultBranch = repo.DefaultBranch,
                    HtmlUrl = repo.HtmlUrl,
                    UpdatedAtUtc = repo.UpdatedAt,
                    SyncedAtUtc = syncedAt
                });
            }

            logger.LogInformation("Fetched page {Page} with {Count} repositories", page, repos.Count);

            if (!HasNextPage(response))
            {
                break;
            }

            page++;
        }

        logger.LogInformation("GitHub repository sync complete: {TotalCount} repositories", allRepos.Count);
        return allRepos;
    }

    public async Task<GitHubIssueResult> CreateIssueAsync(
        string owner,
        string repo,
        string title,
        string? body,
        IReadOnlyList<string>? labels,
        CancellationToken cancellationToken = default)
    {
        var accessToken = await tokenProvider.GetAccessTokenAsync(cancellationToken);

        logger.LogInformation("Creating GitHub issue on {Owner}/{Repo}", owner, repo);

        var issueBody = new Dictionary<string, object> { ["title"] = title };

        if (!string.IsNullOrWhiteSpace(body))
        {
            issueBody["body"] = body;
        }

        if (labels is { Count: > 0 })
        {
            issueBody["labels"] = labels;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"{_options.ApiBaseUrl}/repos/{owner}/{repo}/issues");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("BarretApi", "1.0"));
        request.Content = JsonContent.Create(issueBody);

        var response = await httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            tokenProvider.ClearCache();
            throw new InvalidOperationException(
                "GitHub token is no longer valid. Please reauthenticate via /api/github/auth.");
        }

        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            var resetHeader = response.Headers.TryGetValues("X-RateLimit-Reset", out var values)
                ? values.FirstOrDefault() : null;
            var resetTime = resetHeader is not null
                ? DateTimeOffset.FromUnixTimeSeconds(long.Parse(resetHeader))
                : DateTimeOffset.UtcNow.AddMinutes(60);
            throw new InvalidOperationException(
                $"GitHub API rate limit exceeded. Resets at {resetTime:O}.");
        }

        if (response.StatusCode == System.Net.HttpStatusCode.UnprocessableEntity)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"GitHub API validation error: {errorBody}");
        }

        response.EnsureSuccessStatusCode();

        var issueResult = await response.Content.ReadFromJsonAsync<GitHubIssueResponse>(cancellationToken)
            ?? throw new InvalidOperationException("GitHub issue creation returned an unreadable response.");

        logger.LogInformation("Created GitHub issue #{Number} on {Owner}/{Repo}", issueResult.Number, owner, repo);

        return new GitHubIssueResult
        {
            Number = issueResult.Number,
            Title = issueResult.Title,
            HtmlUrl = issueResult.HtmlUrl,
            State = issueResult.State
        };
    }

    private static bool HasNextPage(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Link", out var linkValues))
        {
            return false;
        }

        var linkHeader = string.Join(",", linkValues);
        return linkHeader.Contains("rel=\"next\"", StringComparison.OrdinalIgnoreCase);
    }
}

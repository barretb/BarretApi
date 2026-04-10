using Azure;
using Azure.Data.Tables;
using Azure.Identity;
using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BarretApi.Infrastructure.GitHub;

public sealed class AzureTableGitHubTokenStore : IGitHubTokenStore
{
    private const string PartitionKey = "github-tokens";
    private const string RowKey = "current";

    private readonly TableClient _tableClient;
    private readonly ILogger<AzureTableGitHubTokenStore> _logger;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _initialized;

    public AzureTableGitHubTokenStore(
        IOptions<GitHubOptions> options,
        ILogger<AzureTableGitHubTokenStore> logger)
    {
        _logger = logger;
        var opts = options.Value;

        if (string.IsNullOrWhiteSpace(opts.TokenStorage.TableName))
        {
            throw new InvalidOperationException("GitHub:TokenStorage:TableName is required.");
        }

        if (!string.IsNullOrWhiteSpace(opts.TokenStorage.ConnectionString))
        {
            _tableClient = new TableClient(
                opts.TokenStorage.ConnectionString,
                opts.TokenStorage.TableName);
        }
        else if (!string.IsNullOrWhiteSpace(opts.TokenStorage.AccountEndpoint))
        {
            _tableClient = new TableClient(
                new Uri(opts.TokenStorage.AccountEndpoint),
                opts.TokenStorage.TableName,
                new DefaultAzureCredential());
        }
        else
        {
            throw new InvalidOperationException(
                "GitHub:TokenStorage requires either ConnectionString or AccountEndpoint.");
        }
    }

    internal AzureTableGitHubTokenStore(
        TableClient tableClient,
        ILogger<AzureTableGitHubTokenStore> logger)
    {
        _tableClient = tableClient;
        _logger = logger;
        _initialized = true;
    }

    public async Task<GitHubTokenRecord?> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        try
        {
            var response = await _tableClient.GetEntityAsync<TableEntity>(
                PartitionKey,
                RowKey,
                cancellationToken: cancellationToken);

            var entity = response.Value;
            var accessToken = entity.GetString("AccessToken");

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                return null;
            }

            return new GitHubTokenRecord
            {
                AccessToken = accessToken,
                Username = entity.GetString("Username") ?? string.Empty,
                Scope = entity.GetString("Scope") ?? string.Empty,
                UpdatedAtUtc = entity.GetDateTimeOffset("UpdatedAtUtc") ?? DateTimeOffset.MinValue
            };
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task SaveTokenAsync(GitHubTokenRecord token, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);
        await EnsureInitializedAsync(cancellationToken);

        var entity = new TableEntity(PartitionKey, RowKey)
        {
            ["AccessToken"] = token.AccessToken,
            ["Username"] = token.Username,
            ["Scope"] = token.Scope,
            ["UpdatedAtUtc"] = token.UpdatedAtUtc
        };

        await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
        _logger.LogInformation("GitHub token saved to table storage");
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        await _initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            await _tableClient.CreateIfNotExistsAsync(cancellationToken);
            _initialized = true;
            _logger.LogInformation("Ensured GitHub token table exists");
        }
        finally
        {
            _initializationLock.Release();
        }
    }
}

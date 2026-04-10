using Azure.Data.Tables;
using Azure.Identity;
using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BarretApi.Infrastructure.GitHub;

public sealed class AzureTableGitHubRepositoryStore : IGitHubRepositoryStore
{
    private const string PartitionKey = "repos";
    private const int BatchSize = 100;

    private readonly TableClient _tableClient;
    private readonly ILogger<AzureTableGitHubRepositoryStore> _logger;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _initialized;

    public AzureTableGitHubRepositoryStore(
        IOptions<GitHubOptions> options,
        ILogger<AzureTableGitHubRepositoryStore> logger)
    {
        _logger = logger;
        var opts = options.Value;

        if (string.IsNullOrWhiteSpace(opts.RepoStorage.TableName))
        {
            throw new InvalidOperationException("GitHub:RepoStorage:TableName is required.");
        }

        if (!string.IsNullOrWhiteSpace(opts.RepoStorage.ConnectionString))
        {
            _tableClient = new TableClient(
                opts.RepoStorage.ConnectionString,
                opts.RepoStorage.TableName);
        }
        else if (!string.IsNullOrWhiteSpace(opts.RepoStorage.AccountEndpoint))
        {
            _tableClient = new TableClient(
                new Uri(opts.RepoStorage.AccountEndpoint),
                opts.RepoStorage.TableName,
                new DefaultAzureCredential());
        }
        else
        {
            throw new InvalidOperationException(
                "GitHub:RepoStorage requires either ConnectionString or AccountEndpoint.");
        }
    }

    internal AzureTableGitHubRepositoryStore(
        TableClient tableClient,
        ILogger<AzureTableGitHubRepositoryStore> logger)
    {
        _tableClient = tableClient;
        _logger = logger;
        _initialized = true;
    }

    public async Task<IReadOnlyList<GitHubRepositoryRecord>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var results = new List<GitHubRepositoryRecord>();
        await foreach (var entity in _tableClient.QueryAsync<TableEntity>(
            cancellationToken: cancellationToken))
        {
            results.Add(MapToRecord(entity));
        }

        return results;
    }

    public async Task<GitHubRepositoryRecord?> GetByNameAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        await foreach (var entity in _tableClient.QueryAsync<TableEntity>(
            $"RowKey eq '{name}'",
            cancellationToken: cancellationToken))
        {
            return MapToRecord(entity);
        }

        return null;
    }

    public async Task ReplaceAllAsync(
        string username,
        IReadOnlyList<GitHubRepositoryRecord> repositories,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var deleteActions = new List<TableTransactionAction>();
        await foreach (var entity in _tableClient.QueryAsync<TableEntity>(
            $"PartitionKey eq '{PartitionKey}'",
            cancellationToken: cancellationToken))
        {
            deleteActions.Add(new TableTransactionAction(TableTransactionActionType.Delete, entity));
        }

        if (deleteActions.Count > 0)
        {
            foreach (var batch in ChunkBy(deleteActions, BatchSize))
            {
                await _tableClient.SubmitTransactionAsync(batch, cancellationToken);
            }
        }

        var insertActions = repositories
            .Select(repo => new TableTransactionAction(
                TableTransactionActionType.Add,
                MapToEntity(repo)))
            .ToList();

        if (insertActions.Count > 0)
        {
            foreach (var batch in ChunkBy(insertActions, BatchSize))
            {
                await _tableClient.SubmitTransactionAsync(batch, cancellationToken);
            }
        }

        _logger.LogInformation(
            "Replaced all repositories for {Username}: {Count} repos stored",
            username,
            repositories.Count);
    }

    private static GitHubRepositoryRecord MapToRecord(TableEntity entity)
    {
        return new GitHubRepositoryRecord
        {
            Name = entity.GetString("Name") ?? entity.RowKey,
            FullName = entity.GetString("FullName") ?? string.Empty,
            Description = entity.GetString("Description"),
            IsPrivate = entity.GetBoolean("IsPrivate") ?? false,
            DefaultBranch = entity.GetString("DefaultBranch") ?? string.Empty,
            HtmlUrl = entity.GetString("HtmlUrl") ?? string.Empty,
            UpdatedAtUtc = entity.GetDateTimeOffset("UpdatedAtUtc") ?? DateTimeOffset.MinValue,
            SyncedAtUtc = entity.GetDateTimeOffset("SyncedAtUtc") ?? DateTimeOffset.MinValue
        };
    }

    private static TableEntity MapToEntity(GitHubRepositoryRecord repo)
    {
        return new TableEntity(PartitionKey, repo.Name)
        {
            ["Name"] = repo.Name,
            ["FullName"] = repo.FullName,
            ["Description"] = repo.Description,
            ["IsPrivate"] = repo.IsPrivate,
            ["DefaultBranch"] = repo.DefaultBranch,
            ["HtmlUrl"] = repo.HtmlUrl,
            ["UpdatedAtUtc"] = repo.UpdatedAtUtc,
            ["SyncedAtUtc"] = repo.SyncedAtUtc
        };
    }

    private static IEnumerable<IEnumerable<T>> ChunkBy<T>(IList<T> source, int size)
    {
        for (var i = 0; i < source.Count; i += size)
        {
            yield return source.Skip(i).Take(size);
        }
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
            _logger.LogInformation("Ensured GitHub repository table exists");
        }
        finally
        {
            _initializationLock.Release();
        }
    }
}

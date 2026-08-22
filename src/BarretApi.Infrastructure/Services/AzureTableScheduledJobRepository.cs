using Azure;
using Azure.Data.Tables;
using Azure.Identity;
using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BarretApi.Infrastructure.Services;

public sealed class AzureTableScheduledJobRepository : IScheduledJobRepository
{
    private readonly TableClient _tableClient;
    private readonly JobSchedulerOptions _options;
    private readonly ILogger<AzureTableScheduledJobRepository> _logger;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _initialized;

    public AzureTableScheduledJobRepository(
        IOptions<JobSchedulerOptions> options,
        ILogger<AzureTableScheduledJobRepository> logger)
    {
        _options = options.Value;
        _logger = logger;
        _options.ThrowIfInvalid();

        var tableName = _options.TableStorage.JobsTableName.Trim().ToLowerInvariant();

        _tableClient = !string.IsNullOrWhiteSpace(_options.TableStorage.ConnectionString)
            ? new TableClient(_options.TableStorage.ConnectionString, tableName)
            : new TableClient(
                new Uri(_options.TableStorage.AccountEndpoint),
                tableName,
                new DefaultAzureCredential());
    }

    internal AzureTableScheduledJobRepository(
        TableClient tableClient,
        IOptions<JobSchedulerOptions> options,
        ILogger<AzureTableScheduledJobRepository> logger)
    {
        _tableClient = tableClient;
        _options = options.Value;
        _logger = logger;
        _initialized = true;
    }

    private string PartitionKey => _options.TableStorage.PartitionKey;

    public async Task<IReadOnlyList<ScheduledJobRecord>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var jobs = new List<ScheduledJobRecord>();
        var filter = $"PartitionKey eq '{EscapeODataString(PartitionKey)}'";

        await foreach (var entity in _tableClient.QueryAsync<TableEntity>(
            filter,
            cancellationToken: cancellationToken))
        {
            jobs.Add(MapEntityToModel(entity));
        }

        return [.. jobs.OrderBy(j => j.Name, StringComparer.OrdinalIgnoreCase)];
    }

    public async Task<ScheduledJobRecord?> GetByNameAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        await EnsureInitializedAsync(cancellationToken);

        var response = await _tableClient.GetEntityIfExistsAsync<TableEntity>(
            PartitionKey,
            name.Trim(),
            cancellationToken: cancellationToken);

        return response.HasValue && response.Value is not null
            ? MapEntityToModel(response.Value)
            : null;
    }

    /// <summary>
    /// The partition holds a handful of rows, so it is scanned and filtered in memory
    /// rather than encoding a datetime comparison into an OData filter.
    /// </summary>
    public async Task<IReadOnlyList<ScheduledJobRecord>> GetDueAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        var all = await GetAllAsync(cancellationToken);

        return [.. all
            .Where(j => j.IsEnabled && j.NextRunUtc is not null && j.NextRunUtc.Value <= nowUtc)
            .OrderBy(j => j.NextRunUtc)];
    }

    public async Task CreateAsync(ScheduledJobRecord job, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        await EnsureInitializedAsync(cancellationToken);

        await _tableClient.AddEntityAsync(MapModelToEntity(job), cancellationToken);
    }

    public async Task UpdateAsync(ScheduledJobRecord job, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        await EnsureInitializedAsync(cancellationToken);

        var response = await _tableClient.UpdateEntityAsync(
            MapModelToEntity(job),
            ETag.All,
            TableUpdateMode.Replace,
            cancellationToken);

        job.ETag = response?.Headers.ETag?.ToString() ?? job.ETag;
    }

    public async Task<bool> TryClaimAsync(
        ScheduledJobRecord job,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        await EnsureInitializedAsync(cancellationToken);

        var previousState = job.RunState;
        var previousClaimedAt = job.ClaimedAtUtc;
        var previousUpdatedAt = job.UpdatedAtUtc;

        job.RunState = JobRunState.Running;
        job.ClaimedAtUtc = nowUtc;
        job.UpdatedAtUtc = nowUtc;

        try
        {
            var response = await _tableClient.UpdateEntityAsync(
                MapModelToEntity(job),
                new ETag(job.ETag),
                TableUpdateMode.Replace,
                cancellationToken);

            job.ETag = response?.Headers.ETag?.ToString() ?? job.ETag;
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 412)
        {
            _logger.LogInformation("Claim for job {JobName} was lost to a concurrent writer.", job.Name);
            job.RunState = previousState;
            job.ClaimedAtUtc = previousClaimedAt;
            job.UpdatedAtUtc = previousUpdatedAt;
            return false;
        }
    }

    public async Task DeleteAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        await EnsureInitializedAsync(cancellationToken);

        await _tableClient.DeleteEntityAsync(PartitionKey, name.Trim(), ETag.All, cancellationToken);
    }

    private TableEntity MapModelToEntity(ScheduledJobRecord job)
    {
        return new TableEntity(PartitionKey, job.Name)
        {
            ["DisplayName"] = job.DisplayName,
            ["JobType"] = job.JobType,
            ["CronExpression"] = job.CronExpression,
            ["TimeZoneId"] = job.TimeZoneId,
            ["ArgumentsJson"] = job.ArgumentsJson,
            ["IsEnabled"] = job.IsEnabled,
            ["NextRunUtc"] = job.NextRunUtc,
            ["LastRunUtc"] = job.LastRunUtc,
            ["LastRunStatus"] = job.LastRunStatus?.ToString(),
            ["LastRunError"] = Truncate(job.LastRunError, 4_000),
            ["LastRunDurationMs"] = job.LastRunDurationMs,
            ["ConsecutiveFailureCount"] = job.ConsecutiveFailureCount,
            ["MaxRetryCount"] = job.MaxRetryCount,
            ["RetryBaseDelaySeconds"] = job.RetryBaseDelaySeconds,
            ["RunState"] = job.RunState.ToString(),
            ["ClaimedAtUtc"] = job.ClaimedAtUtc,
            ["CreatedAtUtc"] = job.CreatedAtUtc,
            ["UpdatedAtUtc"] = job.UpdatedAtUtc
        };
    }

    private static ScheduledJobRecord MapEntityToModel(TableEntity entity)
    {
        return new ScheduledJobRecord
        {
            Name = entity.RowKey,
            DisplayName = entity.GetString("DisplayName") ?? entity.RowKey,
            JobType = entity.GetString("JobType") ?? string.Empty,
            CronExpression = entity.GetString("CronExpression") ?? string.Empty,
            TimeZoneId = entity.GetString("TimeZoneId") ?? "UTC",
            ArgumentsJson = entity.GetString("ArgumentsJson"),
            IsEnabled = entity.GetBoolean("IsEnabled") ?? false,
            NextRunUtc = entity.GetDateTimeOffset("NextRunUtc"),
            LastRunUtc = entity.GetDateTimeOffset("LastRunUtc"),
            LastRunStatus = ParseStatus(entity.GetString("LastRunStatus")),
            LastRunError = entity.GetString("LastRunError"),
            LastRunDurationMs = entity.GetInt64("LastRunDurationMs"),
            ConsecutiveFailureCount = entity.GetInt32("ConsecutiveFailureCount") ?? 0,
            MaxRetryCount = entity.GetInt32("MaxRetryCount") ?? 2,
            RetryBaseDelaySeconds = entity.GetInt32("RetryBaseDelaySeconds") ?? 30,
            RunState = Enum.TryParse<JobRunState>(entity.GetString("RunState"), out var runState)
                ? runState
                : JobRunState.Idle,
            ClaimedAtUtc = entity.GetDateTimeOffset("ClaimedAtUtc"),
            CreatedAtUtc = entity.GetDateTimeOffset("CreatedAtUtc") ?? default,
            UpdatedAtUtc = entity.GetDateTimeOffset("UpdatedAtUtc") ?? default,
            ETag = entity.ETag.ToString()
        };
    }

    private static JobRunStatus? ParseStatus(string? value)
        => Enum.TryParse<JobRunStatus>(value, out var status) ? status : null;

    private static string? Truncate(string? value, int maxLength)
        => value is not null && value.Length > maxLength ? value[..maxLength] : value;

    private static string EscapeODataString(string value) => value.Replace("'", "''");

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
        }
        finally
        {
            _initializationLock.Release();
        }
    }
}

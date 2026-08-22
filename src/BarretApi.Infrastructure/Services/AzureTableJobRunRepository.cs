using Azure;
using Azure.Data.Tables;
using Azure.Identity;
using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BarretApi.Infrastructure.Services;

/// <summary>
/// Run history, partitioned by job name. Row keys invert the start time so a plain
/// partition query already reads newest-first.
/// </summary>
public sealed class AzureTableJobRunRepository : IJobRunRepository
{
    private readonly TableClient _tableClient;
    private readonly ILogger<AzureTableJobRunRepository> _logger;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _initialized;

    public AzureTableJobRunRepository(
        IOptions<JobSchedulerOptions> options,
        ILogger<AzureTableJobRunRepository> logger)
    {
        var schedulerOptions = options.Value;
        _logger = logger;
        schedulerOptions.ThrowIfInvalid();

        var tableName = schedulerOptions.TableStorage.RunsTableName.Trim().ToLowerInvariant();

        _tableClient = !string.IsNullOrWhiteSpace(schedulerOptions.TableStorage.ConnectionString)
            ? new TableClient(schedulerOptions.TableStorage.ConnectionString, tableName)
            : new TableClient(
                new Uri(schedulerOptions.TableStorage.AccountEndpoint),
                tableName,
                new DefaultAzureCredential());
    }

    internal AzureTableJobRunRepository(
        TableClient tableClient,
        IOptions<JobSchedulerOptions> options,
        ILogger<AzureTableJobRunRepository> logger)
    {
        _tableClient = tableClient;
        _logger = logger;
        _initialized = true;
    }

    /// <summary>
    /// Descending row key: the further in the future the start time, the smaller the key.
    /// </summary>
    internal static string BuildRowKey(DateTimeOffset startedAtUtc, string runId)
    {
        var inverted = DateTimeOffset.MaxValue.Ticks - startedAtUtc.UtcTicks;
        return $"{inverted:D19}-{runId}";
    }

    public async Task AddAsync(JobRunRecord run, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        await EnsureInitializedAsync(cancellationToken);

        var entity = new TableEntity(run.JobName, BuildRowKey(run.StartedAtUtc, run.RunId))
        {
            ["RunId"] = run.RunId,
            ["JobType"] = run.JobType,
            ["TriggerType"] = run.TriggerType.ToString(),
            ["ScheduledForUtc"] = run.ScheduledForUtc,
            ["StartedAtUtc"] = run.StartedAtUtc,
            ["CompletedAtUtc"] = run.CompletedAtUtc,
            ["DurationMs"] = run.DurationMs,
            ["Status"] = run.Status.ToString(),
            ["AttemptCount"] = run.AttemptCount,
            ["Summary"] = Truncate(run.Summary, 4_000),
            ["ErrorMessage"] = Truncate(run.ErrorMessage, 4_000)
        };

        await _tableClient.AddEntityAsync(entity, cancellationToken);
    }

    public async Task<IReadOnlyList<JobRunRecord>> GetByJobAsync(
        string jobName,
        int maxCount,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobName);
        await EnsureInitializedAsync(cancellationToken);

        var effectiveMax = Math.Clamp(maxCount, 1, 500);
        var filter = $"PartitionKey eq '{EscapeODataString(jobName.Trim())}'";
        var runs = new List<JobRunRecord>();

        await foreach (var entity in _tableClient.QueryAsync<TableEntity>(
            filter,
            cancellationToken: cancellationToken))
        {
            runs.Add(MapEntityToModel(entity, jobName.Trim()));

            if (runs.Count >= effectiveMax)
            {
                break;
            }
        }

        return runs;
    }

    /// <summary>
    /// Scans every partition, so cost grows with total run history across all jobs rather
    /// than any single job's history. Acceptable at the run volumes this scheduler targets;
    /// revisit with a maintained "oldest run per job" index if that stops being true.
    /// </summary>
    public async Task<int> PurgeOlderThanAsync(
        DateTimeOffset cutoffUtc,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var expired = new List<(string PartitionKey, string RowKey)>();

        await foreach (var entity in _tableClient.QueryAsync<TableEntity>(
            filter: (string?)null,
            select: new[] { "PartitionKey", "RowKey", "StartedAtUtc" },
            cancellationToken: cancellationToken))
        {
            var startedAt = entity.GetDateTimeOffset("StartedAtUtc");
            if (startedAt is not null && startedAt.Value < cutoffUtc)
            {
                expired.Add((entity.PartitionKey, entity.RowKey));
            }
        }

        foreach (var (partitionKey, rowKey) in expired)
        {
            await _tableClient.DeleteEntityAsync(partitionKey, rowKey, ETag.All, cancellationToken);
        }

        if (expired.Count > 0)
        {
            _logger.LogInformation(
                "Purged {DeletedCount} job runs started before {Cutoff}.",
                expired.Count,
                cutoffUtc);
        }

        return expired.Count;
    }

    private static JobRunRecord MapEntityToModel(TableEntity entity, string jobName)
    {
        return new JobRunRecord
        {
            RunId = entity.GetString("RunId") ?? entity.RowKey,
            JobName = jobName,
            JobType = entity.GetString("JobType") ?? string.Empty,
            TriggerType = Enum.TryParse<JobTriggerType>(entity.GetString("TriggerType"), out var trigger)
                ? trigger
                : JobTriggerType.Scheduled,
            ScheduledForUtc = entity.GetDateTimeOffset("ScheduledForUtc"),
            StartedAtUtc = entity.GetDateTimeOffset("StartedAtUtc") ?? default,
            CompletedAtUtc = entity.GetDateTimeOffset("CompletedAtUtc"),
            DurationMs = entity.GetInt64("DurationMs") ?? 0,
            Status = Enum.TryParse<JobRunStatus>(entity.GetString("Status"), out var status)
                ? status
                : JobRunStatus.Failed,
            AttemptCount = entity.GetInt32("AttemptCount") ?? 0,
            Summary = entity.GetString("Summary"),
            ErrorMessage = entity.GetString("ErrorMessage")
        };
    }

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

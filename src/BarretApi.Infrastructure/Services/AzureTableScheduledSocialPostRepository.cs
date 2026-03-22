using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using Azure.Identity;
using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BarretApi.Infrastructure.Services;

public sealed class AzureTableScheduledSocialPostRepository : IScheduledSocialPostRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ScheduledSocialPostOptions _options;
    private readonly ILogger<AzureTableScheduledSocialPostRepository> _logger;
    private readonly TableClient _tableClient;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _initialized;

    public AzureTableScheduledSocialPostRepository(
        IOptions<ScheduledSocialPostOptions> scheduledSocialPostOptions,
        ILogger<AzureTableScheduledSocialPostRepository> logger)
    {
        _options = scheduledSocialPostOptions.Value;
        _logger = logger;
        _options.ThrowIfInvalid();

        if (!string.IsNullOrWhiteSpace(_options.TableStorage.ConnectionString))
        {
            _tableClient = new TableClient(
                _options.TableStorage.ConnectionString,
                _options.TableStorage.TableName);
        }
        else
        {
            _tableClient = new TableClient(
                new Uri(_options.TableStorage.AccountEndpoint),
                _options.TableStorage.TableName,
                new DefaultAzureCredential());
        }
    }

    public async Task SaveScheduledAsync(
        ScheduledSocialPostRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        await EnsureInitializedAsync(cancellationToken);

        var entity = MapModelToEntity(record);
        await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
    }

    public async Task<IReadOnlyList<ScheduledSocialPostRecord>> GetDueForProcessingAsync(
        DateTimeOffset asOfUtc,
        int maxCount,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var filter = $"PartitionKey eq '{_options.TableStorage.PartitionKey}'";
        var candidates = new List<ScheduledSocialPostRecord>();

        await foreach (var entity in _tableClient.QueryAsync<TableEntity>(filter, cancellationToken: cancellationToken))
        {
            var record = MapEntityToModel(entity);
            var due = record.ScheduledForUtc <= asOfUtc;
            var retryable = record.Status is ScheduledPostStatus.Pending or ScheduledPostStatus.Failed;
            if (due && retryable)
            {
                candidates.Add(record);
            }
        }

        return candidates
            .OrderBy(r => r.ScheduledForUtc)
            .ThenBy(r => r.CreatedAtUtc)
            .Take(maxCount)
            .ToList();
    }

    public async Task<bool> TryMarkProcessingAsync(
        string scheduledPostId,
        DateTimeOffset attemptedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scheduledPostId);
        await EnsureInitializedAsync(cancellationToken);

        try
        {
            var response = await _tableClient.GetEntityAsync<TableEntity>(
                _options.TableStorage.PartitionKey,
                scheduledPostId,
                cancellationToken: cancellationToken);

            var entity = response.Value;
            var status = ParseStatus(entity.GetString("Status"));
            if (status is ScheduledPostStatus.Published or ScheduledPostStatus.Processing)
            {
                return false;
            }

            var attemptCount = entity.GetInt32("AttemptCount") ?? 0;
            entity["Status"] = ScheduledPostStatus.Processing.ToString();
            entity["LastAttemptedAtUtc"] = attemptedAtUtc;
            entity["AttemptCount"] = attemptCount + 1;

            await _tableClient.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace, cancellationToken);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status is 404 or 412)
        {
            return false;
        }
    }

    public async Task MarkPublishedAsync(
        string scheduledPostId,
        DateTimeOffset publishedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scheduledPostId);
        await EnsureInitializedAsync(cancellationToken);

        var response = await _tableClient.GetEntityAsync<TableEntity>(
            _options.TableStorage.PartitionKey,
            scheduledPostId,
            cancellationToken: cancellationToken);

        var entity = response.Value;
        entity["Status"] = ScheduledPostStatus.Published.ToString();
        entity["PublishedAtUtc"] = publishedAtUtc;
        entity.Remove("LastErrorCode");
        entity.Remove("LastErrorMessage");

        await _tableClient.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace, cancellationToken);
    }

    public async Task MarkFailedAsync(
        string scheduledPostId,
        string errorCode,
        string errorMessage,
        DateTimeOffset attemptedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scheduledPostId);
        await EnsureInitializedAsync(cancellationToken);

        var response = await _tableClient.GetEntityAsync<TableEntity>(
            _options.TableStorage.PartitionKey,
            scheduledPostId,
            cancellationToken: cancellationToken);

        var entity = response.Value;
        entity["Status"] = ScheduledPostStatus.Failed.ToString();
        entity["LastAttemptedAtUtc"] = attemptedAtUtc;
        entity["LastErrorCode"] = errorCode;
        entity["LastErrorMessage"] = errorMessage;

        await _tableClient.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace, cancellationToken);
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

            try
            {
                await _tableClient.CreateIfNotExistsAsync(cancellationToken);
            }
            catch (RequestFailedException ex) when (ex.Status == 400)
            {
                throw new InvalidOperationException(
                    $"Failed to create or access Azure Table '{_options.TableStorage.TableName}'. Verify ScheduledSocialPost table configuration, especially the table name and storage account settings.",
                    ex);
            }

            _initialized = true;
            _logger.LogInformation(
                "Ensured Azure Table {TableName} exists at {AccountEndpoint}",
                _options.TableStorage.TableName,
                _options.TableStorage.AccountEndpoint);
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private TableEntity MapModelToEntity(ScheduledSocialPostRecord record)
    {
        var entity = new TableEntity(_options.TableStorage.PartitionKey, record.ScheduledPostId)
        {
            ["ScheduledPostId"] = record.ScheduledPostId,
            ["ScheduledForUtc"] = record.ScheduledForUtc,
            ["Status"] = record.Status.ToString(),
            ["Text"] = record.Text,
            ["Hashtags"] = JsonSerializer.Serialize(record.Hashtags, JsonOptions),
            ["TargetPlatforms"] = JsonSerializer.Serialize(record.TargetPlatforms, JsonOptions),
            ["ImageUrls"] = JsonSerializer.Serialize(record.ImageUrls, JsonOptions),
            ["UploadedImages"] = JsonSerializer.Serialize(record.UploadedImages, JsonOptions),
            ["CreatedAtUtc"] = record.CreatedAtUtc,
            ["AttemptCount"] = record.AttemptCount
        };

        if (record.LastAttemptedAtUtc.HasValue)
        {
            entity["LastAttemptedAtUtc"] = record.LastAttemptedAtUtc.Value;
        }

        if (record.PublishedAtUtc.HasValue)
        {
            entity["PublishedAtUtc"] = record.PublishedAtUtc.Value;
        }

        if (!string.IsNullOrWhiteSpace(record.LastErrorCode))
        {
            entity["LastErrorCode"] = record.LastErrorCode;
        }

        if (!string.IsNullOrWhiteSpace(record.LastErrorMessage))
        {
            entity["LastErrorMessage"] = record.LastErrorMessage;
        }

        return entity;
    }

    private static ScheduledSocialPostRecord MapEntityToModel(TableEntity entity)
    {
        var imageUrls = DeserializeList<ImageUrl>(entity.GetString("ImageUrls"));
        var uploadedImages = DeserializeList<StoredImageData>(entity.GetString("UploadedImages"));

        return new ScheduledSocialPostRecord
        {
            ScheduledPostId = entity.GetString("ScheduledPostId") ?? entity.RowKey,
            ScheduledForUtc = entity.GetDateTimeOffset("ScheduledForUtc") ?? DateTimeOffset.MinValue,
            Status = ParseStatus(entity.GetString("Status")),
            Text = entity.GetString("Text") ?? string.Empty,
            Hashtags = DeserializeList<string>(entity.GetString("Hashtags")),
            TargetPlatforms = DeserializeList<string>(entity.GetString("TargetPlatforms")),
            ImageUrls = imageUrls,
            UploadedImages = uploadedImages,
            CreatedAtUtc = entity.GetDateTimeOffset("CreatedAtUtc") ?? DateTimeOffset.MinValue,
            LastAttemptedAtUtc = entity.GetDateTimeOffset("LastAttemptedAtUtc"),
            PublishedAtUtc = entity.GetDateTimeOffset("PublishedAtUtc"),
            LastErrorCode = entity.GetString("LastErrorCode"),
            LastErrorMessage = entity.GetString("LastErrorMessage"),
            AttemptCount = entity.GetInt32("AttemptCount") ?? 0
        };
    }

    private static List<T> DeserializeList<T>(string? serialized)
    {
        if (string.IsNullOrWhiteSpace(serialized))
        {
            return [];
        }

        var value = JsonSerializer.Deserialize<List<T>>(serialized, JsonOptions);
        return value ?? [];
    }

    private static string? NullIfEmpty(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static ScheduledPostStatus ParseStatus(string? value)
    {
        if (Enum.TryParse<ScheduledPostStatus>(value, true, out var parsed))
        {
            return parsed;
        }

        return ScheduledPostStatus.Pending;
    }
}

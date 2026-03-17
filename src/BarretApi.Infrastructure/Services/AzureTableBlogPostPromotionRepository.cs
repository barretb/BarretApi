using System.Security.Cryptography;
using System.Text;
using Azure;
using Azure.Data.Tables;
using Azure.Identity;
using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BarretApi.Infrastructure.Services;

public sealed class AzureTableBlogPostPromotionRepository : IBlogPostPromotionRepository
{
    private readonly BlogPromotionOptions _options;
    private readonly ILogger<AzureTableBlogPostPromotionRepository> _logger;
    private readonly TableClient _tableClient;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _initialized;

    public AzureTableBlogPostPromotionRepository(
        IOptions<BlogPromotionOptions> blogPromotionOptions,
        ILogger<AzureTableBlogPostPromotionRepository> logger)
    {
        _options = blogPromotionOptions.Value;
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

    public async Task<BlogPostPromotionRecord?> GetByEntryIdentityAsync(
        string entryIdentity,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entryIdentity);
        await EnsureInitializedAsync(cancellationToken);

        var rowKey = CreateRowKey(entryIdentity);

        try
        {
            var response = await _tableClient.GetEntityAsync<TableEntity>(
                _options.TableStorage.PartitionKey,
                rowKey,
                cancellationToken: cancellationToken);

            var entity = response.Value;
            if (!string.Equals(entity.GetString("EntryIdentity"), entryIdentity, StringComparison.Ordinal))
            {
                return null;
            }

            return MapEntityToModel(entity);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<BlogPostPromotionRecord>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var records = new List<BlogPostPromotionRecord>();
        var filter = $"PartitionKey eq '{_options.TableStorage.PartitionKey}'";

        await foreach (var entity in _tableClient.QueryAsync<TableEntity>(filter, cancellationToken: cancellationToken))
        {
            records.Add(MapEntityToModel(entity));
        }

        return records;
    }

    public async Task UpsertAsync(BlogPostPromotionRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        await EnsureInitializedAsync(cancellationToken);

        var entity = MapModelToEntity(record);
        await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
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

    private TableEntity MapModelToEntity(BlogPostPromotionRecord record)
    {
        var entity = new TableEntity(_options.TableStorage.PartitionKey, CreateRowKey(record.EntryIdentity))
        {
            ["EntryIdentity"] = record.EntryIdentity,
            ["CanonicalUrl"] = record.CanonicalUrl,
            ["Title"] = record.Title,
            ["PublishedAtUtc"] = record.PublishedAtUtc,
            ["FeedUrl"] = record.FeedUrl,
            ["InitialPostStatus"] = record.InitialPostStatus.ToString(),
            ["InitialPostResultCode"] = record.InitialPostResultCode,
            ["ReminderPostStatus"] = record.ReminderPostStatus.ToString(),
            ["ReminderPostResultCode"] = record.ReminderPostResultCode,
            ["LastProcessedAtUtc"] = record.LastProcessedAtUtc
        };

        if (record.InitialPostAttemptedAtUtc.HasValue)
        {
            entity["InitialPostAttemptedAtUtc"] = record.InitialPostAttemptedAtUtc.Value;
        }

        if (record.InitialPostSucceededAtUtc.HasValue)
        {
            entity["InitialPostSucceededAtUtc"] = record.InitialPostSucceededAtUtc.Value;
        }

        if (record.ReminderPostAttemptedAtUtc.HasValue)
        {
            entity["ReminderPostAttemptedAtUtc"] = record.ReminderPostAttemptedAtUtc.Value;
        }

        if (record.ReminderPostSucceededAtUtc.HasValue)
        {
            entity["ReminderPostSucceededAtUtc"] = record.ReminderPostSucceededAtUtc.Value;
        }

        return entity;
    }

    private static BlogPostPromotionRecord MapEntityToModel(TableEntity entity)
    {
        return new BlogPostPromotionRecord
        {
            EntryIdentity = entity.GetString("EntryIdentity") ?? string.Empty,
            CanonicalUrl = entity.GetString("CanonicalUrl") ?? string.Empty,
            Title = entity.GetString("Title") ?? string.Empty,
            PublishedAtUtc = entity.GetDateTimeOffset("PublishedAtUtc") ?? DateTimeOffset.MinValue,
            FeedUrl = entity.GetString("FeedUrl"),
            InitialPostStatus = ParseStatus(entity.GetString("InitialPostStatus")),
            InitialPostAttemptedAtUtc = entity.GetDateTimeOffset("InitialPostAttemptedAtUtc"),
            InitialPostSucceededAtUtc = entity.GetDateTimeOffset("InitialPostSucceededAtUtc"),
            InitialPostResultCode = entity.GetString("InitialPostResultCode"),
            ReminderPostStatus = ParseStatus(entity.GetString("ReminderPostStatus")),
            ReminderPostAttemptedAtUtc = entity.GetDateTimeOffset("ReminderPostAttemptedAtUtc"),
            ReminderPostSucceededAtUtc = entity.GetDateTimeOffset("ReminderPostSucceededAtUtc"),
            ReminderPostResultCode = entity.GetString("ReminderPostResultCode"),
            LastProcessedAtUtc = entity.GetDateTimeOffset("LastProcessedAtUtc") ?? DateTimeOffset.MinValue
        };
    }

    private static string CreateRowKey(string entryIdentity)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(entryIdentity));
        return Convert.ToHexString(bytes);
    }

    private static PostAttemptStatus ParseStatus(string? value)
    {
        if (Enum.TryParse<PostAttemptStatus>(value, true, out var parsed))
        {
            return parsed;
        }

        return PostAttemptStatus.NotAttempted;
    }
}

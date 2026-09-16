using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using Azure.Identity;
using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BarretApi.Infrastructure.Services;

public sealed class AzureTableScheduledSocialPostRepository : IScheduledSocialPostRepository
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	private readonly ScheduledSocialPostOptions _options;
	private readonly ILogger<AzureTableScheduledSocialPostRepository> _logger;
	private readonly TableClient _tableClient;
	private readonly TableServiceClient _tableServiceClient;
	private readonly string _tableName;
	private readonly SemaphoreSlim _initializationLock = new(1, 1);
	private bool _initialized;

	public AzureTableScheduledSocialPostRepository(
		IOptions<ScheduledSocialPostOptions> scheduledSocialPostOptions,
		ILogger<AzureTableScheduledSocialPostRepository> logger)
	{
		_options = scheduledSocialPostOptions.Value;
		_logger = logger;
		_options.ThrowIfInvalid();
		_tableName = _options.TableStorage.TableName.Trim().ToLowerInvariant();

		if (!string.IsNullOrWhiteSpace(_options.TableStorage.ConnectionString))
		{
			_tableServiceClient = new TableServiceClient(_options.TableStorage.ConnectionString);
			_tableClient = new TableClient(
				_options.TableStorage.ConnectionString,
				_tableName);
		}
		else
		{
			_tableServiceClient = new TableServiceClient(
				new Uri(_options.TableStorage.AccountEndpoint),
				new DefaultAzureCredential());
			_tableClient = new TableClient(
				new Uri(_options.TableStorage.AccountEndpoint),
				_tableName,
				new DefaultAzureCredential());
		}
	}

	internal AzureTableScheduledSocialPostRepository(
		TableClient tableClient, TableServiceClient tableServiceClient,
		IOptions<ScheduledSocialPostOptions> options,
		ILogger<AzureTableScheduledSocialPostRepository> logger)
	{
		_tableClient = tableClient;
		_tableServiceClient = tableServiceClient;
		_options = options.Value;
		_logger = logger;
		_tableName = _options.TableStorage.TableName;
		_initialized = true;
	}

	public async Task<ScheduledSocialPostRecord?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(id);
		await EnsureInitializedAsync(cancellationToken);
		var response = await _tableClient.GetEntityIfExistsAsync<TableEntity>(
			_options.TableStorage.PartitionKey, id, cancellationToken: cancellationToken);
		return response.HasValue && response.Value is not null ? MapEntityToModel(response.Value) : null;
	}

	public async Task<ScheduledPostsPage> ListAsync(ScheduledPostsQuery query, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(query);
		ArgumentOutOfRangeException.ThrowIfLessThan(query.PageSize, 1);
		ArgumentOutOfRangeException.ThrowIfGreaterThan(query.PageSize, 100);
		if ((query.Status.HasValue && !Enum.IsDefined(query.Status.Value))
			|| (query.FromUtc.HasValue && query.ToUtc.HasValue && query.FromUtc > query.ToUtc))
		{
			throw new ArgumentException("Invalid status or scheduled date range.");
		}

		await EnsureInitializedAsync(cancellationToken);
		var filter = BuildListFilter(query);
		try
		{
			var pages = _tableClient.QueryAsync<TableEntity>(filter, maxPerPage: query.PageSize, cancellationToken: cancellationToken)
				.AsPages(query.ContinuationToken, query.PageSize);
			await foreach (var page in pages.WithCancellation(cancellationToken))
			{
				return new ScheduledPostsPage(page.Values.Select(MapEntityToModel).ToList(), page.ContinuationToken);
			}

			return new ScheduledPostsPage([], null);
		}
		catch (RequestFailedException ex) when (ex.Status == 400)
		{
			throw new ArgumentException("The query or continuation token is invalid.", nameof(query), ex);
		}
	}

	public async Task<bool> TryUpdateAsync(ScheduledSocialPostRecord record, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(record);
		ArgumentException.ThrowIfNullOrWhiteSpace(record.Version);
		if (record.Version == "*")
		{
			throw new ArgumentException("Wildcard versions are not permitted.");
		}

		await EnsureInitializedAsync(cancellationToken);
		try
		{
			await SaveClaimAsync(record, cancellationToken);
			return true;
		}
		catch (RequestFailedException ex) when (ex.Status is 404 or 412)
		{
			return false;
		}
	}

	private string BuildListFilter(ScheduledPostsQuery query)
	{
		var filter = TableClient.CreateQueryFilter($"PartitionKey eq {_options.TableStorage.PartitionKey}");
		if (query.Status.HasValue)
		{
			filter += " and " + TableClient.CreateQueryFilter($"Status eq {query.Status.Value.ToString()}");
		}

		if (query.FromUtc.HasValue)
		{
			filter += " and " + TableClient.CreateQueryFilter($"ScheduledForUtc ge {query.FromUtc.Value.ToUniversalTime()}");
		}

		if (query.ToUtc.HasValue)
		{
			filter += " and " + TableClient.CreateQueryFilter($"ScheduledForUtc le {query.ToUtc.Value.ToUniversalTime()}");
		}

		return filter;
	}

	public async Task SaveScheduledAsync(
		ScheduledSocialPostRecord record,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(record);
		await EnsureInitializedAsync(cancellationToken);

		var entity = MapModelToEntity(record, _options.TableStorage.PartitionKey);
		await _tableClient.AddEntityAsync(entity, cancellationToken);
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
			if (ScheduledPostClaimPolicy.IsDue(record, asOfUtc))
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

	public async Task<ScheduledSocialPostRecord?> TryClaimAsync(
		string scheduledPostId,
		DateTimeOffset attemptedAtUtc,
		CancellationToken cancellationToken = default)
	{
		await EnsureInitializedAsync(cancellationToken);
		try
		{
			var response = await _tableClient.GetEntityAsync<TableEntity>(
				_options.TableStorage.PartitionKey, scheduledPostId, cancellationToken: cancellationToken);
			var record = MapEntityToModel(response.Value);
			if (!ScheduledPostClaimPolicy.TryClaim(record, attemptedAtUtc))
			{
				return null;
			}

			await SaveClaimAsync(record, cancellationToken);
			return record;
		}
		catch (RequestFailedException ex) when (ex.Status is 404 or 412)
		{
			return null;
		}
	}

	public async Task SaveClaimAsync(ScheduledSocialPostRecord record, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(record.Version);
		var response = await _tableClient.UpdateEntityAsync(
			MapModelToEntity(record, _options.TableStorage.PartitionKey), new ETag(record.Version), TableUpdateMode.Replace, cancellationToken);
		record.Version = response.Headers.ETag?.ToString()
			?? throw new InvalidOperationException("Storage did not return a concurrency version.");
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
				var createdWithFallback = await TryCreateWithServiceClientAsync(cancellationToken);

				if (createdWithFallback)
				{
					_logger.LogWarning(
						ex,
						"TableClient.CreateIfNotExists failed for table {TableName}, but TableServiceClient fallback succeeded.",
						_tableName);
				}

				// Some environments block table creation but still allow read/write to an existing table.
				// Probe access before failing so we can keep working with pre-provisioned tables.
				var canAccessExistingTable = await CanAccessExistingTableAsync(cancellationToken);
				if (!createdWithFallback && !canAccessExistingTable)
				{
					throw new InvalidOperationException(
						$"Failed to create or access Azure Table '{_tableName}'. Verify ScheduledSocialPost table configuration and ensure the table exists. In restricted environments, pre-create the table and grant the app data-plane access.",
						ex);
				}

				if (!createdWithFallback && canAccessExistingTable)
				{
					_logger.LogWarning(
						ex,
						"CreateIfNotExists failed for table {TableName}, but table access probe succeeded. Continuing with existing table.",
						_tableName);
				}
			}

			_initialized = true;
			_logger.LogInformation(
				"Ensured Azure Table {TableName} exists at {AccountEndpoint}",
				_tableName,
				_options.TableStorage.AccountEndpoint);
		}
		finally
		{
			_initializationLock.Release();
		}
	}

	private async Task<bool> CanAccessExistingTableAsync(CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var _ in _tableClient.QueryAsync<TableEntity>(maxPerPage: 1, cancellationToken: cancellationToken))
			{
				break;
			}

			return true;
		}
		catch (RequestFailedException ex) when (ex.Status == 404)
		{
			return false;
		}
	}

	private async Task<bool> TryCreateWithServiceClientAsync(CancellationToken cancellationToken)
	{
		try
		{
			await _tableServiceClient.CreateTableIfNotExistsAsync(_tableName, cancellationToken: cancellationToken);
			return true;
		}
		catch (RequestFailedException)
		{
			return false;
		}
	}

	internal static TableEntity MapModelToEntity(ScheduledSocialPostRecord record, string partitionKey)
	{
		var entity = new TableEntity(partitionKey, record.ScheduledPostId)
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
			["AttemptCount"] = record.AttemptCount,
			["AutoThread"] = record.AutoThread,
			["DeliveryTrackingEnabled"] = record.DeliveryTrackingEnabled,
			["DeliveryResults"] = JsonSerializer.Serialize(record.DeliveryResults, JsonOptions),
			["DeliveryConfirmations"] = JsonSerializer.Serialize(record.DeliveryConfirmations, JsonOptions)
		};

		if (record.UpdatedAtUtc.HasValue)
		{
			entity["UpdatedAtUtc"] = record.UpdatedAtUtc.Value;
		}

		if (record.LastManagementAction is not null)
		{
			entity["LastManagementAction"] = record.LastManagementAction;
		}

		if (record.ManagementNote is not null)
		{
			entity["ManagementNote"] = record.ManagementNote;
		}

		if (record.LeaseExpiresAtUtc.HasValue)
		{
			entity["LeaseExpiresAtUtc"] = record.LeaseExpiresAtUtc.Value;
		}

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

	internal static ScheduledSocialPostRecord MapEntityToModel(TableEntity entity)
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
			AttemptCount = entity.GetInt32("AttemptCount") ?? 0,
			AutoThread = entity.GetBoolean("AutoThread") ?? false,
			DeliveryTrackingEnabled = entity.GetBoolean("DeliveryTrackingEnabled") ?? false,
			DeliveryResults = DeserializeList<PlatformPostResult>(entity.GetString("DeliveryResults")),
			LeaseExpiresAtUtc = entity.GetDateTimeOffset("LeaseExpiresAtUtc"),
			Version = entity.ETag.ToString(),
			UpdatedAtUtc = entity.Timestamp ?? entity.GetDateTimeOffset("UpdatedAtUtc"),
			LastManagementAction = entity.GetString("LastManagementAction"),
			ManagementNote = entity.GetString("ManagementNote"),
			DeliveryConfirmations = DeserializeList<PostDeliveryConfirmation>(entity.GetString("DeliveryConfirmations"))
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

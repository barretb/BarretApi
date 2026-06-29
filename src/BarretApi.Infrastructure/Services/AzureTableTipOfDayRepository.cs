using Azure;
using Azure.Data.Tables;
using Azure.Identity;
using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BarretApi.Infrastructure.Services;

public sealed class AzureTableTipOfDayRepository : ITipOfDayRepository
{
	private readonly TipOfDayOptions _options;
	private readonly ILogger<AzureTableTipOfDayRepository> _logger;
	private readonly TableClient _tableClient;
	private readonly TableServiceClient _tableServiceClient;
	private readonly string _tableName;
	private readonly SemaphoreSlim _initializationLock = new(1, 1);
	private bool _initialized;

	public AzureTableTipOfDayRepository(
		IOptions<TipOfDayOptions> tipOfDayOptions,
		ILogger<AzureTableTipOfDayRepository> logger)
	{
		_options = tipOfDayOptions.Value;
		_logger = logger;
		_options.ThrowIfInvalid();
		_tableName = _options.TableStorage.TableName.Trim().ToLowerInvariant();

		if (!string.IsNullOrWhiteSpace(_options.TableStorage.ConnectionString))
		{
			_tableServiceClient = new TableServiceClient(_options.TableStorage.ConnectionString);
			_tableClient = new TableClient(_options.TableStorage.ConnectionString, _tableName);
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

	public async Task AddAsync(TipOfDayRecord record, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(record);
		await EnsureInitializedAsync(cancellationToken);

		var entity = MapModelToEntity(record);
		await _tableClient.AddEntityAsync(entity, cancellationToken);
	}

	public async Task<IReadOnlyList<TipOfDayRecord>> GetEligibleByCategoryAsync(
		string category,
		DateTimeOffset repostCutoffUtc,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(category);
		await EnsureInitializedAsync(cancellationToken);

		var escapedCategory = EscapeODataString(category.Trim());
		var filter = $"PartitionKey eq '{_options.TableStorage.PartitionKey}' and Category eq '{escapedCategory}'";
		var records = new List<TipOfDayRecord>();

		await foreach (var entity in _tableClient.QueryAsync<TableEntity>(filter, cancellationToken: cancellationToken))
		{
			var record = MapEntityToModel(entity);
			if (!record.LastPostedDate.HasValue || record.LastPostedDate.Value <= repostCutoffUtc)
			{
				records.Add(record);
			}
		}

		return records;
	}

	public async Task MarkPostedAsync(
		string tipId,
		DateTimeOffset postedAtUtc,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tipId);
		await EnsureInitializedAsync(cancellationToken);

		var response = await _tableClient.GetEntityAsync<TableEntity>(
			_options.TableStorage.PartitionKey,
			tipId,
			cancellationToken: cancellationToken);

		var entity = response.Value;
		entity["LastPostedDate"] = postedAtUtc;
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
				var createdWithFallback = await TryCreateWithServiceClientAsync(cancellationToken);
				var canAccessExistingTable = await CanAccessExistingTableAsync(cancellationToken);
				if (!createdWithFallback && !canAccessExistingTable)
				{
					throw new InvalidOperationException(
						$"Failed to create or access Azure Table '{_tableName}'. Verify TipOfDay table configuration and ensure the table exists.",
						ex);
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

	private TableEntity MapModelToEntity(TipOfDayRecord record)
	{
		var entity = new TableEntity(_options.TableStorage.PartitionKey, record.TipId)
		{
			["TipId"] = record.TipId,
			["Category"] = record.Category,
			["Tip"] = record.Tip,
			["CreatedAtUtc"] = record.CreatedAtUtc
		};

		if (!string.IsNullOrWhiteSpace(record.MoreInfoUrl))
		{
			entity["MoreInfoUrl"] = record.MoreInfoUrl;
		}

		if (record.LastPostedDate.HasValue)
		{
			entity["LastPostedDate"] = record.LastPostedDate.Value;
		}

		return entity;
	}

	private static TipOfDayRecord MapEntityToModel(TableEntity entity)
	{
		return new TipOfDayRecord
		{
			TipId = entity.GetString("TipId") ?? entity.RowKey,
			Category = entity.GetString("Category") ?? string.Empty,
			Tip = entity.GetString("Tip") ?? string.Empty,
			MoreInfoUrl = entity.GetString("MoreInfoUrl"),
			LastPostedDate = entity.GetDateTimeOffset("LastPostedDate"),
			CreatedAtUtc = entity.GetDateTimeOffset("CreatedAtUtc") ?? DateTimeOffset.MinValue
		};
	}

	private static string EscapeODataString(string value)
	{
		return value.Replace("'", "''", StringComparison.Ordinal);
	}
}

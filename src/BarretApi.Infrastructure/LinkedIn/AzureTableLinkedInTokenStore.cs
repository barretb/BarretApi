using Azure;
using Azure.Data.Tables;
using Azure.Identity;
using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BarretApi.Infrastructure.LinkedIn;

public sealed class AzureTableLinkedInTokenStore : ILinkedInTokenStore
{
    private const string PartitionKey = "linkedin-tokens";
    private const string RowKey = "current";

    private readonly TableClient _tableClient;
    private readonly ILogger<AzureTableLinkedInTokenStore> _logger;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _initialized;

    public AzureTableLinkedInTokenStore(
        IOptions<LinkedInOptions> options,
        ILogger<AzureTableLinkedInTokenStore> logger)
    {
        _logger = logger;
        var opts = options.Value;

        if (string.IsNullOrWhiteSpace(opts.TokenStorage.TableName))
        {
            throw new InvalidOperationException("LinkedIn:TokenStorage:TableName is required.");
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
                "LinkedIn:TokenStorage requires either ConnectionString or AccountEndpoint.");
        }
    }

    public async Task<LinkedInTokenRecord?> GetTokensAsync(CancellationToken cancellationToken = default)
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
            var refreshToken = entity.GetString("RefreshToken");

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                return null;
            }

            return new LinkedInTokenRecord
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                AccessTokenExpiresAtUtc = entity.GetDateTimeOffset("AccessTokenExpiresAtUtc") ?? DateTimeOffset.MinValue,
                RefreshTokenExpiresAtUtc = entity.GetDateTimeOffset("RefreshTokenExpiresAtUtc") ?? DateTimeOffset.MinValue,
                UpdatedAtUtc = entity.GetDateTimeOffset("UpdatedAtUtc") ?? DateTimeOffset.MinValue
            };
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task SaveTokensAsync(LinkedInTokenRecord tokens, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        await EnsureInitializedAsync(cancellationToken);

        var entity = new TableEntity(PartitionKey, RowKey)
        {
            ["AccessToken"] = tokens.AccessToken,
            ["RefreshToken"] = tokens.RefreshToken,
            ["AccessTokenExpiresAtUtc"] = tokens.AccessTokenExpiresAtUtc,
            ["RefreshTokenExpiresAtUtc"] = tokens.RefreshTokenExpiresAtUtc,
            ["UpdatedAtUtc"] = tokens.UpdatedAtUtc
        };

        await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
        _logger.LogInformation("LinkedIn tokens saved to table storage");
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
            _logger.LogInformation("Ensured LinkedIn token table exists");
        }
        finally
        {
            _initializationLock.Release();
        }
    }
}

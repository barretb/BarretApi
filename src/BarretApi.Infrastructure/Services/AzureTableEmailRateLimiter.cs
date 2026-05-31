using Azure.Data.Tables;
using BarretApi.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace BarretApi.Infrastructure.Services;

public sealed class AzureTableEmailRateLimiter(
    TableServiceClient tableServiceClient,
    ILogger<AzureTableEmailRateLimiter> logger) : IEmailRateLimiter
{
    private const string TableName = "EmailRateLimit";
    private const string PartitionKey = "PostTypeNotifications";

    private readonly TableServiceClient _tableServiceClient = tableServiceClient;
    private readonly ILogger<AzureTableEmailRateLimiter> _logger = logger;

    public async Task<bool> CanSendEmailAsync(string postType, CancellationToken cancellationToken = default)
    {
        try
        {
            var tableClient = await GetTableClientAsync(cancellationToken);
            var entity = await GetRateLimitEntityAsync(tableClient, postType, cancellationToken);

            if (entity is null)
            {
                return true;
            }

            var now = DateTimeOffset.UtcNow;
            var lastSent = entity.LastEmailSentUtc;
            var daysSinceLastEmail = (now - lastSent).TotalDays;

            if (daysSinceLastEmail >= 1.0)
            {
                _logger.LogDebug(
                    "Email rate limit check passed for {PostType}: {DaysSince:F2} days since last email",
                    postType,
                    daysSinceLastEmail);
                return true;
            }

            _logger.LogInformation(
                "Email rate limit reached for {PostType}: Last email sent {LastSent}, {HoursRemaining:F1} hours until next email allowed",
                postType,
                lastSent,
                24 - (daysSinceLastEmail * 24));

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking email rate limit for {PostType}, allowing email", postType);
            return true;
        }
    }

    public async Task RecordEmailSentAsync(string postType, CancellationToken cancellationToken = default)
    {
        try
        {
            var tableClient = await GetTableClientAsync(cancellationToken);
            var now = DateTimeOffset.UtcNow;

            var entity = new EmailRateLimitEntity
            {
                PartitionKey = PartitionKey,
                RowKey = SanitizePostType(postType),
                PostType = postType,
                LastEmailSentUtc = now,
                EmailCount = await IncrementEmailCountAsync(tableClient, postType, cancellationToken)
            };

            await tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);

            _logger.LogDebug(
                "Recorded email sent for {PostType} at {Timestamp}",
                postType,
                now);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording email sent for {PostType}", postType);
        }
    }

    private async Task<TableClient> GetTableClientAsync(CancellationToken cancellationToken)
    {
        var tableClient = _tableServiceClient.GetTableClient(TableName);
        await tableClient.CreateIfNotExistsAsync(cancellationToken);
        return tableClient;
    }

    private async Task<EmailRateLimitEntity?> GetRateLimitEntityAsync(
        TableClient tableClient,
        string postType,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await tableClient.GetEntityAsync<EmailRateLimitEntity>(
                PartitionKey,
                SanitizePostType(postType),
                cancellationToken: cancellationToken);

            return response.Value;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    private async Task<int> IncrementEmailCountAsync(
        TableClient tableClient,
        string postType,
        CancellationToken cancellationToken)
    {
        var existing = await GetRateLimitEntityAsync(tableClient, postType, cancellationToken);
        return (existing?.EmailCount ?? 0) + 1;
    }

    private static string SanitizePostType(string postType)
    {
        return postType.Replace(" ", "_").Replace("/", "_").Replace("\\", "_");
    }

    private sealed class EmailRateLimitEntity : ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;
        public DateTimeOffset? Timestamp { get; set; }
        public Azure.ETag ETag { get; set; }

        public string PostType { get; set; } = string.Empty;
        public DateTimeOffset LastEmailSentUtc { get; set; }
        public int EmailCount { get; set; }
    }
}

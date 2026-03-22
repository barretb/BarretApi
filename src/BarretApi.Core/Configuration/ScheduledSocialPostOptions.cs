namespace BarretApi.Core.Configuration;

public sealed class ScheduledSocialPostOptions
{
    public const string SectionName = "ScheduledSocialPost";

    public ScheduledSocialPostTableStorageOptions TableStorage { get; init; } = new();
    public int MaxBatchSize { get; init; } = 100;

    public string? Validate()
    {
        if (MaxBatchSize <= 0)
        {
            return "ScheduledSocialPost:MaxBatchSize must be greater than zero.";
        }

        if (MaxBatchSize > 1_000)
        {
            return "ScheduledSocialPost:MaxBatchSize must not exceed 1000.";
        }

        // Only validate AccountEndpoint if it's explicitly provided
        if (!string.IsNullOrWhiteSpace(TableStorage.AccountEndpoint))
        {
            if (!Uri.TryCreate(TableStorage.AccountEndpoint, UriKind.Absolute, out var endpointUri))
            {
                return "ScheduledSocialPost:TableStorage:AccountEndpoint must be a valid absolute URL if provided.";
            }

            if (endpointUri.Scheme is not "https")
            {
                return "ScheduledSocialPost:TableStorage:AccountEndpoint must use https.";
            }
        }
        else if (string.IsNullOrWhiteSpace(TableStorage.ConnectionString))
        {
            // If neither AccountEndpoint nor ConnectionString is provided, require ConnectionString
            return "ScheduledSocialPost:TableStorage:ConnectionString or AccountEndpoint must be configured.";
        }

        if (string.IsNullOrWhiteSpace(TableStorage.TableName))
        {
            return "ScheduledSocialPost:TableStorage:TableName is required.";
        }

        if (string.IsNullOrWhiteSpace(TableStorage.PartitionKey))
        {
            return "ScheduledSocialPost:TableStorage:PartitionKey is required.";
        }

        return null;
    }

    public void ThrowIfInvalid()
    {
        var error = Validate();
        if (!string.IsNullOrWhiteSpace(error))
        {
            throw new InvalidOperationException(error);
        }
    }
}

public sealed class ScheduledSocialPostTableStorageOptions
{
    public string? ConnectionString { get; init; }
    public string AccountEndpoint { get; init; } = string.Empty;
    public string TableName { get; init; } = "scheduledsocialposts";
    public string PartitionKey { get; init; } = "scheduled-social-post";
}

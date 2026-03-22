namespace BarretApi.Core.Configuration;

public sealed class ScheduledSocialPostOptions
{
    public const string SectionName = "ScheduledSocialPost";

    public ScheduledSocialPostTableStorageOptions TableStorage { get; init; } = new();
    public ScheduledSocialPostBlobStorageOptions BlobStorage { get; init; } = new();
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

        if (!IsValidTableName(TableStorage.TableName))
        {
            return "ScheduledSocialPost:TableStorage:TableName must be a valid Azure Table name (3-63 characters, start with a letter, letters and numbers only).";
        }

        if (string.IsNullOrWhiteSpace(TableStorage.PartitionKey))
        {
            return "ScheduledSocialPost:TableStorage:PartitionKey is required.";
        }

        if (!IsValidBlobContainerName(BlobStorage.ContainerName))
        {
            return "ScheduledSocialPost:BlobStorage:ContainerName must be a valid Azure Blob container name (3-63 lowercase characters, letters, numbers, and hyphens only, starting and ending with a letter or number).";
        }

        return null;
    }

    private static bool IsValidBlobContainerName(string name)
    {
        if (name.Length is < 3 or > 63)
        {
            return false;
        }

        if (!char.IsLetterOrDigit(name[0]) || !char.IsLetterOrDigit(name[^1]))
        {
            return false;
        }

        var prevHyphen = false;
        foreach (var c in name)
        {
            if (c == '-')
            {
                if (prevHyphen)
                {
                    return false;
                }

                prevHyphen = true;
            }
            else if (char.IsLower(c) || char.IsDigit(c))
            {
                prevHyphen = false;
            }
            else
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidTableName(string tableName)
    {
        if (tableName.Length is < 3 or > 63)
        {
            return false;
        }

        if (!char.IsLetter(tableName[0]))
        {
            return false;
        }

        foreach (var character in tableName)
        {
            if (!char.IsLetterOrDigit(character))
            {
                return false;
            }
        }

        return true;
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

public sealed class ScheduledSocialPostBlobStorageOptions
{
    public string ContainerName { get; init; } = "scheduled-post-images";
}

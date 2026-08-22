namespace BarretApi.Core.Configuration;

public sealed class JobSchedulerOptions
{
	public const string SectionName = "JobScheduler";

	public bool Enabled { get; init; }
	public int TickIntervalSeconds { get; init; } = 30;
	public int ClaimTimeoutMinutes { get; init; } = 30;
	public int RunRetentionDays { get; init; } = 30;
	public JobSchedulerTableStorageOptions TableStorage { get; init; } = new();

	public string? Validate()
	{
		if (TickIntervalSeconds is < 1 or > 3_600)
		{
			return "JobScheduler:TickIntervalSeconds must be between 1 and 3600.";
		}

		if (ClaimTimeoutMinutes <= 0)
		{
			return "JobScheduler:ClaimTimeoutMinutes must be greater than zero.";
		}

		if (RunRetentionDays <= 0)
		{
			return "JobScheduler:RunRetentionDays must be greater than zero.";
		}

		if (!string.IsNullOrWhiteSpace(TableStorage.AccountEndpoint))
		{
			if (!Uri.TryCreate(TableStorage.AccountEndpoint, UriKind.Absolute, out var endpointUri))
			{
				return "JobScheduler:TableStorage:AccountEndpoint must be a valid absolute URL if provided.";
			}

			if (endpointUri.Scheme is not "https")
			{
				return "JobScheduler:TableStorage:AccountEndpoint must use https.";
			}
		}
		else if (string.IsNullOrWhiteSpace(TableStorage.ConnectionString))
		{
			return "JobScheduler:TableStorage:ConnectionString or AccountEndpoint must be configured.";
		}

		if (!IsValidTableName(TableStorage.JobsTableName))
		{
			return "JobScheduler:TableStorage:JobsTableName must be a valid Azure Table name (3-63 characters, start with a letter, letters and numbers only).";
		}

		if (!IsValidTableName(TableStorage.RunsTableName))
		{
			return "JobScheduler:TableStorage:RunsTableName must be a valid Azure Table name (3-63 characters, start with a letter, letters and numbers only).";
		}

		if (string.IsNullOrWhiteSpace(TableStorage.PartitionKey))
		{
			return "JobScheduler:TableStorage:PartitionKey is required.";
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

	private static bool IsValidTableName(string tableName)
	{
		if (string.IsNullOrWhiteSpace(tableName) || tableName.Length is < 3 or > 63)
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
}

public sealed class JobSchedulerTableStorageOptions
{
	public string? ConnectionString { get; init; }
	public string AccountEndpoint { get; init; } = string.Empty;
	public string JobsTableName { get; init; } = "scheduledjobs";
	public string RunsTableName { get; init; } = "jobruns";
	public string PartitionKey { get; init; } = "scheduled-job";
}

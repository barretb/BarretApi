namespace BarretApi.Core.Configuration;

public sealed class TipOfDayOptions
{
	public const string SectionName = "TipOfDay";

	public TipOfDayTableStorageOptions TableStorage { get; init; } = new();
	public int RepostCooldownDays { get; init; } = 180;

	public string? Validate()
	{
		if (RepostCooldownDays <= 0)
		{
			return "TipOfDay:RepostCooldownDays must be greater than zero.";
		}

		if (!string.IsNullOrWhiteSpace(TableStorage.AccountEndpoint))
		{
			if (!Uri.TryCreate(TableStorage.AccountEndpoint, UriKind.Absolute, out var endpointUri))
			{
				return "TipOfDay:TableStorage:AccountEndpoint must be a valid absolute URL if provided.";
			}

			if (endpointUri.Scheme is not "https")
			{
				return "TipOfDay:TableStorage:AccountEndpoint must use https.";
			}
		}
		else if (string.IsNullOrWhiteSpace(TableStorage.ConnectionString))
		{
			return "TipOfDay:TableStorage:ConnectionString or AccountEndpoint must be configured.";
		}

		if (string.IsNullOrWhiteSpace(TableStorage.TableName))
		{
			return "TipOfDay:TableStorage:TableName is required.";
		}

		if (!IsValidTableName(TableStorage.TableName))
		{
			return "TipOfDay:TableStorage:TableName must be a valid Azure Table name (3-63 characters, start with a letter, letters and numbers only).";
		}

		if (string.IsNullOrWhiteSpace(TableStorage.PartitionKey))
		{
			return "TipOfDay:TableStorage:PartitionKey is required.";
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
}

public sealed class TipOfDayTableStorageOptions
{
	public string? ConnectionString { get; init; }
	public string AccountEndpoint { get; init; } = string.Empty;
	public string TableName { get; init; } = "tipofthedaytips";
	public string PartitionKey { get; init; } = "tip-of-the-day";
}

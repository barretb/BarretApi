namespace BarretApi.Core.Configuration;

public sealed class BlogPromotionOptions
{
	public const string SectionName = "BlogPromotion";

	public string FeedUrl { get; init; } = string.Empty;
	public int RecentDaysWindow { get; init; } = 7;
	public bool EnableReminderPosts { get; init; } = false;
	public int ReminderDelayHours { get; init; } = 24;
	public BlogPromotionTableStorageOptions TableStorage { get; init; } = new();

	public string? Validate()
	{
		if (!Uri.TryCreate(FeedUrl, UriKind.Absolute, out var feedUri))
		{
			return "BlogPromotion:FeedUrl must be a valid absolute URL.";
		}

		if (feedUri.Scheme is not ("http" or "https"))
		{
			return "BlogPromotion:FeedUrl must use http or https.";
		}

		if (RecentDaysWindow <= 0)
		{
			return "BlogPromotion:RecentDaysWindow must be greater than zero.";
		}

		if (ReminderDelayHours <= 0)
		{
			return "BlogPromotion:ReminderDelayHours must be greater than zero.";
		}

		if (!Uri.TryCreate(TableStorage.AccountEndpoint, UriKind.Absolute, out var endpointUri))
		{
			if (string.IsNullOrWhiteSpace(TableStorage.ConnectionString))
			{
				return "BlogPromotion:TableStorage:AccountEndpoint must be a valid absolute URL when ConnectionString is not set.";
			}
		}
		else if (endpointUri.Scheme is not "https")
		{
			return "BlogPromotion:TableStorage:AccountEndpoint must use https when ConnectionString is not set.";
		}

		if (string.IsNullOrWhiteSpace(TableStorage.TableName))
		{
			return "BlogPromotion:TableStorage:TableName is required.";
		}

		if (string.IsNullOrWhiteSpace(TableStorage.PartitionKey))
		{
			return "BlogPromotion:TableStorage:PartitionKey is required.";
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

public sealed class BlogPromotionTableStorageOptions
{
	public string? ConnectionString { get; init; }
	public string AccountEndpoint { get; init; } = string.Empty;
	public string TableName { get; init; } = "blogpostpromotions";
	public string PartitionKey { get; init; } = "blog-promotion";
}

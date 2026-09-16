namespace BarretApi.Api.Features.ScheduledPosts;

public sealed class ListScheduledPostsResponse
{
	public required IReadOnlyList<ScheduledPostSummary> Posts { get; init; }
	public string? ContinuationToken { get; init; }
}

public sealed class ScheduledPostSummary
{
	public required string ScheduledPostId { get; init; }
	public required string Status { get; init; }
	public required string Version { get; init; }
	public required string TextPreview { get; init; }
	public DateTimeOffset ScheduledForUtc { get; init; }
	public DateTimeOffset CreatedAtUtc { get; init; }
	public DateTimeOffset? PublishedAtUtc { get; init; }
	public required IReadOnlyList<string> Platforms { get; init; }
	public int AttemptCount { get; init; }
	public int SuccessfulPlatformCount { get; init; }
}

public sealed class ScheduledPostDetailsResponse
{
	public required ScheduledPostSummary Post { get; init; }
	public required string Text { get; init; }
	public required IReadOnlyList<string> Hashtags { get; init; }
	public bool AutoThread { get; init; }
	public required IReadOnlyList<ScheduledPostImageDetails> Images { get; init; }
	public required IReadOnlyList<ScheduledPostDeliveryDetails> Deliveries { get; init; }
	public required IReadOnlyList<DeliveryConfirmationDetails> Confirmations { get; init; }
	public DateTimeOffset? LastAttemptedAtUtc { get; init; }
	public DateTimeOffset? LeaseExpiresAtUtc { get; init; }
	public DateTimeOffset? UpdatedAtUtc { get; init; }
	public string? LastErrorCode { get; init; }
	public string? LastErrorMessage { get; init; }
	public string? LastManagementAction { get; init; }
	public string? ManagementNote { get; init; }
}

public sealed record ScheduledPostImageDetails(string Source, string? Url, string AltText, string? ContentType, string? FileName);

public sealed class ScheduledPostDeliveryDetails
{
	public required string Platform { get; init; }
	public bool Success { get; init; }
	public string? PostId { get; init; }
	public string? PostUrl { get; init; }
	public string? ErrorCode { get; init; }
	public string? ErrorMessage { get; init; }
	public IReadOnlyList<ScheduledPostDeliveryDetails>? ThreadResults { get; init; }
}

public sealed record DeliveryConfirmationDetails(
	string Platform, string PostId, string? PostUrl, DateTimeOffset ConfirmedAtUtc, string Note);

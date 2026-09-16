namespace BarretApi.Core.Models;

public sealed record ScheduledPostsQuery(
	ScheduledPostStatus? Status = null,
	DateTimeOffset? FromUtc = null,
	DateTimeOffset? ToUtc = null,
	int PageSize = 50,
	string? ContinuationToken = null);

public sealed record ScheduledPostsPage(
	IReadOnlyList<ScheduledSocialPostRecord> Posts,
	string? ContinuationToken);

public sealed record UpdateScheduledPostCommand(
	string Version,
	string? Text = null,
	IReadOnlyList<string>? Hashtags = null,
	IReadOnlyList<string>? Platforms = null,
	IReadOnlyList<ImageUrl>? Images = null,
	bool? AutoThread = null,
	DateTimeOffset? ScheduledForUtc = null,
	bool RemoveUploadedImages = false);

public sealed record CancelScheduledPostCommand(string Version, string Note);

public sealed record ConfirmedPostDelivery(string Platform, string PostId, string? PostUrl);

public sealed record ReconcileScheduledPostCommand(
	string Version,
	IReadOnlyList<ConfirmedPostDelivery> PublishedDeliveries,
	string Note);

public sealed record RetryScheduledPostCommand(
	string Version,
	IReadOnlyList<string> Platforms,
	bool ConfirmNotPublished,
	string Note,
	DateTimeOffset? ScheduledForUtc = null);

public sealed record PostDeliveryConfirmation(
	string Platform,
	string PostId,
	string? PostUrl,
	DateTimeOffset ConfirmedAtUtc,
	string Note);

public enum PostManagementOutcome
{
	Success,
	NotFound,
	Conflict
}

public sealed record PostManagementResult(
	PostManagementOutcome Outcome,
	ScheduledSocialPostRecord? Post = null,
	string? Message = null);

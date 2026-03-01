namespace BarretApi.Core.Models;

public enum PromotionPhase
{
	Initial = 0,
	Reminder = 1
}

public sealed class PromotionEntryFailure
{
	public required string EntryIdentity { get; init; }
	public required string CanonicalUrl { get; init; }
	public required PromotionPhase Phase { get; init; }
	public required string Platform { get; init; }
	public required string ErrorCode { get; init; }
	public required string ErrorMessage { get; init; }
}

public sealed class PromotionRunSummary
{
	public required string RunId { get; init; }
	public required DateTimeOffset StartedAtUtc { get; init; }
	public required DateTimeOffset CompletedAtUtc { get; set; }
	public int EntriesEvaluated { get; set; }
	public int NewPostsAttempted { get; set; }
	public int NewPostsSucceeded { get; set; }
	public int ReminderPostsAttempted { get; set; }
	public int ReminderPostsSucceeded { get; set; }
	public int EntriesSkippedAlreadyPosted { get; set; }
	public int EntriesSkippedOutsideWindow { get; set; }
	public List<PromotionEntryFailure> Failures { get; init; } = [];
	public List<RecentBlogPostDetails> LastTwoBlogPosts { get; init; } = [];
}

public sealed class RecentBlogPostDetails
{
	public required string EntryIdentity { get; init; }
	public required string CanonicalUrl { get; init; }
	public required string Title { get; init; }
	public required DateTimeOffset PublishedAtUtc { get; init; }
}

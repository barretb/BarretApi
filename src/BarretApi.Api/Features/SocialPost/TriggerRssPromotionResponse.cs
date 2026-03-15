namespace BarretApi.Api.Features.SocialPost;

public sealed class TriggerRssPromotionResponse
{
    public required string RunId { get; init; }
    public required DateTimeOffset StartedAtUtc { get; init; }
    public required DateTimeOffset CompletedAtUtc { get; init; }
    public int EntriesEvaluated { get; init; }
    public int NewPostsAttempted { get; init; }
    public int NewPostsSucceeded { get; init; }
    public int ReminderPostsAttempted { get; init; }
    public int ReminderPostsSucceeded { get; init; }
    public int EntriesSkippedAlreadyPosted { get; init; }
    public int EntriesSkippedOutsideWindow { get; init; }
    public int EntriesSkippedNoTags { get; init; }
    public List<TriggerRssPromotionFailure> Failures { get; init; } = [];
    public List<TriggerRssPromotionBlogPost> LastTwoBlogPosts { get; init; } = [];
}

public sealed class TriggerRssPromotionBlogPost
{
    public required string EntryIdentity { get; init; }
    public required string CanonicalUrl { get; init; }
    public required string Title { get; init; }
    public required DateTimeOffset PublishedAtUtc { get; init; }
}

public sealed class TriggerRssPromotionFailure
{
    public required string EntryIdentity { get; init; }
    public required string CanonicalUrl { get; init; }
    public required string Phase { get; init; }
    public required string Platform { get; init; }
    public required string ErrorCode { get; init; }
    public required string ErrorMessage { get; init; }
}

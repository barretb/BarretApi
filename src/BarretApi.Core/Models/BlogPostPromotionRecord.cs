namespace BarretApi.Core.Models;

public enum PostAttemptStatus
{
    NotAttempted = 0,
    Succeeded = 1,
    Failed = 2
}

public sealed class BlogPostPromotionRecord
{
    public required string EntryIdentity { get; init; }
    public required string CanonicalUrl { get; set; }
    public required string Title { get; set; }
    public required DateTimeOffset PublishedAtUtc { get; set; }
    public PostAttemptStatus InitialPostStatus { get; set; } = PostAttemptStatus.NotAttempted;
    public DateTimeOffset? InitialPostAttemptedAtUtc { get; set; }
    public DateTimeOffset? InitialPostSucceededAtUtc { get; set; }
    public string? InitialPostResultCode { get; set; }
    public PostAttemptStatus ReminderPostStatus { get; set; } = PostAttemptStatus.NotAttempted;
    public DateTimeOffset? ReminderPostAttemptedAtUtc { get; set; }
    public DateTimeOffset? ReminderPostSucceededAtUtc { get; set; }
    public string? ReminderPostResultCode { get; set; }
    public DateTimeOffset LastProcessedAtUtc { get; set; }
}

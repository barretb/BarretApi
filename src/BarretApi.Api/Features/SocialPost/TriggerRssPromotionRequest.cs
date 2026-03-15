namespace BarretApi.Api.Features.SocialPost;

public sealed class TriggerRssPromotionRequest
{
    public string? FeedUrl { get; init; }
    public string? Header { get; init; }
    public int? RecentDaysWindow { get; init; }
}

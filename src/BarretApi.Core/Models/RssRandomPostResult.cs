namespace BarretApi.Core.Models;

public sealed class RssRandomPostResult
{
    public required BlogFeedEntry SelectedEntry { get; init; }
    public required IReadOnlyList<PlatformPostResult> PlatformResults { get; init; }
}

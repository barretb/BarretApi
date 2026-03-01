namespace BarretApi.Core.Configuration;

public sealed class MastodonOptions
{
    public const string SectionName = "Mastodon";

    public required string InstanceUrl { get; init; }
    public required string AccessToken { get; init; }
}

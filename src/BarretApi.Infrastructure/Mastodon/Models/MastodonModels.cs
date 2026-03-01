using System.Text.Json.Serialization;

namespace BarretApi.Infrastructure.Mastodon.Models;

internal sealed class MastodonInstanceConfig
{
    [JsonPropertyName("configuration")]
    public MastodonInstanceConfiguration? Configuration { get; init; }
}

internal sealed class MastodonInstanceConfiguration
{
    [JsonPropertyName("statuses")]
    public MastodonStatusConfig? Statuses { get; init; }

    [JsonPropertyName("media_attachments")]
    public MastodonMediaConfig? MediaAttachments { get; init; }
}

internal sealed class MastodonStatusConfig
{
    [JsonPropertyName("max_characters")]
    public int MaxCharacters { get; init; } = 500;

    [JsonPropertyName("max_media_attachments")]
    public int MaxMediaAttachments { get; init; } = 4;

    [JsonPropertyName("characters_reserved_per_url")]
    public int CharactersReservedPerUrl { get; init; } = 23;
}

internal sealed class MastodonMediaConfig
{
    [JsonPropertyName("description_limit")]
    public int DescriptionLimit { get; init; } = 1_500;

    [JsonPropertyName("image_size_limit")]
    public long ImageSizeLimit { get; init; } = 16_777_216;
}

internal sealed class MastodonStatus
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("url")]
    public string? Url { get; init; }

    [JsonPropertyName("content")]
    public string? Content { get; init; }

    [JsonPropertyName("created_at")]
    public string? CreatedAt { get; init; }
}

internal sealed class MastodonMediaAttachment
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("url")]
    public string? Url { get; init; }

    [JsonPropertyName("type")]
    public string? Type { get; init; }
}

internal sealed class MastodonError
{
    [JsonPropertyName("error")]
    public string? Error { get; init; }

    [JsonPropertyName("error_description")]
    public string? ErrorDescription { get; init; }
}

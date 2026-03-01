using System.Text.Json.Serialization;

namespace BarretApi.Infrastructure.Bluesky.Models;

internal sealed class BlueskySession
{
    [JsonPropertyName("did")]
    public required string Did { get; init; }

    [JsonPropertyName("handle")]
    public required string Handle { get; init; }

    [JsonPropertyName("accessJwt")]
    public required string AccessJwt { get; init; }

    [JsonPropertyName("refreshJwt")]
    public required string RefreshJwt { get; init; }
}

internal sealed class BlueskyCreateRecordRequest
{
    [JsonPropertyName("repo")]
    public required string Repo { get; init; }

    [JsonPropertyName("collection")]
    public required string Collection { get; init; }

    [JsonPropertyName("record")]
    public required BlueskyPostRecord Record { get; init; }
}

internal sealed class BlueskyPostRecord
{
    [JsonPropertyName("$type")]
    public string Type { get; init; } = "app.bsky.feed.post";

    [JsonPropertyName("text")]
    public required string Text { get; init; }

    [JsonPropertyName("createdAt")]
    public required string CreatedAt { get; init; }

    [JsonPropertyName("facets")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<BlueskyFacet>? Facets { get; init; }

    [JsonPropertyName("embed")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Embed { get; init; }
}

internal sealed class BlueskyFacet
{
    [JsonPropertyName("index")]
    public required BlueskyFacetIndex Index { get; init; }

    [JsonPropertyName("features")]
    public required List<BlueskyFacetFeature> Features { get; init; }
}

internal sealed class BlueskyFacetIndex
{
    [JsonPropertyName("byteStart")]
    public required int ByteStart { get; init; }

    [JsonPropertyName("byteEnd")]
    public required int ByteEnd { get; init; }
}

internal sealed class BlueskyFacetFeature
{
    [JsonPropertyName("$type")]
    public required string Type { get; init; }

    [JsonPropertyName("tag")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Tag { get; init; }

    [JsonPropertyName("uri")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Uri { get; init; }

    [JsonPropertyName("did")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Did { get; init; }
}

internal sealed class BlueskyCreateRecordResponse
{
    [JsonPropertyName("uri")]
    public required string Uri { get; init; }

    [JsonPropertyName("cid")]
    public required string Cid { get; init; }
}

internal sealed class BlueskyUploadBlobResponse
{
    [JsonPropertyName("blob")]
    public required BlueskyBlob Blob { get; init; }
}

internal sealed class BlueskyBlob
{
    [JsonPropertyName("$type")]
    public string Type { get; init; } = "blob";

    [JsonPropertyName("ref")]
    public required BlueskyBlobRef Ref { get; init; }

    [JsonPropertyName("mimeType")]
    public required string MimeType { get; init; }

    [JsonPropertyName("size")]
    public required long Size { get; init; }
}

internal sealed class BlueskyBlobRef
{
    [JsonPropertyName("$link")]
    public required string Link { get; init; }
}

internal sealed class BlueskyImageEmbed
{
    [JsonPropertyName("$type")]
    public string Type { get; init; } = "app.bsky.embed.images";

    [JsonPropertyName("images")]
    public required List<BlueskyEmbedImage> Images { get; init; }
}

internal sealed class BlueskyEmbedImage
{
    [JsonPropertyName("alt")]
    public required string Alt { get; init; }

    [JsonPropertyName("image")]
    public required BlueskyBlob Image { get; init; }
}

internal sealed class BlueskyErrorResponse
{
    [JsonPropertyName("error")]
    public string? Error { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }
}

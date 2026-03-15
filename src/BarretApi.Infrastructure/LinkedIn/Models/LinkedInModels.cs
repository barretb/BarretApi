using System.Text.Json.Serialization;

namespace BarretApi.Infrastructure.LinkedIn.Models;

internal sealed class LinkedInRegisterUploadRequest
{
    [JsonPropertyName("registerUploadRequest")]
    public required LinkedInUploadRegistration RegisterUploadRequest { get; init; }
}

internal sealed class LinkedInUploadRegistration
{
    [JsonPropertyName("recipes")]
    public required List<string> Recipes { get; init; }

    [JsonPropertyName("owner")]
    public required string Owner { get; init; }

    [JsonPropertyName("serviceRelationships")]
    public required List<LinkedInServiceRelationship> ServiceRelationships { get; init; }
}

internal sealed class LinkedInServiceRelationship
{
    [JsonPropertyName("relationshipType")]
    public required string RelationshipType { get; init; }

    [JsonPropertyName("identifier")]
    public required string Identifier { get; init; }
}

internal sealed class LinkedInRegisterUploadResponse
{
    [JsonPropertyName("value")]
    public LinkedInRegisterUploadValue? Value { get; init; }
}

internal sealed class LinkedInRegisterUploadValue
{
    [JsonPropertyName("asset")]
    public string? Asset { get; init; }

    [JsonPropertyName("uploadMechanism")]
    public LinkedInUploadMechanism? UploadMechanism { get; init; }
}

internal sealed class LinkedInUploadMechanism
{
    [JsonPropertyName("com.linkedin.digitalmedia.uploading.MediaUploadHttpRequest")]
    public LinkedInUploadHttpRequest? MediaUploadHttpRequest { get; init; }
}

internal sealed class LinkedInUploadHttpRequest
{
    [JsonPropertyName("uploadUrl")]
    public string? UploadUrl { get; init; }
}

internal sealed class LinkedInUgcPostRequest
{
    [JsonPropertyName("author")]
    public required string Author { get; init; }

    [JsonPropertyName("lifecycleState")]
    public string LifecycleState { get; init; } = "PUBLISHED";

    [JsonPropertyName("specificContent")]
    public required LinkedInSpecificContent SpecificContent { get; init; }

    [JsonPropertyName("visibility")]
    public required LinkedInVisibility Visibility { get; init; }
}

internal sealed class LinkedInSpecificContent
{
    [JsonPropertyName("com.linkedin.ugc.ShareContent")]
    public required LinkedInShareContent ShareContent { get; init; }
}

internal sealed class LinkedInShareContent
{
    [JsonPropertyName("shareCommentary")]
    public required LinkedInTextValue ShareCommentary { get; init; }

    [JsonPropertyName("shareMediaCategory")]
    public required string ShareMediaCategory { get; init; }

    [JsonPropertyName("media")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LinkedInMedia>? Media { get; init; }
}

internal sealed class LinkedInTextValue
{
    [JsonPropertyName("text")]
    public required string Text { get; init; }
}

internal sealed class LinkedInMedia
{
    [JsonPropertyName("status")]
    public string Status { get; init; } = "READY";

    [JsonPropertyName("description")]
    public required LinkedInTextValue Description { get; init; }

    [JsonPropertyName("media")]
    public required string MediaUrn { get; init; }

    [JsonPropertyName("title")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LinkedInTextValue? Title { get; init; }
}

internal sealed class LinkedInVisibility
{
    [JsonPropertyName("com.linkedin.ugc.MemberNetworkVisibility")]
    public string MemberNetworkVisibility { get; init; } = "PUBLIC";
}

internal sealed class LinkedInErrorResponse
{
    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("error_description")]
    public string? ErrorDescription { get; init; }

    [JsonPropertyName("serviceErrorCode")]
    public int? ServiceErrorCode { get; init; }
}

internal sealed class LinkedInAccessTokenResponse
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; init; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; init; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; init; }

    [JsonPropertyName("refresh_token_expires_in")]
    public int RefreshTokenExpiresIn { get; init; }
}
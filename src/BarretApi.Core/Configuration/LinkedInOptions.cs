namespace BarretApi.Core.Configuration;

public sealed class LinkedInOptions
{
    public const string SectionName = "LinkedIn";

    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
    public string AuthorUrn { get; init; } = string.Empty;
    public string ApiBaseUrl { get; init; } = "https://api.linkedin.com";
    public string OAuthBaseUrl { get; init; } = "https://www.linkedin.com";
    public LinkedInTokenStorageOptions TokenStorage { get; init; } = new();

    public string? Validate()
    {
        if (string.IsNullOrWhiteSpace(ClientId))
        {
            return "LinkedIn:ClientId is required.";
        }

        if (string.IsNullOrWhiteSpace(ClientSecret))
        {
            return "LinkedIn:ClientSecret is required.";
        }

        if (string.IsNullOrWhiteSpace(AuthorUrn))
        {
            return "LinkedIn:AuthorUrn is required.";
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(AuthorUrn,
            @"^urn:li:(person|member|organization|company):\S+$"))
        {
            return "LinkedIn:AuthorUrn must be a valid URN (e.g., 'urn:li:person:abc123' or 'urn:li:organization:12345').";
        }

        if (!Uri.TryCreate(ApiBaseUrl, UriKind.Absolute, out var apiUri))
        {
            return "LinkedIn:ApiBaseUrl must be a valid absolute URL.";
        }

        if (apiUri.Scheme is not "https")
        {
            return "LinkedIn:ApiBaseUrl must use https.";
        }

        if (!Uri.TryCreate(OAuthBaseUrl, UriKind.Absolute, out var oauthUri))
        {
            return "LinkedIn:OAuthBaseUrl must be a valid absolute URL.";
        }

        if (oauthUri.Scheme is not "https")
        {
            return "LinkedIn:OAuthBaseUrl must use https.";
        }

        return null;
    }
}

public sealed class LinkedInTokenStorageOptions
{
    public string? ConnectionString { get; init; }
    public string AccountEndpoint { get; init; } = string.Empty;
    public string TableName { get; init; } = "linkedintokens";
}
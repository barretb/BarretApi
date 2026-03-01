namespace BarretApi.Api.Features.SocialPost;

public sealed class CreateSocialPostRequest
{
    public string? Text { get; init; }
    public List<string>? Hashtags { get; init; }
    public List<string>? Platforms { get; init; }
    public List<ImageAttachmentRequest>? Images { get; init; }
}

public sealed class ImageAttachmentRequest
{
    public required string Url { get; init; }
    public required string AltText { get; init; }
}

namespace BarretApi.Core.Models;

public static class AvatarFormat
{
    public const string Svg = "svg";
    public const string Png = "png";
    public const string Jpg = "jpg";
    public const string WebP = "webp";
    public const string Avif = "avif";

    public static readonly IReadOnlyList<string> All =
    [
        Svg, Png, Jpg, WebP, Avif
    ];

    private static readonly Dictionary<string, string> ContentTypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        [Svg] = "image/svg+xml",
        [Png] = "image/png",
        [Jpg] = "image/jpeg",
        [WebP] = "image/webp",
        [Avif] = "image/avif"
    };

    public static bool IsValid(string? format)
    {
        return format is not null && ContentTypeMap.ContainsKey(format);
    }

    public static string GetContentType(string format)
    {
        return ContentTypeMap.TryGetValue(format, out var contentType)
            ? contentType
            : throw new ArgumentException($"Unsupported format: {format}", nameof(format));
    }
}

using System.Net.Http.Headers;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using Microsoft.Extensions.Logging;

namespace BarretApi.Infrastructure.Services;

/// <summary>
/// Downloads images from URLs with Content-Type validation and configurable size limits.
/// </summary>
public sealed class ImageDownloadService(
    HttpClient httpClient,
    ILogger<ImageDownloadService> logger) : IImageDownloadService
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly ILogger<ImageDownloadService> _logger = logger;

    private const int MaxImageSizeBytes = 1_048_576; // 1 MB

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/gif",
        "image/webp"
    };

    public async Task<ImageData> DownloadAsync(
        string imageUrl,
        string altText,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imageUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(altText);

        _logger.LogInformation("Downloading image from {Url}", imageUrl);

        using var response = await _httpClient.GetAsync(imageUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";

        if (!AllowedContentTypes.Contains(contentType))
        {
            throw new InvalidOperationException(
                $"Unsupported image content type '{contentType}'. Allowed: {string.Join(", ", AllowedContentTypes)}");
        }

        var contentLength = response.Content.Headers.ContentLength;

        if (contentLength > MaxImageSizeBytes)
        {
            throw new InvalidOperationException(
                $"Image exceeds maximum size of {MaxImageSizeBytes} bytes (reported: {contentLength} bytes)");
        }

        var imageBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);

        if (imageBytes.Length > MaxImageSizeBytes)
        {
            throw new InvalidOperationException(
                $"Image exceeds maximum size of {MaxImageSizeBytes} bytes (actual: {imageBytes.Length} bytes)");
        }

        var fileName = ExtractFileName(imageUrl, contentType);

        _logger.LogInformation(
            "Downloaded image: {FileName} ({ContentType}, {Size} bytes)",
            fileName,
            contentType,
            imageBytes.Length);

        return new ImageData
        {
            Content = imageBytes,
            ContentType = contentType,
            AltText = altText,
            FileName = fileName
        };
    }

    private static string ExtractFileName(string url, string contentType)
    {
        try
        {
            var uri = new Uri(url);
            var path = uri.AbsolutePath;
            var lastSlash = path.LastIndexOf('/');

            if (lastSlash >= 0 && lastSlash < path.Length - 1)
            {
                return path[(lastSlash + 1)..];
            }
        }
        catch
        {
            // Fall through to default
        }

        var extension = contentType switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            _ => ".bin"
        };

        return $"image{extension}";
    }
}

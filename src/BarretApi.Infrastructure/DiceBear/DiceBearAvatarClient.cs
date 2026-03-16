using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using Microsoft.Extensions.Logging;

namespace BarretApi.Infrastructure.DiceBear;

public sealed class DiceBearAvatarClient(
    HttpClient httpClient,
    ILogger<DiceBearAvatarClient> logger) : IDiceBearAvatarClient
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly ILogger<DiceBearAvatarClient> _logger = logger;

    public async Task<AvatarResult> GetAvatarAsync(
        string? style = null,
        string? format = null,
        string? seed = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedStyle = style ?? AvatarStyle.GetRandom();
        var resolvedFormat = format ?? AvatarFormat.Svg;
        var resolvedSeed = seed ?? Guid.NewGuid().ToString("N");

        var url = BuildUrl(resolvedStyle, resolvedFormat, resolvedSeed);
        _logger.LogInformation("Fetching avatar from {Url}", url);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync(url, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to connect to DiceBear API at {Url}", url);
            throw new InvalidOperationException(
                "The avatar generation service is temporarily unavailable. Please try again later.", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var statusCode = (int)response.StatusCode;
            _logger.LogWarning(
                "DiceBear API returned {StatusCode} for {Url}",
                statusCode, url);
            throw new InvalidOperationException(
                "The avatar generation service is temporarily unavailable. Please try again later.");
        }

        var imageBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var contentType = AvatarFormat.GetContentType(resolvedFormat);

        _logger.LogInformation(
            "Avatar fetched: {Style}/{Format}, seed={Seed}, {Size} bytes",
            resolvedStyle, resolvedFormat, resolvedSeed, imageBytes.Length);

        return new AvatarResult
        {
            ImageBytes = imageBytes,
            ContentType = contentType,
            Style = resolvedStyle,
            Seed = resolvedSeed,
            Format = resolvedFormat
        };
    }

    private static string BuildUrl(string style, string format, string seed)
    {
        var encodedSeed = Uri.EscapeDataString(seed);
        return $"9.x/{style}/{format}?seed={encodedSeed}";
    }
}

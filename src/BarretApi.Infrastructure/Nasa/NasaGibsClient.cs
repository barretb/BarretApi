using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BarretApi.Infrastructure.Nasa;

public sealed class NasaGibsClient(
    HttpClient httpClient,
    IOptions<NasaGibsOptions> options,
    ILogger<NasaGibsClient> logger) : INasaGibsClient
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly NasaGibsOptions _options = options.Value;
    private readonly ILogger<NasaGibsClient> _logger = logger;

    public async Task<GibsSnapshotEntry> GetSnapshotAsync(
        GibsSnapshotRequest request,
        CancellationToken cancellationToken = default)
    {
        var requestUrl = BuildRequestUrl(request);
        _logger.LogInformation("Fetching GIBS snapshot from {Url}", requestUrl);

        var response = await _httpClient.GetAsync(requestUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
        if (contentType.Contains("xml", StringComparison.OrdinalIgnoreCase) ||
            contentType.Contains("text", StringComparison.OrdinalIgnoreCase))
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("GIBS snapshot returned an error: {ErrorBody}", errorBody);
            throw new InvalidOperationException(
                $"GIBS snapshot returned an error: {ExtractErrorMessage(errorBody)}");
        }

        var imageBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);

        _logger.LogInformation(
            "Fetched GIBS snapshot: {Layer} {Date}, {ByteCount} bytes, {ContentType}",
            request.Layer,
            request.Date,
            imageBytes.Length,
            contentType);

        return new GibsSnapshotEntry(
            imageBytes,
            request.Date,
            request.Layer,
            request.ImageWidth,
            request.ImageHeight,
            contentType);
    }

    private string BuildRequestUrl(GibsSnapshotRequest request)
    {
        return $"{_options.BaseUrl}" +
            $"?REQUEST=GetSnapshot" +
            $"&SERVICE=WMS" +
            $"&LAYERS={Uri.EscapeDataString(request.Layer)}" +
            $"&CRS=EPSG:4326" +
            $"&BBOX={request.BboxSouth},{request.BboxWest},{request.BboxNorth},{request.BboxEast}" +
            $"&FORMAT=image/jpeg" +
            $"&WIDTH={request.ImageWidth}" +
            $"&HEIGHT={request.ImageHeight}" +
            $"&TIME={request.Date:yyyy-MM-dd}";
    }

    private static string ExtractErrorMessage(string body)
    {
        var startTag = "<ServiceException";
        var endTag = "</ServiceException>";
        var startIndex = body.IndexOf(startTag, StringComparison.OrdinalIgnoreCase);
        var endIndex = body.IndexOf(endTag, StringComparison.OrdinalIgnoreCase);

        if (startIndex >= 0 && endIndex > startIndex)
        {
            var fragment = body[startIndex..(endIndex + endTag.Length)];
            return fragment;
        }

        if (body.Contains("<!doctype", StringComparison.OrdinalIgnoreCase) ||
            body.Contains("<html", StringComparison.OrdinalIgnoreCase))
        {
            return "The GIBS API returned an unexpected HTML response. Verify the configured base URL.";
        }

        return body.Length > 200 ? body[..200] : body;
    }
}

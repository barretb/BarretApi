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
		string layer,
		DateOnly date,
		CancellationToken cancellationToken = default)
	{
		var requestUrl = BuildRequestUrl(layer, date);
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
			layer,
			date,
			imageBytes.Length,
			contentType);

		return new GibsSnapshotEntry(
			imageBytes,
			date,
			layer,
			_options.ImageWidth,
			_options.ImageHeight,
			contentType);
	}

	private string BuildRequestUrl(string layer, DateOnly date)
	{
		return $"{_options.BaseUrl}" +
			$"?REQUEST=GetSnapshot" +
			$"&SERVICE=WMS" +
			$"&LAYERS={Uri.EscapeDataString(layer)}" +
			$"&CRS=EPSG:4326" +
			$"&BBOX={_options.BboxSouth},{_options.BboxWest},{_options.BboxNorth},{_options.BboxEast}" +
			$"&FORMAT=image/jpeg" +
			$"&WIDTH={_options.ImageWidth}" +
			$"&HEIGHT={_options.ImageHeight}" +
			$"&TIME={date:yyyy-MM-dd}";
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

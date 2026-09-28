using System.Text.Json;
using System.Text.Json.Serialization;
using AngleSharp.Html.Parser;
using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BarretApi.Infrastructure.Nasa;

public sealed class NasaApodClient(
	HttpClient httpClient,
	IOptions<NasaApodOptions> options,
	ILogger<NasaApodClient> logger) : INasaApodClient
{
	private readonly HttpClient _httpClient = httpClient;
	private readonly NasaApodOptions _options = options.Value;
	private readonly ILogger<NasaApodClient> _logger = logger;

	public async Task<ApodEntry> GetApodAsync(DateOnly? date, CancellationToken cancellationToken = default)
	{
		var requestUrl = BuildRequestUrl(date);
		_logger.LogInformation("Fetching APOD from {Url}", requestUrl);

		using var response = await _httpClient.GetAsync(requestUrl, cancellationToken);
		response.EnsureSuccessStatusCode();

		var json = await response.Content.ReadAsStringAsync(cancellationToken);
		using var document = JsonDocument.Parse(json);
		var entry = document.RootElement;
		if (entry.ValueKind == JsonValueKind.Array)
		{
			if (entry.GetArrayLength() == 0)
			{
				throw new InvalidOperationException("NASA APOD response contains no entries.");
			}

			entry = entry[0];
		}

		var apiResponse = entry.Deserialize<ApodApiResponse>()
			?? throw new InvalidOperationException("Failed to deserialize NASA APOD response.");

		_logger.LogInformation(
			"Fetched APOD: {Title} ({Date}, {MediaType})",
			apiResponse.Title,
			apiResponse.Date,
			apiResponse.MediaType);

		return MapToApodEntry(apiResponse);
	}

	private string BuildRequestUrl(DateOnly? date)
	{
		var url = _options.BaseUrl.TrimEnd('/');
		return date.HasValue
			? $"{url}/{date.Value:yyMMdd}"
			: $"{url}?per_page=1";
	}

	private static ApodEntry MapToApodEntry(ApodApiResponse response)
	{
		var mediaType = response.MediaType.Equals("video", StringComparison.OrdinalIgnoreCase)
			|| response.MediaType.Equals("iframe", StringComparison.OrdinalIgnoreCase)
			? ApodMediaType.Video
			: ApodMediaType.Image;

		return new ApodEntry
		{
			Title = ToPlainText(response.Title),
			Date = DateOnly.Parse(response.Date),
			Explanation = ToPlainText(response.Explanation),
			Url = mediaType == ApodMediaType.Image
				? response.HdUrl ?? throw new InvalidOperationException("NASA APOD response has no image URL.")
				: response.Url,
			HdUrl = mediaType == ApodMediaType.Image ? response.HdUrl : null,
			MediaType = mediaType,
			Copyright = response.Copyright is null ? null : ToPlainText(response.Copyright),
			ThumbnailUrl = response.ThumbnailUrl ?? (mediaType == ApodMediaType.Video ? response.HdUrl : null)
		};
	}

	private static string ToPlainText(string html)
	{
		using var document = new HtmlParser().ParseDocument(html);
		foreach (var element in document.QuerySelectorAll("br, p, div"))
		{
			element.TextContent = $" {element.TextContent} ";
		}

		return document.Body?.TextContent.Trim() ?? string.Empty;
	}

	private sealed class ApodApiResponse
	{
		[JsonPropertyName("date")]
		public required string Date { get; init; }

		[JsonPropertyName("title")]
		public required string Title { get; init; }

		[JsonPropertyName("explanation")]
		public required string Explanation { get; init; }

		[JsonPropertyName("url")]
		public required string Url { get; init; }

		[JsonPropertyName("hdurl")]
		public string? HdUrl { get; init; }

		[JsonPropertyName("media_type")]
		public required string MediaType { get; init; }

		[JsonPropertyName("copyright")]
		public string? Copyright { get; init; }

		[JsonPropertyName("thumbnail_url")]
		public string? ThumbnailUrl { get; init; }

		[JsonPropertyName("service_version")]
		public string? ServiceVersion { get; init; }
	}
}

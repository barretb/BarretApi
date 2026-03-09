using System.Text.Json;
using System.Text.Json.Serialization;
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

		var response = await _httpClient.GetAsync(requestUrl, cancellationToken);
		response.EnsureSuccessStatusCode();

		var json = await response.Content.ReadAsStringAsync(cancellationToken);
		var apiResponse = JsonSerializer.Deserialize<ApodApiResponse>(json)
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
		var url = $"{_options.BaseUrl}?api_key={_options.ApiKey}&thumbs=True";

		if (date.HasValue)
		{
			url += $"&date={date.Value:yyyy-MM-dd}";
		}

		return url;
	}

	private static ApodEntry MapToApodEntry(ApodApiResponse response)
	{
		var mediaType = response.MediaType.Equals("video", StringComparison.OrdinalIgnoreCase)
			? ApodMediaType.Video
			: ApodMediaType.Image;

		return new ApodEntry
		{
			Title = response.Title,
			Date = DateOnly.Parse(response.Date),
			Explanation = response.Explanation,
			Url = response.Url,
			HdUrl = response.HdUrl,
			MediaType = mediaType,
			Copyright = response.Copyright,
			ThumbnailUrl = response.ThumbnailUrl
		};
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

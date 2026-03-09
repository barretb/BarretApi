using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BarretApi.Core.Services;

public class NasaGibsPostService(
	INasaGibsClient nasaGibsClient,
	SocialPostService socialPostService,
	IOptions<NasaGibsOptions> options,
	ILogger<NasaGibsPostService> logger)
{
	private readonly INasaGibsClient _nasaGibsClient = nasaGibsClient;
	private readonly SocialPostService _socialPostService = socialPostService;
	private readonly NasaGibsOptions _options = options.Value;
	private readonly ILogger<NasaGibsPostService> _logger = logger;

	public virtual async Task<SatellitePostResult> PostAsync(
		DateOnly? date,
		string? layer,
		string? title,
		string? description,
		double? bboxSouth,
		double? bboxWest,
		double? bboxNorth,
		double? bboxEast,
		int? imageWidth,
		int? imageHeight,
		IReadOnlyList<string> platforms,
		CancellationToken cancellationToken = default)
	{
		var resolvedDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
		var resolvedLayer = layer ?? _options.DefaultLayer;
		var resolvedTitle = title ?? "Satellite view of Ohio";
		var resolvedSouth = bboxSouth ?? _options.BboxSouth;
		var resolvedWest = bboxWest ?? _options.BboxWest;
		var resolvedNorth = bboxNorth ?? _options.BboxNorth;
		var resolvedEast = bboxEast ?? _options.BboxEast;
		var resolvedWidth = imageWidth ?? _options.ImageWidth;
		var resolvedHeight = imageHeight ?? _options.ImageHeight;

		_logger.LogInformation(
			"Fetching GIBS snapshot for date {Date}, layer {Layer}",
			resolvedDate.ToString("yyyy-MM-dd"),
			resolvedLayer);

		var snapshotRequest = new GibsSnapshotRequest(
			resolvedLayer,
			resolvedDate,
			resolvedSouth,
			resolvedWest,
			resolvedNorth,
			resolvedEast,
			resolvedWidth,
			resolvedHeight);

		var snapshot = await _nasaGibsClient.GetSnapshotAsync(snapshotRequest, cancellationToken);

		var worldviewUrl = BuildWorldviewUrl(resolvedLayer, resolvedDate, resolvedSouth, resolvedWest, resolvedNorth, resolvedEast);
		var postText = BuildPostText(resolvedTitle, resolvedDate, resolvedLayer, worldviewUrl);
		var altText = description ?? BuildAltText(resolvedDate, resolvedLayer);

		_logger.LogInformation(
			"Posting GIBS snapshot to {PlatformCount} platform(s), image size: {ImageSize} bytes",
			platforms.Count == 0 ? "all" : platforms.Count,
			snapshot.ImageBytes.Length);

		var socialPost = new SocialPost
		{
			Text = postText,
			Hashtags = ["#satellite", "#NASA", "#EarthObservation"],
			Images =
			[
				new ImageData
				{
					Content = snapshot.ImageBytes,
					ContentType = snapshot.ContentType,
					AltText = altText,
					FileName = $"satellite-{resolvedDate:yyyy-MM-dd}.jpg"
				}
			],
			TargetPlatforms = platforms.ToList()
		};

		var platformResults = await _socialPostService.PostAsync(socialPost, cancellationToken);

		return new SatellitePostResult(
			Date: resolvedDate,
			Layer: resolvedLayer,
			Title: resolvedTitle,
			WorldviewUrl: worldviewUrl,
			BboxSouth: resolvedSouth,
			BboxWest: resolvedWest,
			BboxNorth: resolvedNorth,
			BboxEast: resolvedEast,
			ImageWidth: snapshot.Width,
			ImageHeight: snapshot.Height,
			ImageAttached: true,
			ImageResized: false,
			PlatformResults: platformResults);
	}

	private static string BuildWorldviewUrl(string layer, DateOnly date, double south, double west, double north, double east)
	{
		return $"https://worldview.earthdata.nasa.gov/" +
			$"?v={west},{south},{east},{north}" +
			$"&l={layer}" +
			$"&t={date:yyyy-MM-dd}";
	}

	private static string BuildPostText(string title, DateOnly date, string layer, string worldviewUrl)
	{
		return $"{title} \u2014 {date:yyyy-MM-dd}\n" +
			$"Imagery: {layer}\n" +
			$"{worldviewUrl}\n\n" +
			"Imagery: NASA GIBS";
	}

	private static string BuildAltText(DateOnly date, string layer)
	{
		return $"Satellite image of Ohio captured on {date:yyyy-MM-dd} using {layer} imagery from NASA GIBS";
	}
}

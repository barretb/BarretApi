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

	public virtual async Task<OhioSatellitePostResult> PostAsync(
		DateOnly? date,
		string? layer,
		IReadOnlyList<string> platforms,
		CancellationToken cancellationToken = default)
	{
		var resolvedDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
		var resolvedLayer = layer ?? _options.DefaultLayer;

		_logger.LogInformation(
			"Fetching GIBS snapshot for date {Date}, layer {Layer}",
			resolvedDate.ToString("yyyy-MM-dd"),
			resolvedLayer);

		var snapshot = await _nasaGibsClient.GetSnapshotAsync(resolvedLayer, resolvedDate, cancellationToken);

		var worldviewUrl = BuildWorldviewUrl(resolvedLayer, resolvedDate);
		var postText = BuildPostText(resolvedDate, resolvedLayer, worldviewUrl);
		var altText = BuildAltText(resolvedDate, resolvedLayer);

		_logger.LogInformation(
			"Posting GIBS snapshot to {PlatformCount} platform(s), image size: {ImageSize} bytes",
			platforms.Count == 0 ? "all" : platforms.Count,
			snapshot.ImageBytes.Length);

		var socialPost = new SocialPost
		{
			Text = postText,
			Hashtags = ["#Ohio", "#satellite", "#NASA", "#EarthObservation"],
			Images =
			[
				new ImageData
				{
					Content = snapshot.ImageBytes,
					ContentType = snapshot.ContentType,
					AltText = altText,
					FileName = $"ohio-satellite-{resolvedDate:yyyy-MM-dd}.jpg"
				}
			],
			TargetPlatforms = platforms.ToList()
		};

		var platformResults = await _socialPostService.PostAsync(socialPost, cancellationToken);

		return new OhioSatellitePostResult(
			Date: resolvedDate,
			Layer: resolvedLayer,
			WorldviewUrl: worldviewUrl,
			ImageWidth: snapshot.Width,
			ImageHeight: snapshot.Height,
			ImageAttached: true,
			ImageResized: false,
			PlatformResults: platformResults);
	}

	private string BuildWorldviewUrl(string layer, DateOnly date)
	{
		return $"https://worldview.earthdata.nasa.gov/" +
			$"?v={_options.BboxWest},{_options.BboxSouth},{_options.BboxEast},{_options.BboxNorth}" +
			$"&l={layer}" +
			$"&t={date:yyyy-MM-dd}";
	}

	private static string BuildPostText(DateOnly date, string layer, string worldviewUrl)
	{
		return $"Satellite view of Ohio \u2014 {date:yyyy-MM-dd}\n" +
			$"Imagery: {layer}\n" +
			$"{worldviewUrl}\n\n" +
			"Imagery: NASA GIBS";
	}

	private static string BuildAltText(DateOnly date, string layer)
	{
		return $"Satellite image of Ohio captured on {date:yyyy-MM-dd} using {layer} imagery from NASA GIBS";
	}
}

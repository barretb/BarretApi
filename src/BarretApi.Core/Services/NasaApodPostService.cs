using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using Microsoft.Extensions.Logging;

namespace BarretApi.Core.Services;

public class NasaApodPostService(
	INasaApodClient nasaApodClient,
	SocialPostService socialPostService,
	IImageResizer imageResizer,
	ILogger<NasaApodPostService> logger)
{
	private readonly INasaApodClient _nasaApodClient = nasaApodClient;
	private readonly SocialPostService _socialPostService = socialPostService;
	private readonly IImageResizer _imageResizer = imageResizer;
	private readonly ILogger<NasaApodPostService> _logger = logger;

	public virtual async Task<ApodPostResult> PostAsync(
		DateOnly? date,
		IReadOnlyList<string> platforms,
		CancellationToken cancellationToken = default)
	{
		_logger.LogInformation("Fetching NASA APOD for date {Date}", date?.ToString("yyyy-MM-dd") ?? "today");

		var apod = await _nasaApodClient.GetApodAsync(date, cancellationToken);

		_logger.LogInformation(
			"Fetched APOD: {Title} ({Date}, {MediaType})",
			apod.Title,
			apod.Date,
			apod.MediaType);

		var postText = BuildPostText(apod);
		var imageUrl = DetermineImageUrl(apod);
		var imageAttached = imageUrl is not null;

		var socialPost = new SocialPost
		{
			Text = postText,
			ImageUrls = imageUrl is not null
				? [new ImageUrl { Url = imageUrl, AltText = apod.Explanation }]
				: [],
			TargetPlatforms = platforms.ToList()
		};

		_logger.LogInformation(
			"Posting APOD to {PlatformCount} platform(s), image attached: {ImageAttached}",
			platforms.Count == 0 ? "all" : platforms.Count,
			imageAttached);

		var platformResults = await _socialPostService.PostAsync(socialPost, cancellationToken);

		return new ApodPostResult
		{
			ApodEntry = apod,
			PlatformResults = platformResults,
			ImageAttached = imageAttached,
			ImageResized = false
		};
	}

	private static string BuildPostText(ApodEntry apod)
	{
		var linkUrl = apod.HdUrl ?? apod.Url;
		var text = $"{apod.Title}\n{linkUrl}";

		if (!string.IsNullOrWhiteSpace(apod.Copyright))
		{
			text += $"\nCredit: {apod.Copyright}";
		}

		return text;
	}

	private static string? DetermineImageUrl(ApodEntry apod)
	{
		if (apod.MediaType == ApodMediaType.Image)
		{
			return apod.Url;
		}

		return apod.ThumbnailUrl;
	}
}

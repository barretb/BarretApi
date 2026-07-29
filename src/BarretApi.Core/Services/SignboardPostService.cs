using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using Microsoft.Extensions.Logging;

namespace BarretApi.Core.Services;

/// <summary>
/// Generates a signboard image and posts it to social platforms,
/// following the same orchestration shape as NasaGibsPostService.
/// </summary>
public class SignboardPostService(
	ISignboardGenerator signboardGenerator,
	SocialPostService socialPostService,
	ILogger<SignboardPostService> logger)
{
	private readonly ISignboardGenerator _signboardGenerator = signboardGenerator;
	private readonly SocialPostService _socialPostService = socialPostService;
	private readonly ILogger<SignboardPostService> _logger = logger;

	public virtual async Task<SignboardPostResult> PostAsync(
		SignboardGenerationCommand command,
		string? caption,
		IReadOnlyList<string> hashtags,
		string? altText,
		IReadOnlyList<string> platforms,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(command);

		var imageBytes = await _signboardGenerator.GenerateAsync(command, cancellationToken);

		var displayText = command.Text.ReplaceLineEndings(" ");
		var resolvedCaption = string.IsNullOrWhiteSpace(caption) ? command.Text : caption;
		var resolvedAltText = string.IsNullOrWhiteSpace(altText)
			? $"A signboard that reads: {displayText}"
			: altText;

		_logger.LogInformation(
			"Posting signboard to {PlatformCount} platform(s), image size: {ImageSize} bytes",
			platforms.Count == 0 ? "all" : platforms.Count,
			imageBytes.Length);

		var socialPost = new SocialPost
		{
			Text = resolvedCaption,
			Hashtags = hashtags,
			Images =
			[
				new ImageData
				{
					Content = imageBytes,
					ContentType = "image/png",
					AltText = resolvedAltText,
					FileName = "signboard.png"
				}
			],
			TargetPlatforms = platforms.ToList()
		};

		var platformResults = await _socialPostService.PostAsync(socialPost, cancellationToken);

		return new SignboardPostResult(
			Width: command.Width,
			Height: command.Height,
			Seed: command.Seed,
			ImageAttached: true,
			PlatformResults: platformResults);
	}
}

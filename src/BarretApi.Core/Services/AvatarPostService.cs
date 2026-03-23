using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using Microsoft.Extensions.Logging;

namespace BarretApi.Core.Services;

public class AvatarPostService(
	IDiceBearAvatarClient avatarClient,
	SocialPostService socialPostService,
	ILogger<AvatarPostService> logger)
{
	private readonly IDiceBearAvatarClient _avatarClient = avatarClient;
	private readonly SocialPostService _socialPostService = socialPostService;
	private readonly ILogger<AvatarPostService> _logger = logger;

	private const string DefaultFormat = "png";
	private const string DefaultAltText = "A randomly generated DiceBear avatar";

	public virtual async Task<AvatarPostResult> PostAsync(
		string? style,
		string? seed,
		string text,
		string altText,
		IReadOnlyList<string> hashtags,
		IReadOnlyList<string> platforms,
		CancellationToken cancellationToken = default)
	{
		_logger.LogInformation(
			"Generating avatar for social post: style={Style}, seed={Seed}",
			style ?? "(random)",
			seed ?? "(random)");

		var avatarResult = await _avatarClient.GetAvatarAsync(
			style, DefaultFormat, seed, cancellationToken);

		_logger.LogInformation(
			"Avatar generated: style={Style}, seed={Seed}, format={Format}, size={Size} bytes",
			avatarResult.Style,
			avatarResult.Seed,
			avatarResult.Format,
			avatarResult.ImageBytes.Length);

		var resolvedAltText = string.IsNullOrWhiteSpace(altText)
			? DefaultAltText
			: altText;

		var socialPost = new SocialPost
		{
			Text = text,
			Hashtags = hashtags.ToList(),
			TargetPlatforms = platforms.ToList(),
			Images =
			[
				new ImageData
				{
					Content = avatarResult.ImageBytes,
					ContentType = avatarResult.ContentType,
					AltText = resolvedAltText,
					FileName = $"avatar-{avatarResult.Seed}.{avatarResult.Format}"
				}
			]
		};

		_logger.LogInformation(
			"Posting avatar to {PlatformCount} platform(s)",
			platforms.Count == 0 ? "all" : platforms.Count);

		var platformResults = await _socialPostService.PostAsync(socialPost, cancellationToken);

		return new AvatarPostResult
		{
			Style = avatarResult.Style,
			Seed = avatarResult.Seed,
			Format = avatarResult.Format,
			ImageAttached = true,
			PlatformResults = platformResults
		};
	}
}

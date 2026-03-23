using BarretApi.Api.Features.SocialPost;
using BarretApi.Core.Models;
using BarretApi.Core.Services;
using FastEndpoints;
using Microsoft.Extensions.Logging;

namespace BarretApi.Api.Features.Avatar;

public sealed class AvatarPostEndpoint(
	AvatarPostService avatarPostService,
	ILogger<AvatarPostEndpoint> logger)
	: Endpoint<AvatarPostRequest, AvatarPostResponse>
{
	private readonly AvatarPostService _avatarPostService = avatarPostService;
	private readonly ILogger<AvatarPostEndpoint> _logger = logger;

	public override void Configure()
	{
		Post("/api/social-posts/avatar");

		Summary(s =>
		{
			s.Summary = "Post a random avatar to social platforms";
			s.Description = "Generates a random DiceBear avatar and posts it to selected social media platforms.";
			s.ExampleRequest = new AvatarPostRequest
			{
				Style = "pixel-art",
				Seed = "my-seed",
				Text = "Check out this random avatar!",
				AltText = "A pixel-art style avatar",
				Hashtags = ["avatar", "dicebear"],
				Platforms = ["bluesky", "mastodon"]
			};
			s.Responses[200] = "All targeted platforms succeeded.";
			s.Responses[207] = "Partial success: at least one platform succeeded and at least one failed.";
			s.Responses[400] = "Request validation failed.";
			s.Responses[401] = "Missing or invalid X-Api-Key.";
			s.Responses[502] = "All targeted platforms failed or avatar generation failed.";
		});
	}

	public override async Task HandleAsync(AvatarPostRequest req, CancellationToken ct)
	{
		_logger.LogInformation(
			"Avatar post request received: style={Style}, seed={Seed}",
			req.Style ?? "(random)",
			req.Seed ?? "(random)");

		AvatarPostResult result;
		try
		{
			result = await _avatarPostService.PostAsync(
				req.Style,
				req.Seed,
				req.Text ?? string.Empty,
				req.AltText ?? string.Empty,
				req.Hashtags ?? [],
				req.Platforms ?? [],
				ct);
		}
		catch (InvalidOperationException ex)
		{
			_logger.LogError(ex, "Failed to generate avatar for social post");
			await SendErrorResponseAsync(
				$"Failed to generate avatar: {ex.Message}", 502, ct);
			return;
		}

		var response = BuildResponse(result);
		var statusCode = DetermineStatusCode(result.PlatformResults);
		_logger.LogInformation("Avatar post completed with status {StatusCode}", statusCode);

		await Send.ResponseAsync(response, statusCode, ct);
	}

	private static AvatarPostResponse BuildResponse(AvatarPostResult result)
	{
		return new AvatarPostResponse
		{
			Style = result.Style,
			Seed = result.Seed,
			Format = result.Format,
			ImageAttached = result.ImageAttached,
			Results = result.PlatformResults.Select(r => new PlatformResult
			{
				Platform = r.Platform,
				Success = r.Success,
				PostId = r.PostId,
				PostUrl = r.PostUrl,
				ShortenedText = r.PublishedText,
				Error = r.Success ? null : r.ErrorMessage,
				ErrorCode = r.Success ? null : r.ErrorCode
			}).ToList(),
			PostedAt = DateTimeOffset.UtcNow
		};
	}

	private static int DetermineStatusCode(IReadOnlyList<PlatformPostResult> results)
	{
		if (results.All(r => r.Success))
		{
			return 200;
		}

		if (results.Any(r => r.Success))
		{
			return 207;
		}

		return 502;
	}

	private async Task SendErrorResponseAsync(string message, int statusCode, CancellationToken ct)
	{
		HttpContext.Response.StatusCode = statusCode;
		await HttpContext.Response.WriteAsJsonAsync(new { statusCode, message }, ct);
	}
}

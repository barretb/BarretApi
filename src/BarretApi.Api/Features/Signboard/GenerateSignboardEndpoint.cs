using BarretApi.Api.Features.SocialPost;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Core.Services;
using FastEndpoints;
using Microsoft.Extensions.Logging;

namespace BarretApi.Api.Features.Signboard;

public sealed class GenerateSignboardEndpoint(
	ISignboardGenerator generator,
	SignboardPostService postService,
	ILogger<GenerateSignboardEndpoint> logger)
	: Endpoint<GenerateSignboardRequest, GenerateSignboardResponse>
{
	private readonly ISignboardGenerator _generator = generator;
	private readonly SignboardPostService _postService = postService;
	private readonly ILogger<GenerateSignboardEndpoint> _logger = logger;

	private const int DefaultWidth = 1200;
	private const int DefaultHeight = 900;

	public override void Configure()
	{
		Post("/api/signboard");

		Summary(s =>
		{
			s.Summary = "Generate a letterboard signboard image";
			s.Description = "Renders the supplied text as a letterboard-style lightbox sign PNG. "
				+ "Returns the raw PNG by default; when platforms are supplied, posts the image "
				+ "to the targeted social platforms and returns per-platform results as JSON.";
			s.ExampleRequest = new GenerateSignboardRequest
			{
				Text = "I USED TO THINK\nI WAS INDECISIVE\nBUT NOW\nI'M NOT SURE",
				Seed = 42
			};
			s.Responses[200] = "PNG image (no platforms) or all targeted platforms succeeded (JSON).";
			s.Responses[207] = "Partial success: at least one platform succeeded and at least one failed.";
			s.Responses[400] = "Request validation failed.";
			s.Responses[401] = "Missing or invalid X-Api-Key.";
			s.Responses[500] = "Unexpected error during image generation.";
			s.Responses[502] = "All targeted platforms failed to post.";
		});
	}

	public override async Task HandleAsync(GenerateSignboardRequest req, CancellationToken ct)
	{
		var seed = req.Seed ?? Random.Shared.Next();
		var command = new SignboardGenerationCommand(
			Text: req.Text!,
			Width: req.Width ?? DefaultWidth,
			Height: req.Height ?? DefaultHeight,
			Seed: seed);

		var platforms = req.Platforms ?? [];

		_logger.LogInformation(
			"Signboard request: {Width}x{Height}, seed {Seed}, posting: {Posting}",
			command.Width,
			command.Height,
			seed,
			platforms.Count > 0);

		if (platforms.Count == 0)
		{
			await GenerateOnlyAsync(command, ct);
			return;
		}

		SignboardPostResult result;
		try
		{
			result = await _postService.PostAsync(
				command,
				req.Caption,
				req.Hashtags ?? [],
				req.AltText,
				platforms,
				ct);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogError(ex, "Signboard generation failed");
			AddError("An unexpected error occurred during image generation.");
			await Send.ErrorsAsync(500, ct);
			return;
		}

		var response = new GenerateSignboardResponse
		{
			Width = result.Width,
			Height = result.Height,
			Seed = result.Seed,
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

		var statusCode = DetermineStatusCode(result.PlatformResults);
		_logger.LogInformation("Signboard post completed with status {StatusCode}", statusCode);

		await Send.ResponseAsync(response, statusCode, ct);
	}

	private async Task GenerateOnlyAsync(SignboardGenerationCommand command, CancellationToken ct)
	{
		byte[] imageBytes;
		try
		{
			imageBytes = await _generator.GenerateAsync(command, ct);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogError(ex, "Signboard generation failed");
			AddError("An unexpected error occurred during image generation.");
			await Send.ErrorsAsync(500, ct);
			return;
		}

		_logger.LogInformation("Signboard generated: {Size} bytes", imageBytes.Length);

		HttpContext.Response.Headers["X-Signboard-Seed"] = command.Seed.ToString();
		HttpContext.Response.ContentType = "image/png";
		HttpContext.Response.ContentLength = imageBytes.Length;
		await HttpContext.Response.Body.WriteAsync(imageBytes, ct);
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
}

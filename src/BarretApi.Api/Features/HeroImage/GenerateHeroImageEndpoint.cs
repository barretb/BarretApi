using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using FastEndpoints;
using Microsoft.Extensions.Logging;

namespace BarretApi.Api.Features.HeroImage;

public sealed class GenerateHeroImageEndpoint(
	IHeroImageGenerator generator,
	ILogger<GenerateHeroImageEndpoint> logger)
	: Endpoint<GenerateHeroImageRequest>
{
	private readonly IHeroImageGenerator _generator = generator;
	private readonly ILogger<GenerateHeroImageEndpoint> _logger = logger;

	public override void Configure()
	{
		Post("/api/hero-image");
		AllowFileUploads();

		Summary(s =>
		{
			s.Summary = "Generate a branded hero image";
			s.Description = "Composites a 1280×720 PNG hero image with the provided title, optional subtitle, "
				+ "face (lower-right), logo (lower-left), and a faded background. "
				+ "If no background image is uploaded the built-in generic background is used.";
			s.ExampleRequest = new GenerateHeroImageRequest
			{
				Title = "Getting Started with .NET 10",
				Subtitle = "A practical guide for C# developers"
			};
			s.Responses[200] = "Hero image PNG generated successfully (1280×720).";
			s.Responses[400] = "Request validation failed (missing title, overlong text, invalid background file).";
			s.Responses[422] = "Uploaded background image could not be decoded as a valid image.";
			s.Responses[500] = "Unexpected server error during image generation.";
		});
	}

	public override async Task HandleAsync(GenerateHeroImageRequest req, CancellationToken ct)
	{
		var correlationId = Guid.NewGuid().ToString("N")[..8];

		using var scope = _logger.BeginScope(new Dictionary<string, object>
		{
			["CorrelationId"] = correlationId,
			["Title"] = req.Title ?? string.Empty
		});

		_logger.LogInformation("Hero image generation requested");

		byte[]? customBackgroundBytes = null;

		if (req.BackgroundImage is not null)
		{
			using var ms = new MemoryStream();
			await req.BackgroundImage.CopyToAsync(ms, ct);
			customBackgroundBytes = ms.ToArray();
		}

		var command = new HeroImageGenerationCommand(
			Title: req.Title!,
			Subtitle: string.IsNullOrWhiteSpace(req.Subtitle) ? null : req.Subtitle,
			CustomBackgroundBytes: customBackgroundBytes);

		byte[] imageBytes;
		try
		{
			imageBytes = await _generator.GenerateAsync(command, ct);
		}
		catch (InvalidOperationException ex) when (ex.Message.Contains("decode", StringComparison.OrdinalIgnoreCase))
		{
			_logger.LogWarning("Uploaded background image could not be decoded: {Message}", ex.Message);
			await SendErrorResponseAsync("The uploaded background image could not be decoded as a valid image.", 422, ct);
			return;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Hero image generation failed");
			await SendErrorResponseAsync("An unexpected error occurred during image generation.", 500, ct);
			return;
		}

		_logger.LogInformation("Hero image generated successfully: {Size} bytes", imageBytes.Length);

		HttpContext.Response.ContentType = "image/png";
		HttpContext.Response.ContentLength = imageBytes.Length;
		await HttpContext.Response.Body.WriteAsync(imageBytes, ct);
	}

	private async Task SendErrorResponseAsync(string message, int statusCode, CancellationToken ct)
	{
		await Send.ResponseAsync(new { statusCode, message }, statusCode, ct);
	}
}

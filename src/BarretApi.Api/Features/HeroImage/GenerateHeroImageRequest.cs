using Microsoft.AspNetCore.Http;

namespace BarretApi.Api.Features.HeroImage;

public sealed class GenerateHeroImageRequest
{
	public string? Title { get; init; }

	public string? Subtitle { get; init; }

	public IFormFile? BackgroundImage { get; init; }
}

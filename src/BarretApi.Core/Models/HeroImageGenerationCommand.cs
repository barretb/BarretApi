namespace BarretApi.Core.Models;

/// <summary>
/// Use-case input for generating a hero image.
/// </summary>
public sealed record HeroImageGenerationCommand(
	string Title,
	string? Subtitle = null,
	byte[]? CustomBackgroundBytes = null);

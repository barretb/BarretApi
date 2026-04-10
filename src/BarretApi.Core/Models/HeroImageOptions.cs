namespace BarretApi.Core.Models;

/// <summary>
/// Resolved configuration for hero image generation — asset file paths and output dimensions.
/// </summary>
public sealed class HeroImageOptions
{
	public const string SectionName = "HeroImage";

	public string FaceImagePath { get; set; } = string.Empty;

	public string LogoImagePath { get; set; } = string.Empty;

	public string DefaultBackgroundPath { get; set; } = string.Empty;

	public int OutputWidth { get; set; } = 1280;

	public int OutputHeight { get; set; } = 720;
}

namespace BarretApi.Core.Models;

/// <summary>
/// Input for signboard image generation. Text is rendered uppercase;
/// Seed drives deterministic accent-letter placement and jitter.
/// </summary>
public sealed record SignboardGenerationCommand(
	string Text,
	int Width,
	int Height,
	int Seed);

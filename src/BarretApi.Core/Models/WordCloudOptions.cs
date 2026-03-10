namespace BarretApi.Core.Models;

/// <summary>
/// Resolved configuration for word cloud image generation.
/// </summary>
public sealed class WordCloudOptions
{
    public int Width { get; init; } = 800;

    public int Height { get; init; } = 600;

    public int MaxWords { get; init; } = 100;

    public int MinFontSize { get; init; } = 10;

    public int MaxFontSize { get; init; } = 64;
}

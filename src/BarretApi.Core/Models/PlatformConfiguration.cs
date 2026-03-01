namespace BarretApi.Core.Models;

public sealed class PlatformConfiguration
{
    public required string Name { get; init; }
    public required int MaxCharacters { get; init; }
    public int MaxImages { get; init; } = 4;
    public long MaxImageSizeBytes { get; init; } = 1_048_576;
    public int MaxAltTextLength { get; init; } = 1_500;
}

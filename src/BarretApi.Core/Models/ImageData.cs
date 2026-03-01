namespace BarretApi.Core.Models;

public sealed class ImageData
{
    public required byte[] Content { get; init; }
    public required string ContentType { get; init; }
    public required string AltText { get; init; }
    public string? FileName { get; init; }
}

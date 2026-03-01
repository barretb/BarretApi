namespace BarretApi.Core.Models;

public sealed class UploadedImage
{
    public required string PlatformImageId { get; init; }
    public required string AltText { get; init; }
    public object? PlatformData { get; init; }
}

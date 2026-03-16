namespace BarretApi.Core.Models;

public sealed class AvatarResult
{
    public required byte[] ImageBytes { get; init; }

    public required string ContentType { get; init; }

    public required string Style { get; init; }

    public required string Seed { get; init; }

    public required string Format { get; init; }
}

namespace BarretApi.Api.Features.Avatar;

public sealed class GenerateAvatarRequest
{
    public string? Style { get; init; }

    public string? Format { get; init; }

    public string? Seed { get; init; }
}

namespace BarretApi.Api.Features.WordCloud;

public sealed class GenerateWordCloudRequest
{
    public string? Url { get; init; }

    public int? Width { get; init; }

    public int? Height { get; init; }
}

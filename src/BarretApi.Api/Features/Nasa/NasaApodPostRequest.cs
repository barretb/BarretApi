namespace BarretApi.Api.Features.Nasa;

public sealed class NasaApodPostRequest
{
    public string? Date { get; init; }
    public List<string>? Platforms { get; init; }
}

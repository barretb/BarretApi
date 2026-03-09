namespace BarretApi.Core.Configuration;

/// <summary>
/// Configuration for the NASA APOD API client.
/// </summary>
public sealed class NasaApodOptions
{
	public const string SectionName = "NasaApod";

	public required string ApiKey { get; init; }
	public string BaseUrl { get; init; } = "https://api.nasa.gov/planetary/apod";
}

namespace BarretApi.Core.Configuration;

/// <summary>
/// Configuration for the NASA APOD API client.
/// </summary>
public sealed class NasaApodOptions
{
	public const string SectionName = "NasaApod";

	public string BaseUrl { get; init; } = "https://science.nasa.gov/wp-json/wp/v2/apod-basic";
}

namespace BarretApi.Core.Interfaces;

/// <summary>
/// Fetches a web page by URL and extracts visible text content.
/// </summary>
public interface IHtmlTextExtractor
{
    /// <summary>
    /// Fetches the URL, parses the HTML, strips non-visible elements, and returns visible text.
    /// </summary>
    /// <param name="url">The absolute HTTP or HTTPS URL to fetch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The visible text content of the web page.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="url"/> is null or empty.</exception>
    /// <exception cref="HttpRequestException">Thrown when the page cannot be fetched.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the content type is not HTML.</exception>
    Task<string> ExtractTextAsync(string url, CancellationToken cancellationToken = default);
}

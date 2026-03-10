using BarretApi.Core.Models;

namespace BarretApi.Core.Interfaces;

/// <summary>
/// Generates a word cloud PNG image from word frequencies.
/// </summary>
public interface IWordCloudGenerator
{
    /// <summary>
    /// Renders a word cloud image from the given frequencies and returns the PNG bytes.
    /// </summary>
    /// <param name="frequencies">The word frequency data to render.</param>
    /// <param name="options">Image generation options (dimensions, font sizes, max words).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A byte array containing the PNG image data.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="frequencies"/> is null or empty.</exception>
    Task<byte[]> GenerateAsync(
        IReadOnlyList<WordFrequency> frequencies,
        WordCloudOptions options,
        CancellationToken cancellationToken = default);
}

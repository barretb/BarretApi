namespace BarretApi.Core.Interfaces;

/// <summary>
/// Service for shortening text to fit within platform character limits.
/// </summary>
public interface ITextShorteningService
{
    /// <summary>
    /// Shortens the given text to fit within the specified maximum grapheme cluster count.
    /// Truncates at word boundaries and appends a Unicode ellipsis (U+2026) when shortened.
    /// </summary>
    /// <param name="text">The original text to potentially shorten.</param>
    /// <param name="maxGraphemeClusters">The maximum number of grapheme clusters allowed.</param>
    /// <returns>The original text if within limits, or a shortened version with ellipsis.</returns>
    string Shorten(string text, int maxGraphemeClusters);
}

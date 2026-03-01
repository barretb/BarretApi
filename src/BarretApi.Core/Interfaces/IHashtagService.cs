namespace BarretApi.Core.Interfaces;

/// <summary>
/// Service for processing hashtags: parsing inline hashtags from text,
/// merging with a separate list, de-duplicating, and auto-prefixing.
/// </summary>
public interface IHashtagService
{
    /// <summary>
    /// Processes hashtags by parsing inline hashtags from the text,
    /// merging with the provided separate hashtag list, performing
    /// case-insensitive de-duplication, and auto-prefixing with #.
    /// </summary>
    /// <param name="text">The post text that may contain inline hashtags.</param>
    /// <param name="separateHashtags">Additional hashtags provided as a separate list.</param>
    /// <returns>A result containing the clean text and the merged, de-duplicated hashtag list.</returns>
    HashtagProcessingResult ProcessHashtags(string text, IReadOnlyList<string> separateHashtags);
}

/// <summary>
/// Result of hashtag processing containing the final text with hashtags appended.
/// </summary>
public sealed class HashtagProcessingResult
{
    /// <summary>
    /// The final text with all unique hashtags appended at the end.
    /// </summary>
    public required string FinalText { get; init; }

    /// <summary>
    /// All unique hashtags found (both inline and from the separate list), with # prefix.
    /// </summary>
    public required IReadOnlyList<string> AllHashtags { get; init; }
}

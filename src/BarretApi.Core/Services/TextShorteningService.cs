using System.Globalization;
using System.Text.RegularExpressions;
using BarretApi.Core.Interfaces;

namespace BarretApi.Core.Services;

/// <summary>
/// Shortens text to fit within a maximum grapheme cluster count,
/// truncating at word boundaries and appending a Unicode ellipsis (U+2026).
/// When trailing hashtags are present, removes them from the end first
/// before truncating body text (per FR-007).
/// </summary>
public sealed partial class TextShorteningService : ITextShorteningService
{
    private const string Ellipsis = "\u2026";
    private const int EllipsisGraphemeCount = 1;

    [GeneratedRegex(@"(\s+#\w+)+\s*$", RegexOptions.Compiled)]
    private static partial Regex TrailingHashtagsPattern();

    public string Shorten(string text, int maxGraphemeClusters)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxGraphemeClusters, 1);

        var graphemeCount = CountGraphemeClusters(text);

        if (graphemeCount <= maxGraphemeClusters)
        {
            return text;
        }

        // Split into body and trailing hashtags
        var (body, trailingHashtags) = SplitTrailingHashtags(text);

        if (trailingHashtags.Count > 0)
        {
            // Try removing hashtags from the end first
            var result = ShortenByRemovingTrailingHashtags(body, trailingHashtags, maxGraphemeClusters);

            if (result is not null)
            {
                return result;
            }
        }

        // If still too long after removing all trailing hashtags, truncate the body
        return TruncateWithEllipsis(body.TrimEnd(), maxGraphemeClusters);
    }

    private static (string Body, List<string> TrailingHashtags) SplitTrailingHashtags(string text)
    {
        var match = TrailingHashtagsPattern().Match(text);

        if (!match.Success)
        {
            return (text, []);
        }

        var body = text[..match.Index];
        var hashtagPart = match.Value.Trim();
        var hashtags = hashtagPart.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();

        return (body, hashtags);
    }

    private static string? ShortenByRemovingTrailingHashtags(
        string body,
        List<string> trailingHashtags,
        int maxGraphemeClusters)
    {
        // Try with progressively fewer hashtags
        for (var removeCount = 1; removeCount <= trailingHashtags.Count; removeCount++)
        {
            var remainingHashtags = trailingHashtags[..^removeCount];
            var candidate = remainingHashtags.Count > 0
                ? $"{body.TrimEnd()} {string.Join(" ", remainingHashtags)}"
                : body.TrimEnd();

            if (CountGraphemeClusters(candidate) <= maxGraphemeClusters)
            {
                return candidate;
            }
        }

        return null;
    }

    private static string TruncateWithEllipsis(string text, int maxGraphemeClusters)
    {
        var truncateTarget = maxGraphemeClusters - EllipsisGraphemeCount;

        if (truncateTarget <= 0)
        {
            return Ellipsis;
        }

        var truncated = TruncateToGraphemeClusters(text, truncateTarget);
        var wordBoundary = FindLastWordBoundary(truncated);

        if (wordBoundary > 0)
        {
            truncated = truncated[..wordBoundary].TrimEnd();
        }

        return truncated + Ellipsis;
    }

    private static int CountGraphemeClusters(string text)
    {
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        var count = 0;

        while (enumerator.MoveNext())
        {
            count++;
        }

        return count;
    }

    private static string TruncateToGraphemeClusters(string text, int maxClusters)
    {
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        var count = 0;
        var endIndex = 0;

        while (enumerator.MoveNext() && count < maxClusters)
        {
            count++;
            endIndex = enumerator.ElementIndex + enumerator.GetTextElement().Length;
        }

        return text[..endIndex];
    }

    private static int FindLastWordBoundary(string text)
    {
        for (var i = text.Length - 1; i >= 0; i--)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                return i;
            }
        }

        return -1;
    }
}

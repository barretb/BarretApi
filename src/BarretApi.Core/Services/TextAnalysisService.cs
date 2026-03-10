using System.Text.RegularExpressions;
using BarretApi.Core.Models;

namespace BarretApi.Core.Services;

/// <summary>
/// Processes raw text into ranked word frequencies by tokenizing, normalizing,
/// filtering stop words, counting occurrences, and returning the top N words.
/// </summary>
public sealed partial class TextAnalysisService
{
    /// <summary>
    /// Analyzes the given text and returns the top words by frequency.
    /// </summary>
    /// <param name="text">The raw text to analyze.</param>
    /// <param name="maxWords">Maximum number of words to return.</param>
    /// <returns>A list of word frequencies sorted by count descending.</returns>
    public IReadOnlyList<WordFrequency> AnalyzeText(string text, int maxWords)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var wordCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var tokens = TokenPattern().Split(text);

        foreach (var token in tokens)
        {
            var cleaned = StripPunctuation(token).ToLowerInvariant();

            if (cleaned.Length < 3)
            {
                continue;
            }

            if (EnglishStopWords.IsStopWord(cleaned))
            {
                continue;
            }

            if (wordCounts.TryGetValue(cleaned, out var count))
            {
                wordCounts[cleaned] = count + 1;
            }
            else
            {
                wordCounts[cleaned] = 1;
            }
        }

        return wordCounts
            .OrderByDescending(kvp => kvp.Value)
            .Take(maxWords)
            .Select(kvp => new WordFrequency(kvp.Key, kvp.Value))
            .ToList();
    }

    private static string StripPunctuation(string word)
    {
        var span = word.AsSpan();
        var start = 0;
        var end = span.Length - 1;

        while (start <= end && char.IsPunctuation(span[start]))
        {
            start++;
        }

        while (end >= start && char.IsPunctuation(span[end]))
        {
            end--;
        }

        if (start > end)
        {
            return string.Empty;
        }

        return span[start..(end + 1)].ToString();
    }

    [GeneratedRegex(@"[\s\r\n\t]+", RegexOptions.Compiled)]
    private static partial Regex TokenPattern();
}

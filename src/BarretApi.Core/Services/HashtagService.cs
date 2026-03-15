using System.Text.RegularExpressions;
using BarretApi.Core.Interfaces;

namespace BarretApi.Core.Services;

/// <summary>
/// Processes hashtags by extracting inline hashtags from text, merging with
/// a separate list, performing case-insensitive de-duplication, and auto-prefixing with #.
/// Appends non-inline hashtags to the end of the text.
/// </summary>
public sealed partial class HashtagService : IHashtagService
{
    private static readonly Dictionary<string, string> SpecialMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["C#"] = "csharp",
        [".NET"] = "dotnet",
        ["F#"] = "fsharp",
    };

    [GeneratedRegex(@"(?<=\s|^)#(\w+)", RegexOptions.Compiled)]
    private static partial Regex HashtagPattern();

    [GeneratedRegex(@"[^\w]")]
    private static partial Regex NonWordCharacterPattern();

    public HashtagProcessingResult ProcessHashtags(string text, IReadOnlyList<string> separateHashtags)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(separateHashtags);

        var inlineHashtags = ExtractInlineHashtags(text);
        var allHashtags = MergeAndDeduplicate(inlineHashtags, separateHashtags);
        var newHashtags = GetNewHashtags(inlineHashtags, separateHashtags);
        var finalText = AppendHashtags(text, newHashtags);

        return new HashtagProcessingResult
        {
            FinalText = finalText,
            AllHashtags = allHashtags
        };
    }

    private static List<string> ExtractInlineHashtags(string text)
    {
        var matches = HashtagPattern().Matches(text);
        return matches.Select(m => m.Groups[1].Value).ToList();
    }

    private static IReadOnlyList<string> MergeAndDeduplicate(
        List<string> inlineHashtags,
        IReadOnlyList<string> separateHashtags)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();

        foreach (var tag in inlineHashtags)
        {
            var normalized = NormalizeTag(tag);

            if (seen.Add(normalized))
            {
                result.Add($"#{normalized}");
            }
        }

        foreach (var tag in separateHashtags)
        {
            var normalized = NormalizeTag(tag);

            if (seen.Add(normalized))
            {
                result.Add($"#{normalized}");
            }
        }

        return result;
    }

    private static List<string> GetNewHashtags(
        List<string> inlineHashtags,
        IReadOnlyList<string> separateHashtags)
    {
        var inlineSet = new HashSet<string>(
            inlineHashtags.Select(NormalizeTag),
            StringComparer.OrdinalIgnoreCase);

        var newTags = new List<string>();

        foreach (var tag in separateHashtags)
        {
            var normalized = NormalizeTag(tag);

            if (inlineSet.Add(normalized))
            {
                newTags.Add($"#{normalized}");
            }
        }

        return newTags;
    }

    private static string AppendHashtags(string text, List<string> hashtags)
    {
        if (hashtags.Count == 0)
        {
            return text;
        }

        var suffix = string.Join(" ", hashtags);
        var trimmedText = text.TrimEnd();

        return $"{trimmedText} {suffix}";
    }

    private static string NormalizeTag(string tag)
    {
        var trimmed = tag.TrimStart('#');

        if (SpecialMappings.TryGetValue(trimmed, out var mapped))
        {
            return mapped;
        }

        return NonWordCharacterPattern().Replace(trimmed, "");
    }
}

using System.Text;
using System.Text.RegularExpressions;
using BarretApi.Infrastructure.Bluesky.Models;

namespace BarretApi.Infrastructure.Bluesky;

/// <summary>
/// Detects hashtags in text and generates Bluesky rich text facets
/// with correct UTF-8 byte offsets.
/// </summary>
internal static partial class BlueskyFacetBuilder
{
    [GeneratedRegex(@"(?<=\s|^)#(\w+)", RegexOptions.Compiled)]
    private static partial Regex HashtagPattern();

    /// <summary>
    /// Builds facets for all hashtags found in the text.
    /// Returns null if no hashtags are found.
    /// </summary>
    public static List<BlueskyFacet>? BuildFacets(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var matches = HashtagPattern().Matches(text);

        if (matches.Count == 0)
        {
            return null;
        }

        var facets = new List<BlueskyFacet>();

        foreach (Match match in matches)
        {
            var hashtagWithPrefix = match.Value; // e.g. "#dotnet"
            var tagWithoutPrefix = match.Groups[1].Value; // e.g. "dotnet"

            var byteStart = Encoding.UTF8.GetByteCount(text[..match.Index]);
            var byteEnd = byteStart + Encoding.UTF8.GetByteCount(hashtagWithPrefix);

            facets.Add(new BlueskyFacet
            {
                Index = new BlueskyFacetIndex
                {
                    ByteStart = byteStart,
                    ByteEnd = byteEnd
                },
                Features =
                [
                    new BlueskyFacetFeature
                    {
                        Type = "app.bsky.richtext.facet#tag",
                        Tag = tagWithoutPrefix
                    }
                ]
            });
        }

        return facets;
    }
}

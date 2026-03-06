using System.Text;
using System.Text.RegularExpressions;
using BarretApi.Infrastructure.Bluesky.Models;

namespace BarretApi.Infrastructure.Bluesky;

/// <summary>
/// Detects URLs and hashtags in text and generates Bluesky rich text facets
/// with correct UTF-8 byte offsets.
/// </summary>
internal static partial class BlueskyFacetBuilder
{
	[GeneratedRegex(@"(?<=\s|^)#(\w+)", RegexOptions.Compiled)]
	private static partial Regex HashtagPattern();

	[GeneratedRegex(@"https?://[^\s\)\]\}>""]+", RegexOptions.Compiled)]
	private static partial Regex UrlPattern();

	/// <summary>
	/// Builds facets for all URLs and hashtags found in the text.
	/// Returns null if none are found.
	/// </summary>
	public static List<BlueskyFacet>? BuildFacets(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return null;
		}

		var facets = new List<BlueskyFacet>();

		foreach (Match match in UrlPattern().Matches(text))
		{
			var url = match.Value.TrimEnd('.', ',', ';', ':', '!', '?');
			var byteStart = Encoding.UTF8.GetByteCount(text[..match.Index]);
			var byteEnd = byteStart + Encoding.UTF8.GetByteCount(url);

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
						Type = "app.bsky.richtext.facet#link",
						Uri = url
					}
				]
			});
		}

		foreach (Match match in HashtagPattern().Matches(text))
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

		return facets.Count > 0 ? facets : null;
	}
}

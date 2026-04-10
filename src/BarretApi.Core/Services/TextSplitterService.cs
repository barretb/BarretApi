using System.Globalization;
using BarretApi.Core.Interfaces;

namespace BarretApi.Core.Services;

/// <summary>
/// Splits text into segments that each fit within a grapheme cluster limit,
/// breaking at paragraph or word boundaries.
/// </summary>
public sealed class TextSplitterService : ITextSplitterService
{
    public IReadOnlyList<string> Split(string text, int maxLength)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxLength, 1);

        if (CountGraphemeClusters(text) <= maxLength)
        {
            return [text];
        }

        var segments = new List<string>();
        var remaining = text.AsSpan();

        while (!remaining.IsEmpty)
        {
            var chunk = TakeChunk(remaining, maxLength);
            segments.Add(chunk.ToString());
            remaining = remaining[chunk.Length..].TrimStart();
        }

        return segments;
    }

    private static ReadOnlySpan<char> TakeChunk(ReadOnlySpan<char> text, int maxLength)
    {
        if (CountGraphemeClusters(text.ToString()) <= maxLength)
        {
            return text;
        }

        // Find the cut point at maxLength grapheme clusters
        var cutIndex = GraphemeIndexToCharIndex(text.ToString(), maxLength);

        // Prefer to break at a paragraph boundary (\n\n) within the chunk
        var chunk = text[..cutIndex];
        var paraBreak = LastIndexOfDoubleNewline(chunk);
        if (paraBreak > 0)
        {
            return text[..paraBreak];
        }

        // Then try a single newline
        var lineBreak = chunk.LastIndexOf('\n');
        if (lineBreak > 0)
        {
            return text[..lineBreak];
        }

        // Fall back to word boundary
        var wordBreak = LastWordBoundary(chunk);
        if (wordBreak > 0)
        {
            return text[..wordBreak];
        }

        // Hard cut as last resort
        return text[..cutIndex];
    }

    private static int LastIndexOfDoubleNewline(ReadOnlySpan<char> text)
    {
        for (var i = text.Length - 2; i >= 1; i--)
        {
            if (text[i] == '\n' && text[i - 1] == '\n')
            {
                return i - 1;
            }
        }
        return -1;
    }

    private static int LastWordBoundary(ReadOnlySpan<char> text)
    {
        for (var i = text.Length - 1; i > 0; i--)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                return i;
            }
        }
        return -1;
    }

    private static int GraphemeIndexToCharIndex(string text, int graphemeIndex)
    {
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        var charIndex = 0;
        var count = 0;

        while (count < graphemeIndex && enumerator.MoveNext())
        {
            charIndex = enumerator.ElementIndex + ((string)enumerator.Current).Length;
            count++;
        }

        return charIndex;
    }

    private static int CountGraphemeClusters(string text) =>
        new StringInfo(text).LengthInTextElements;
}

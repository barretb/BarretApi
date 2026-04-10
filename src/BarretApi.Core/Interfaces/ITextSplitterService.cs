namespace BarretApi.Core.Interfaces;

public interface ITextSplitterService
{
    /// <summary>
    /// Splits <paramref name="text"/> into segments each no longer than
    /// <paramref name="maxLength"/> grapheme clusters, breaking at paragraph
    /// or word boundaries. Hashtags are preserved on the last segment only.
    /// </summary>
    IReadOnlyList<string> Split(string text, int maxLength);
}

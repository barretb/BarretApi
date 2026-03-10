using System.Collections.Frozen;

namespace BarretApi.Core.Services;

/// <summary>
/// Provides O(1) case-insensitive lookup for common English stop words.
/// </summary>
public static class EnglishStopWords
{
    private static readonly FrozenSet<string> Words = new[]
    {
        "a", "about", "above", "after", "again", "against", "all", "also", "am", "an",
        "and", "any", "are", "aren't", "as", "at", "be", "because", "been", "before",
        "being", "below", "between", "both", "but", "by", "can", "can't", "cannot",
        "com", "could", "couldn't", "did", "didn't", "do", "does", "doesn't", "doing",
        "don't", "down", "during", "each", "else", "even", "every", "few", "find",
        "first", "for", "from", "further", "get", "give", "go", "going", "gone", "got",
        "had", "hadn't", "has", "hasn't", "have", "haven't", "having", "he", "her",
        "here", "hers", "herself", "him", "himself", "his", "how", "however", "http",
        "https", "i", "if", "in", "into", "is", "isn't", "it", "its", "itself", "just",
        "keep", "know", "let", "like", "look", "made", "make", "many", "may", "me",
        "might", "more", "most", "much", "must", "mustn't", "my", "myself", "new", "no",
        "nor", "not", "now", "of", "off", "on", "once", "one", "only", "or", "other",
        "ought", "our", "ours", "ourselves", "out", "over", "own", "per", "put", "quite",
        "rather", "re", "really", "right", "said", "same", "say", "see", "shall",
        "shan't", "she", "should", "shouldn't", "since", "so", "some", "still", "such",
        "take", "tell", "than", "that", "the", "their", "theirs", "them", "themselves",
        "then", "there", "therefore", "these", "they", "thing", "think", "this", "those",
        "though", "through", "time", "to", "too", "two", "under", "until", "up", "upon",
        "us", "use", "used", "using", "very", "want", "was", "wasn't", "way", "we",
        "well", "were", "weren't", "what", "when", "where", "which", "while", "who",
        "whom", "why", "will", "with", "won't", "would", "wouldn't", "www", "yet",
        "you", "your", "yours", "yourself", "yourselves"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Returns true if the given word is a common English stop word.
    /// </summary>
    public static bool IsStopWord(string word)
    {
        return Words.Contains(word);
    }
}

namespace BarretApi.Core.Models;

/// <summary>
/// Represents a word and its occurrence count in extracted text.
/// </summary>
public sealed record WordFrequency(string Word, int Count);

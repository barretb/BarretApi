namespace BarretApi.Core.Services;

/// <summary>
/// Defines the set of characters that can be rendered as letterboard tiles.
/// Shared by request validation and the signboard generator.
/// </summary>
public static class SignboardCharacterSet
{
	public const string SupportedPunctuation = "!?.,'\"&@#$%-+/:;";

	private static readonly HashSet<char> Supported = BuildSupportedSet();

	public static IReadOnlyList<char> FindUnsupported(string text)
	{
		ArgumentNullException.ThrowIfNull(text);

		var unsupported = new List<char>();
		foreach (var c in text)
		{
			if (!Supported.Contains(c) && !unsupported.Contains(c))
			{
				unsupported.Add(c);
			}
		}

		return unsupported;
	}

	private static HashSet<char> BuildSupportedSet()
	{
		var set = new HashSet<char> { ' ', '\n', '\r' };

		for (var c = 'A'; c <= 'Z'; c++)
		{
			set.Add(c);
		}

		for (var c = 'a'; c <= 'z'; c++)
		{
			set.Add(c);
		}

		for (var c = '0'; c <= '9'; c++)
		{
			set.Add(c);
		}

		foreach (var c in SupportedPunctuation)
		{
			set.Add(c);
		}

		return set;
	}
}

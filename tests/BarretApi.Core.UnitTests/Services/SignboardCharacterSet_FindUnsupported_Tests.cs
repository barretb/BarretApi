using BarretApi.Core.Services;
using Shouldly;

namespace BarretApi.Core.UnitTests.Services;

public sealed class SignboardCharacterSet_FindUnsupported_Tests
{
	[Theory]
	[InlineData("HELLO WORLD")]
	[InlineData("hello world")]
	[InlineData("ABC123")]
	[InlineData("WOW! REALLY? YES.")]
	[InlineData("IT'S \"FINE\", OK & DONE @ 5% + #1 $9 - A/B: C;")]
	[InlineData("LINE ONE\nLINE TWO")]
	[InlineData("CRLF\r\nTEXT")]
	public void ReturnsEmpty_GivenOnlySupportedCharacters(string text)
	{
		var result = SignboardCharacterSet.FindUnsupported(text);

		result.ShouldBeEmpty();
	}

	[Theory]
	[InlineData("HELLO_WORLD", '_')]
	[InlineData("CAFÉ", 'É')]
	[InlineData("TAB\tHERE", '\t')]
	[InlineData("(PARENS)", '(')]
	public void ReturnsOffendingCharacter_GivenUnsupportedCharacter(string text, char expected)
	{
		var result = SignboardCharacterSet.FindUnsupported(text);

		result.ShouldContain(expected);
	}

	[Fact]
	public void ReturnsOffendingCharacters_GivenEmoji()
	{
		var result = SignboardCharacterSet.FindUnsupported("EMOJI \U0001F600");

		result.Count.ShouldBe(2);
	}

	[Fact]
	public void ReturnsDistinctCharactersInFirstSeenOrder_GivenRepeatedUnsupportedCharacters()
	{
		var result = SignboardCharacterSet.FindUnsupported("A_B_C~D~");

		result.ShouldBe(new[] { '_', '~' });
	}
}

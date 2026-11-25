using System.Globalization;
using BarretApi.Core.Services;
using Shouldly;

namespace BarretApi.Core.UnitTests.Services;

public sealed class TextShorteningService_Shorten_Tests
{
    private const string Url = "https://devblogs.microsoft.com/visualstudio/beyond-the-prompt-discovering-trusted-technical-perspectives-with-your-visual-studio-subscription/";

    private readonly TextShorteningService _sut = new();

    [Fact]
    public void ReturnsTextUnchanged_GivenTextWithinLimit()
    {
        var text = $"Short title\n{Url} #dotnet";

        var result = _sut.Shorten(text, 500);

        result.ShouldBe(text);
    }

    [Fact]
    public void KeepsFullUrl_GivenBodyWithTrailingUrlExceedingLimit()
    {
        var text = "From the Visual Studio blog...\n\nIn case you missed it earlier...\n\n"
            + "Beyond the Prompt: Discovering trusted technical perspectives with your Visual Studio subscription and much more text here\n"
            + Url;

        var result = _sut.Shorten(text, 300);

        result.ShouldEndWith(Url);
        result.ShouldContain("…\n" + Url);
        result.ShouldStartWith("From the Visual Studio blog...");
        GraphemeCount(result).ShouldBeLessThanOrEqualTo(300);
    }

    [Fact]
    public void DropsHashtagsAndKeepsUrl_GivenTrailingHashtagsAndLongBody()
    {
        var text = "From the Visual Studio blog...\n\nIn case you missed it earlier...\n\n"
            + "Beyond the Prompt: Discovering trusted technical perspectives with your Visual Studio subscription and much more text here\n"
            + Url + "\n\n#VisualStudio #dotnet #AI";

        var result = _sut.Shorten(text, 300);

        result.ShouldEndWith(Url);
        result.ShouldNotContain("#VisualStudio");
        GraphemeCount(result).ShouldBeLessThanOrEqualTo(300);
    }

    [Fact]
    public void KeepsUrlAndSomeHashtags_GivenRemovingHashtagsIsEnough()
    {
        var text = $"Title\n{Url} #one #two #three";

        var result = _sut.Shorten(text, GraphemeCount($"Title\n{Url} #one"));

        result.ShouldBe($"Title\n{Url} #one");
    }

    [Fact]
    public void ReturnsOnlyUrl_GivenUrlConsumesEntireLimit()
    {
        var text = $"Some leading text that cannot fit\n{Url}";

        var result = _sut.Shorten(text, GraphemeCount(Url) + 2);

        result.ShouldBe(Url);
    }

    [Fact]
    public void TruncatesWithEllipsis_GivenTextWithoutUrl()
    {
        var text = "one two three four five six seven eight nine ten";

        var result = _sut.Shorten(text, 20);

        result.ShouldBe("one two three four…");
    }

    private static int GraphemeCount(string text) => new StringInfo(text).LengthInTextElements;
}

using BarretApi.Core.Services;
using Shouldly;

namespace BarretApi.Core.UnitTests.Services;

public sealed class HashtagService_ProcessHashtags_Tests
{
	private readonly HashtagService _sut = new();

	[Fact]
	public void StripsSpacesFromCategoryTags_GivenTagsWithSpaces()
	{
		var result = _sut.ProcessHashtags("Check this out", ["Visual Studio", "ASP.NET Core"]);

		result.AllHashtags.ShouldContain("#VisualStudio");
		result.AllHashtags.ShouldContain("#ASPNETCore");
	}

	[Fact]
	public void StripsPunctuationFromCategoryTags_GivenTagsWithPunctuation()
	{
		var result = _sut.ProcessHashtags("Check this out", ["What's New", "Maintenance & Updates"]);

		result.AllHashtags.ShouldContain("#WhatsNew");
		result.AllHashtags.ShouldContain("#MaintenanceUpdates");
	}

	[Theory]
	[InlineData("C#", "#csharp")]
	[InlineData("c#", "#csharp")]
	[InlineData(".NET", "#dotnet")]
	[InlineData(".net", "#dotnet")]
	[InlineData("F#", "#fsharp")]
	public void MapsSpecialCategories_GivenKnownSpecialNames(string category, string expectedHashtag)
	{
		var result = _sut.ProcessHashtags("Post text", [category]);

		result.AllHashtags.ShouldContain(expectedHashtag);
	}

	[Fact]
	public void AppendsNormalizedHashtagsToText_GivenCategoriesWithSpaces()
	{
		var result = _sut.ProcessHashtags("Check this out", ["Visual Studio"]);

		result.FinalText.ShouldBe("Check this out #VisualStudio");
	}

	[Fact]
	public void AppendsSpecialMappedHashtags_GivenCSharpCategory()
	{
		var result = _sut.ProcessHashtags("New features in", ["C#"]);

		result.FinalText.ShouldBe("New features in #csharp");
	}

	[Fact]
	public void DeduplicatesNormalizedTags_GivenInlineAndSeparateDuplicates()
	{
		var result = _sut.ProcessHashtags("Check #dotnet out", [".NET"]);

		result.AllHashtags.Count.ShouldBe(1);
		result.AllHashtags.ShouldContain("#dotnet");
	}

	[Fact]
	public void HandlesPlainTagsWithoutSpecialCharacters_GivenSimpleTags()
	{
		var result = _sut.ProcessHashtags("Post", ["AI", "Blazor"]);

		result.AllHashtags.ShouldBe(new[] { "#AI", "#Blazor" });
	}
}

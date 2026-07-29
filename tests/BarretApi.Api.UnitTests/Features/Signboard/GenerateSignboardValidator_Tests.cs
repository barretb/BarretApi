using BarretApi.Api.Features.Signboard;
using Shouldly;

namespace BarretApi.Api.UnitTests.Features.Signboard;

public sealed class GenerateSignboardValidator_Tests
{
	private readonly GenerateSignboardValidator _validator = new();

	[Fact]
	public void IsValid_GivenMinimalValidRequest()
	{
		var request = new GenerateSignboardRequest { Text = "HELLO WORLD" };

		var result = _validator.Validate(request);

		result.IsValid.ShouldBeTrue();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void RejectsText_GivenMissingText(string? text)
	{
		var request = new GenerateSignboardRequest { Text = text };

		var result = _validator.Validate(request);

		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.PropertyName == "Text");
	}

	[Fact]
	public void RejectsText_GivenTextOver200Characters()
	{
		var request = new GenerateSignboardRequest { Text = new string('A', 201) };

		var result = _validator.Validate(request);

		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.PropertyName == "Text");
	}

	[Fact]
	public void RejectsText_GivenUnsupportedCharacters()
	{
		var request = new GenerateSignboardRequest { Text = "HELLO_WORLD~" };

		var result = _validator.Validate(request);

		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.PropertyName == "Text");
		result.Errors.ShouldContain(e => e.ErrorMessage.Contains("'_'") && e.ErrorMessage.Contains("'~'"));
	}

	[Theory]
	[InlineData(399)]
	[InlineData(2001)]
	public void RejectsWidth_GivenWidthOutOfRange(int width)
	{
		var request = new GenerateSignboardRequest { Text = "HI", Width = width };

		var result = _validator.Validate(request);

		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.PropertyName == "Width");
	}

	[Theory]
	[InlineData(399)]
	[InlineData(2001)]
	public void RejectsHeight_GivenHeightOutOfRange(int height)
	{
		var request = new GenerateSignboardRequest { Text = "HI", Height = height };

		var result = _validator.Validate(request);

		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.PropertyName == "Height");
	}

	[Fact]
	public void RejectsPlatforms_GivenUnknownPlatform()
	{
		var request = new GenerateSignboardRequest { Text = "HI", Platforms = ["myspace"] };

		var result = _validator.Validate(request);

		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.PropertyName == "Platforms");
	}

	[Fact]
	public void IsValid_GivenAllKnownPlatformsAnyCase()
	{
		var request = new GenerateSignboardRequest { Text = "HI", Platforms = ["Bluesky", "MASTODON", "linkedin"] };

		var result = _validator.Validate(request);

		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public void RejectsCaption_GivenCaptionOver1000Characters()
	{
		var request = new GenerateSignboardRequest { Text = "HI", Caption = new string('a', 1001) };

		var result = _validator.Validate(request);

		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.PropertyName == "Caption");
	}

	[Fact]
	public void RejectsAltText_GivenAltTextOver1500Characters()
	{
		var request = new GenerateSignboardRequest { Text = "HI", AltText = new string('a', 1501) };

		var result = _validator.Validate(request);

		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.PropertyName == "AltText");
	}

	[Fact]
	public void RejectsHashtags_GivenHashtagWithSpaces()
	{
		var request = new GenerateSignboardRequest { Text = "HI", Hashtags = ["has space"] };

		var result = _validator.Validate(request);

		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.PropertyName.StartsWith("Hashtags"));
	}
}

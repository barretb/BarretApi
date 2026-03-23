using BarretApi.Api.Features.Avatar;
using Shouldly;

namespace BarretApi.Api.UnitTests.Features.Avatar;

public sealed class AvatarPostValidator_Style_Tests
{
	private readonly AvatarPostValidator _validator = new();

	[Fact]
	public async Task ReturnsNoError_GivenNullStyle()
	{
		var request = new AvatarPostRequest { Style = null };

		var result = await _validator.ValidateAsync(request);

		result.Errors.ShouldNotContain(e => e.PropertyName == "Style");
	}

	[Theory]
	[InlineData("pixel-art")]
	[InlineData("adventurer")]
	[InlineData("bottts")]
	public async Task ReturnsNoError_GivenValidStyle(string style)
	{
		var request = new AvatarPostRequest { Style = style };

		var result = await _validator.ValidateAsync(request);

		result.Errors.ShouldNotContain(e => e.PropertyName == "Style");
	}

	[Theory]
	[InlineData("invalid")]
	[InlineData("not-a-style")]
	public async Task ReturnsError_GivenInvalidStyle(string style)
	{
		var request = new AvatarPostRequest { Style = style };

		var result = await _validator.ValidateAsync(request);

		result.Errors.ShouldContain(e => e.PropertyName == "Style");
	}
}

public sealed class AvatarPostValidator_Seed_Tests
{
	private readonly AvatarPostValidator _validator = new();

	[Fact]
	public async Task ReturnsNoError_GivenNullSeed()
	{
		var request = new AvatarPostRequest { Seed = null };

		var result = await _validator.ValidateAsync(request);

		result.Errors.ShouldNotContain(e => e.PropertyName == "Seed");
	}

	[Fact]
	public async Task ReturnsError_GivenSeedExceeding256Characters()
	{
		var request = new AvatarPostRequest { Seed = new string('a', 257) };

		var result = await _validator.ValidateAsync(request);

		result.Errors.ShouldContain(e => e.PropertyName == "Seed");
	}
}

public sealed class AvatarPostValidator_Text_Tests
{
	private readonly AvatarPostValidator _validator = new();

	[Fact]
	public async Task ReturnsNoError_GivenNullText()
	{
		var request = new AvatarPostRequest { Text = null };

		var result = await _validator.ValidateAsync(request);

		result.Errors.ShouldNotContain(e => e.PropertyName == "Text");
	}

	[Fact]
	public async Task ReturnsError_GivenTextExceeding10000Characters()
	{
		var request = new AvatarPostRequest { Text = new string('a', 10_001) };

		var result = await _validator.ValidateAsync(request);

		result.Errors.ShouldContain(e => e.PropertyName == "Text");
	}
}

public sealed class AvatarPostValidator_AltText_Tests
{
	private readonly AvatarPostValidator _validator = new();

	[Fact]
	public async Task ReturnsNoError_GivenNullAltText()
	{
		var request = new AvatarPostRequest { AltText = null };

		var result = await _validator.ValidateAsync(request);

		result.Errors.ShouldNotContain(e => e.PropertyName == "AltText");
	}

	[Fact]
	public async Task ReturnsError_GivenAltTextExceeding1500Characters()
	{
		var request = new AvatarPostRequest { AltText = new string('a', 1_501) };

		var result = await _validator.ValidateAsync(request);

		result.Errors.ShouldContain(e => e.PropertyName == "AltText");
	}
}

public sealed class AvatarPostValidator_Platforms_Tests
{
	private readonly AvatarPostValidator _validator = new();

	[Fact]
	public async Task ReturnsNoError_GivenNullPlatforms()
	{
		var request = new AvatarPostRequest { Platforms = null };

		var result = await _validator.ValidateAsync(request);

		result.Errors.ShouldNotContain(e => e.PropertyName == "Platforms");
	}

	[Theory]
	[InlineData("bluesky")]
	[InlineData("mastodon")]
	[InlineData("linkedin")]
	public async Task ReturnsNoError_GivenSupportedPlatform(string platform)
	{
		var request = new AvatarPostRequest { Platforms = [platform] };

		var result = await _validator.ValidateAsync(request);

		result.Errors.ShouldNotContain(e => e.PropertyName == "Platforms");
	}

	[Theory]
	[InlineData("twitter")]
	[InlineData("facebook")]
	public async Task ReturnsError_GivenUnsupportedPlatform(string platform)
	{
		var request = new AvatarPostRequest { Platforms = [platform] };

		var result = await _validator.ValidateAsync(request);

		result.Errors.ShouldContain(e => e.PropertyName == "Platforms");
	}
}

public sealed class AvatarPostValidator_Hashtags_Tests
{
	private readonly AvatarPostValidator _validator = new();

	[Fact]
	public async Task ReturnsNoError_GivenNullHashtags()
	{
		var request = new AvatarPostRequest { Hashtags = null };

		var result = await _validator.ValidateAsync(request);

		result.Errors.ShouldNotContain(e => e.PropertyName.Contains("Hashtags"));
	}

	[Fact]
	public async Task ReturnsError_GivenHashtagWithSpaces()
	{
		var request = new AvatarPostRequest { Hashtags = ["has space"] };

		var result = await _validator.ValidateAsync(request);

		result.Errors.ShouldContain(e => e.PropertyName.Contains("Hashtags"));
	}

	[Fact]
	public async Task ReturnsError_GivenHashtagExceeding100Characters()
	{
		var request = new AvatarPostRequest { Hashtags = [new string('a', 101)] };

		var result = await _validator.ValidateAsync(request);

		result.Errors.ShouldContain(e => e.PropertyName.Contains("Hashtags"));
	}
}

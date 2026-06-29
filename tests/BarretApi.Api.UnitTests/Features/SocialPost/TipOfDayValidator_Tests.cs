using BarretApi.Api.Features.SocialPost;
using Shouldly;

namespace BarretApi.Api.UnitTests.Features.SocialPost;

public sealed class PostTipOfDayValidator_Tests
{
	private readonly PostTipOfDayValidator _validator = new();

	[Fact]
	public async Task ReturnsError_GivenMissingCategory()
	{
		var result = await _validator.ValidateAsync(new PostTipOfDayRequest());

		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.PropertyName == "Category");
	}

	[Theory]
	[InlineData("bluesky")]
	[InlineData("mastodon")]
	[InlineData("linkedin")]
	public async Task ReturnsNoError_GivenSupportedPlatform(string platform)
	{
		var request = new PostTipOfDayRequest
		{
			Category = "dotnet",
			Platforms = [platform]
		};

		var result = await _validator.ValidateAsync(request);

		result.Errors.ShouldNotContain(e => e.PropertyName.Contains("Platforms"));
	}

	[Fact]
	public async Task ReturnsError_GivenUnsupportedPlatform()
	{
		var request = new PostTipOfDayRequest
		{
			Category = "dotnet",
			Platforms = ["twitter"]
		};

		var result = await _validator.ValidateAsync(request);

		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.PropertyName.Contains("Platforms"));
	}
}

public sealed class AddTipOfDayValidator_Tests
{
	private readonly AddTipOfDayValidator _validator = new();

	[Fact]
	public async Task ReturnsError_GivenMissingTip()
	{
		var request = new AddTipOfDayRequest { Category = "dotnet" };

		var result = await _validator.ValidateAsync(request);

		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.PropertyName == "Tip");
	}

	[Fact]
	public async Task ReturnsError_GivenInvalidMoreInfoUrl()
	{
		var request = new AddTipOfDayRequest
		{
			Category = "dotnet",
			Tip = "Use nullable annotations.",
			MoreInfoUrl = "ftp://example.com/tip"
		};

		var result = await _validator.ValidateAsync(request);

		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.PropertyName == "MoreInfoUrl");
	}

	[Fact]
	public async Task ReturnsNoError_GivenValidRequest()
	{
		var request = new AddTipOfDayRequest
		{
			Category = "dotnet",
			Tip = "Use nullable annotations.",
			MoreInfoUrl = "https://example.com/tip"
		};

		var result = await _validator.ValidateAsync(request);

		result.IsValid.ShouldBeTrue();
	}
}

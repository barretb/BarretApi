using BarretApi.Api.Features.HeroImage;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Shouldly;

namespace BarretApi.Api.UnitTests.Features.HeroImage;

public sealed class GenerateHeroImageValidator_Tests
{
	private readonly GenerateHeroImageValidator _validator = new();

	[Fact]
	public async Task ReturnsNoErrors_GivenValidTitleOnly()
	{
		var request = new GenerateHeroImageRequest { Title = "My Blog Post" };

		var result = await _validator.ValidateAsync(request);

		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnsNoErrors_GivenValidTitleAndSubtitle()
	{
		var request = new GenerateHeroImageRequest
		{
			Title = "My Blog Post",
			Subtitle = "Part 1: Getting Started"
		};

		var result = await _validator.ValidateAsync(request);

		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnsErrors_GivenEmptyTitle()
	{
		var request = new GenerateHeroImageRequest { Title = "" };

		var result = await _validator.ValidateAsync(request);

		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.PropertyName == "Title");
	}

	[Fact]
	public async Task ReturnsErrors_GivenNullTitle()
	{
		var request = new GenerateHeroImageRequest { Title = null };

		var result = await _validator.ValidateAsync(request);

		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.PropertyName == "Title");
	}

	[Fact]
	public async Task ReturnsErrors_GivenTitleExceeding200Chars()
	{
		var request = new GenerateHeroImageRequest { Title = new string('A', 201) };

		var result = await _validator.ValidateAsync(request);

		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.PropertyName == "Title");
	}

	[Fact]
	public async Task ReturnsNoErrors_GivenTitleOf200Chars()
	{
		var request = new GenerateHeroImageRequest { Title = new string('A', 200) };

		var result = await _validator.ValidateAsync(request);

		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnsErrors_GivenSubtitleExceeding300Chars()
	{
		var request = new GenerateHeroImageRequest
		{
			Title = "Valid Title",
			Subtitle = new string('B', 301)
		};

		var result = await _validator.ValidateAsync(request);

		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.PropertyName == "Subtitle");
	}

	[Fact]
	public async Task ReturnsNoErrors_GivenSubtitleOf300Chars()
	{
		var request = new GenerateHeroImageRequest
		{
			Title = "Valid Title",
			Subtitle = new string('B', 300)
		};

		var result = await _validator.ValidateAsync(request);

		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnsErrors_GivenBackgroundImageWithInvalidContentType()
	{
		var file = Substitute.For<IFormFile>();
		file.ContentType.Returns("image/gif");
		file.Length.Returns(1024);

		var request = new GenerateHeroImageRequest
		{
			Title = "Valid Title",
			BackgroundImage = file
		};

		var result = await _validator.ValidateAsync(request);

		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.ErrorMessage.Contains("JPEG or PNG"));
	}

	[Theory]
	[InlineData("image/jpeg")]
	[InlineData("image/png")]
	public async Task ReturnsNoErrors_GivenBackgroundImageWithAllowedContentType(string contentType)
	{
		var file = Substitute.For<IFormFile>();
		file.ContentType.Returns(contentType);
		file.Length.Returns(1024);

		var request = new GenerateHeroImageRequest
		{
			Title = "Valid Title",
			BackgroundImage = file
		};

		var result = await _validator.ValidateAsync(request);

		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnsErrors_GivenBackgroundImageOver10MB()
	{
		var file = Substitute.For<IFormFile>();
		file.ContentType.Returns("image/jpeg");
		file.Length.Returns(10_485_761L);

		var request = new GenerateHeroImageRequest
		{
			Title = "Valid Title",
			BackgroundImage = file
		};

		var result = await _validator.ValidateAsync(request);

		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.ErrorMessage.Contains("10 MB"));
	}

	[Fact]
	public async Task ReturnsNoErrors_GivenBackgroundImageExactly10MB()
	{
		var file = Substitute.For<IFormFile>();
		file.ContentType.Returns("image/png");
		file.Length.Returns(10_485_760L);

		var request = new GenerateHeroImageRequest
		{
			Title = "Valid Title",
			BackgroundImage = file
		};

		var result = await _validator.ValidateAsync(request);

		result.IsValid.ShouldBeTrue();
	}
}

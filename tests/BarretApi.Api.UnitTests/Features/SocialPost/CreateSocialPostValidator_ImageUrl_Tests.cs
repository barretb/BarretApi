using BarretApi.Api.Features.SocialPost;
using Shouldly;

namespace BarretApi.Api.UnitTests.Features.SocialPost;

public sealed class CreateSocialPostValidator_ImageUrl_Tests
{
	private readonly CreateSocialPostValidator _validator = new();

	[Fact]
	public async Task ReturnsError_GivenEmptyImageUrl()
	{
		var request = new CreateSocialPostRequest
		{
			Text = "Hello",
			Images = [new ImageAttachmentRequest { Url = "", AltText = "desc" }]
		};

		var result = await _validator.ValidateAsync(request);

		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.PropertyName.Contains("Url"));
	}

	[Fact]
	public async Task ReturnsError_GivenRelativeImageUrl()
	{
		var request = new CreateSocialPostRequest
		{
			Text = "Hello",
			Images = [new ImageAttachmentRequest { Url = "/images/photo.jpg", AltText = "desc" }]
		};

		var result = await _validator.ValidateAsync(request);

		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.PropertyName.Contains("Url"));
	}

	[Fact]
	public async Task ReturnsError_GivenFtpImageUrl()
	{
		var request = new CreateSocialPostRequest
		{
			Text = "Hello",
			Images = [new ImageAttachmentRequest { Url = "ftp://example.com/photo.jpg", AltText = "desc" }]
		};

		var result = await _validator.ValidateAsync(request);

		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.PropertyName.Contains("Url"));
	}

	[Fact]
	public async Task ReturnsError_GivenFileSchemeImageUrl()
	{
		var request = new CreateSocialPostRequest
		{
			Text = "Hello",
			Images = [new ImageAttachmentRequest { Url = "file:///etc/passwd", AltText = "desc" }]
		};

		var result = await _validator.ValidateAsync(request);

		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.PropertyName.Contains("Url"));
	}

	[Fact]
	public async Task ReturnsNoError_GivenValidHttpsImageUrl()
	{
		var request = new CreateSocialPostRequest
		{
			Text = "Hello",
			Images = [new ImageAttachmentRequest { Url = "https://example.com/photo.jpg", AltText = "desc" }]
		};

		var result = await _validator.ValidateAsync(request);

		result.Errors.ShouldNotContain(e => e.PropertyName.Contains("Url"));
	}

	[Fact]
	public async Task ReturnsNoError_GivenImageUrlWithEncodedCharacters()
	{
		var request = new CreateSocialPostRequest
		{
			Text = "Hello",
			Images = [new ImageAttachmentRequest { Url = "https://example.com/my%20photo%20(1).jpg", AltText = "desc" }]
		};

		var result = await _validator.ValidateAsync(request);

		result.Errors.ShouldNotContain(e => e.PropertyName.Contains("Url"));
	}

	[Fact]
	public async Task ReturnsNoError_GivenImageUrlWithQueryString()
	{
		var request = new CreateSocialPostRequest
		{
			Text = "Hello",
			Images = [new ImageAttachmentRequest { Url = "https://example.com/photo.jpg?width=800&format=webp", AltText = "desc" }]
		};

		var result = await _validator.ValidateAsync(request);

		result.Errors.ShouldNotContain(e => e.PropertyName.Contains("Url"));
	}

	[Fact]
	public async Task ReturnsNoError_GivenImageUrlWithUnicodeCharacters()
	{
		var request = new CreateSocialPostRequest
		{
			Text = "Hello",
			Images = [new ImageAttachmentRequest { Url = "https://example.com/画像/photo.jpg", AltText = "desc" }]
		};

		var result = await _validator.ValidateAsync(request);

		result.Errors.ShouldNotContain(e => e.PropertyName.Contains("Url"));
	}
}

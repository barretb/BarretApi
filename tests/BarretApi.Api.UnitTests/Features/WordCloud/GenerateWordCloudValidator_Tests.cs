using BarretApi.Api.Features.WordCloud;
using Shouldly;

namespace BarretApi.Api.UnitTests.Features.WordCloud;

public sealed class GenerateWordCloudValidator_Url_Tests
{
	private readonly GenerateWordCloudValidator _validator = new();

	[Fact]
	public async Task ReturnsError_GivenNullUrl()
	{
		var request = new GenerateWordCloudRequest { Url = null };

		var result = await _validator.ValidateAsync(request);

		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.PropertyName == "Url");
	}

	[Fact]
	public async Task ReturnsError_GivenEmptyUrl()
	{
		var request = new GenerateWordCloudRequest { Url = "" };

		var result = await _validator.ValidateAsync(request);

		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.PropertyName == "Url");
	}

	[Fact]
	public async Task ReturnsError_GivenWhitespaceUrl()
	{
		var request = new GenerateWordCloudRequest { Url = "   " };

		var result = await _validator.ValidateAsync(request);

		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.PropertyName == "Url");
	}

	[Fact]
	public async Task ReturnsError_GivenRelativeUrl()
	{
		var request = new GenerateWordCloudRequest { Url = "/page.html" };

		var result = await _validator.ValidateAsync(request);

		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.PropertyName == "Url");
	}

	[Fact]
	public async Task ReturnsError_GivenFtpScheme()
	{
		var request = new GenerateWordCloudRequest { Url = "ftp://example.com/page.html" };

		var result = await _validator.ValidateAsync(request);

		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.PropertyName == "Url");
	}

	[Fact]
	public async Task ReturnsError_GivenFileScheme()
	{
		var request = new GenerateWordCloudRequest { Url = "file:///etc/passwd" };

		var result = await _validator.ValidateAsync(request);

		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.PropertyName == "Url");
	}

	[Fact]
	public async Task ReturnsError_GivenMalformedUrl()
	{
		var request = new GenerateWordCloudRequest { Url = "not-a-url" };

		var result = await _validator.ValidateAsync(request);

		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.PropertyName == "Url");
	}

	[Fact]
	public async Task ReturnsNoError_GivenValidHttpUrl()
	{
		var request = new GenerateWordCloudRequest { Url = "http://example.com/page" };

		var result = await _validator.ValidateAsync(request);

		result.Errors.ShouldNotContain(e => e.PropertyName == "Url");
	}

	[Fact]
	public async Task ReturnsNoError_GivenValidHttpsUrl()
	{
		var request = new GenerateWordCloudRequest { Url = "https://example.com/page" };

		var result = await _validator.ValidateAsync(request);

		result.Errors.ShouldNotContain(e => e.PropertyName == "Url");
	}

	[Fact]
	public async Task ReturnsNoError_GivenUrlWithQueryString()
	{
		var request = new GenerateWordCloudRequest { Url = "https://example.com/page?key=value&other=123" };

		var result = await _validator.ValidateAsync(request);

		result.Errors.ShouldNotContain(e => e.PropertyName == "Url");
	}

	[Fact]
	public async Task ReturnsNoError_GivenUrlWithEncodedSpaces()
	{
		var request = new GenerateWordCloudRequest { Url = "https://example.com/my%20page/test" };

		var result = await _validator.ValidateAsync(request);

		result.Errors.ShouldNotContain(e => e.PropertyName == "Url");
	}

	[Fact]
	public async Task ReturnsNoError_GivenUrlWithFragment()
	{
		var request = new GenerateWordCloudRequest { Url = "https://example.com/page#section" };

		var result = await _validator.ValidateAsync(request);

		result.Errors.ShouldNotContain(e => e.PropertyName == "Url");
	}

	[Fact]
	public async Task ReturnsNoError_GivenUrlWithUnicodeCharacters()
	{
		var request = new GenerateWordCloudRequest { Url = "https://example.com/页面/日本語" };

		var result = await _validator.ValidateAsync(request);

		result.Errors.ShouldNotContain(e => e.PropertyName == "Url");
	}

	[Fact]
	public async Task ReturnsNoError_GivenUrlWithSpecialCharactersInPath()
	{
		var request = new GenerateWordCloudRequest { Url = "https://en.wikipedia.org/wiki/.NET" };

		var result = await _validator.ValidateAsync(request);

		result.Errors.ShouldNotContain(e => e.PropertyName == "Url");
	}

	[Fact]
	public async Task ReturnsNoError_GivenUrlWithPercentEncodedCharacters()
	{
		var request = new GenerateWordCloudRequest { Url = "https://example.com/path%2Fwith%2Fslashes" };

		var result = await _validator.ValidateAsync(request);

		result.Errors.ShouldNotContain(e => e.PropertyName == "Url");
	}

	[Fact]
	public async Task ReturnsNoError_GivenUrlWithPort()
	{
		var request = new GenerateWordCloudRequest { Url = "https://example.com:8443/page" };

		var result = await _validator.ValidateAsync(request);

		result.Errors.ShouldNotContain(e => e.PropertyName == "Url");
	}
}

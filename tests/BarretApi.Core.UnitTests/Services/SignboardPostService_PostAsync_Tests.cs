using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Core.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace BarretApi.Core.UnitTests.Services;

public sealed class SignboardPostService_PostAsync_Tests
{
	private static readonly byte[] FakePng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01];

	private readonly ISignboardGenerator _generator = Substitute.For<ISignboardGenerator>();
	private readonly ISocialPlatformClient _blueskyClient;
	private readonly SignboardPostService _sut;

	public SignboardPostService_PostAsync_Tests()
	{
		_blueskyClient = CreateMockClient("bluesky", 300, 1_048_576, 1000);

		var textShorteningService = Substitute.For<ITextShorteningService>();
		textShorteningService.Shorten(Arg.Any<string>(), Arg.Any<int>())
			.Returns(callInfo => callInfo.Arg<string>());

		var hashtagService = Substitute.For<IHashtagService>();
		hashtagService.ProcessHashtags(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>())
			.Returns(callInfo => new HashtagProcessingResult
			{
				FinalText = callInfo.Arg<string>()!,
				AllHashtags = []
			});

		var imageResizer = Substitute.For<IImageResizer>();
		imageResizer.ResizeToFit(Arg.Any<byte[]>(), Arg.Any<long>())
			.Returns(callInfo => callInfo.Arg<byte[]>());

		var socialPostService = new SocialPostService(
			[_blueskyClient],
			textShorteningService,
			Substitute.For<ITextSplitterService>(),
			Substitute.For<IImageDownloadService>(),
			imageResizer,
			hashtagService,
			Substitute.For<ILogger<SocialPostService>>());

		_generator.GenerateAsync(Arg.Any<SignboardGenerationCommand>(), Arg.Any<CancellationToken>())
			.Returns(FakePng);

		_sut = new SignboardPostService(
			_generator,
			socialPostService,
			Substitute.For<ILogger<SignboardPostService>>());
	}

	[Fact]
	public async Task GeneratesImageWithSuppliedCommand_GivenValidRequest()
	{
		var command = new SignboardGenerationCommand("HELLO", 1200, 900, 42);

		var result = await _sut.PostAsync(command, null, [], null, ["bluesky"]);

		await _generator.Received(1).GenerateAsync(command, Arg.Any<CancellationToken>());
		result.Width.ShouldBe(1200);
		result.Height.ShouldBe(900);
		result.Seed.ShouldBe(42);
		result.ImageAttached.ShouldBeTrue();
	}

	[Fact]
	public async Task UsesSignTextAsCaption_GivenNoCaption()
	{
		var command = new SignboardGenerationCommand("HELLO WORLD", 1200, 900, 1);

		await _sut.PostAsync(command, null, [], null, ["bluesky"]);

		await _blueskyClient.Received(1).PostAsync(
			Arg.Is<string>(t => t != null && t.Contains("HELLO WORLD")),
			Arg.Any<IReadOnlyList<UploadedImage>>(),
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task UsesSuppliedCaption_GivenCaption()
	{
		var command = new SignboardGenerationCommand("HELLO", 1200, 900, 1);

		await _sut.PostAsync(command, "Custom caption here", [], null, ["bluesky"]);

		await _blueskyClient.Received(1).PostAsync(
			Arg.Is<string>(t => t != null && t.Contains("Custom caption here")),
			Arg.Any<IReadOnlyList<UploadedImage>>(),
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task AttachesPngWithDefaultAltText_GivenNoAltText()
	{
		var command = new SignboardGenerationCommand("Line One\nLine Two", 1200, 900, 1);

		ImageData? capturedImage = null;
		_blueskyClient.UploadImageAsync(Arg.Any<ImageData>(), Arg.Any<CancellationToken>())
			.Returns(callInfo =>
			{
				capturedImage = callInfo.Arg<ImageData>();
				return new UploadedImage { PlatformImageId = "bluesky-img-1", AltText = capturedImage!.AltText };
			});

		await _sut.PostAsync(command, null, [], null, ["bluesky"]);

		capturedImage.ShouldNotBeNull();
		capturedImage!.ContentType.ShouldBe("image/png");
		capturedImage.Content.ShouldBe(FakePng);
		capturedImage.AltText.ShouldBe("A signboard that reads: Line One Line Two");
	}

	[Fact]
	public async Task UsesSuppliedAltText_GivenAltText()
	{
		var command = new SignboardGenerationCommand("HELLO", 1200, 900, 1);

		ImageData? capturedImage = null;
		_blueskyClient.UploadImageAsync(Arg.Any<ImageData>(), Arg.Any<CancellationToken>())
			.Returns(callInfo =>
			{
				capturedImage = callInfo.Arg<ImageData>();
				return new UploadedImage { PlatformImageId = "bluesky-img-1", AltText = capturedImage!.AltText };
			});

		await _sut.PostAsync(command, null, [], "My alt text", ["bluesky"]);

		capturedImage.ShouldNotBeNull();
		capturedImage!.AltText.ShouldBe("My alt text");
	}

	[Fact]
	public async Task ReturnsPlatformResults_GivenSuccessfulPost()
	{
		var command = new SignboardGenerationCommand("HELLO", 1200, 900, 1);

		var result = await _sut.PostAsync(command, null, [], null, ["bluesky"]);

		result.PlatformResults.Count.ShouldBe(1);
		result.PlatformResults[0].Platform.ShouldBe("bluesky");
		result.PlatformResults[0].Success.ShouldBeTrue();
	}

	private static ISocialPlatformClient CreateMockClient(string platform, int maxChars, long maxImageSize, int maxAltText)
	{
		var client = Substitute.For<ISocialPlatformClient>();
		client.PlatformName.Returns(platform);
		client.GetConfigurationAsync(Arg.Any<CancellationToken>())
			.Returns(new PlatformConfiguration
			{
				Name = platform,
				MaxCharacters = maxChars,
				MaxImageSizeBytes = maxImageSize,
				MaxAltTextLength = maxAltText
			});
		client.PostAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<UploadedImage>>(), Arg.Any<CancellationToken>())
			.Returns(callInfo => new PlatformPostResult
			{
				Platform = platform,
				Success = true,
				PostId = $"{platform}-post-123",
				PostUrl = $"https://{platform}.example/post/1",
				PublishedText = callInfo.ArgAt<string>(0)
			});
		client.UploadImageAsync(Arg.Any<ImageData>(), Arg.Any<CancellationToken>())
			.Returns(callInfo => new UploadedImage
			{
				PlatformImageId = $"{platform}-img-1",
				AltText = callInfo.Arg<ImageData>()!.AltText
			});
		return client;
	}
}

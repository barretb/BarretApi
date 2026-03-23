using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Core.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace BarretApi.Core.UnitTests.Services;

public sealed class AvatarPostService_PostAsync_Tests
{
	private readonly IDiceBearAvatarClient _avatarClient = Substitute.For<IDiceBearAvatarClient>();
	private readonly SocialPostService _socialPostService;
	private readonly ILogger<AvatarPostService> _logger = Substitute.For<ILogger<AvatarPostService>>();
	private readonly ISocialPlatformClient _blueskyClient;
	private readonly ISocialPlatformClient _mastodonClient;
	private readonly AvatarPostService _sut;

	public AvatarPostService_PostAsync_Tests()
	{
		_blueskyClient = CreateMockClient("bluesky", 300, 1_048_576, 1000);
		_mastodonClient = CreateMockClient("mastodon", 500, 16_777_216, 1500);

		var textShorteningService = Substitute.For<ITextShorteningService>();
		textShorteningService.Shorten(Arg.Any<string>(), Arg.Any<int>())
			.Returns(callInfo => callInfo.Arg<string>());

		var imageDownloadService = Substitute.For<IImageDownloadService>();

		var hashtagService = Substitute.For<IHashtagService>();
		hashtagService.ProcessHashtags(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>())
			.Returns(callInfo => new HashtagProcessingResult
			{
				FinalText = callInfo.Arg<string>(),
				AllHashtags = []
			});

		var imageResizer = Substitute.For<IImageResizer>();
		imageResizer.ResizeToFit(Arg.Any<byte[]>(), Arg.Any<long>())
			.Returns(callInfo => callInfo.Arg<byte[]>());

		_socialPostService = new SocialPostService(
			[_blueskyClient, _mastodonClient],
			textShorteningService,
			imageDownloadService,
			imageResizer,
			hashtagService,
			Substitute.For<ILogger<SocialPostService>>());

		_sut = new AvatarPostService(_avatarClient, _socialPostService, _logger);
	}

	[Fact]
	public async Task ReturnsAvatarPostResult_GivenSuccessfulGeneration()
	{
		SetupAvatarClient("pixel-art", "test-seed", "png");

		var result = await _sut.PostAsync(
			"pixel-art", "test-seed", "Hello!", "Alt text", [], [], CancellationToken.None);

		result.ShouldNotBeNull();
		result.Style.ShouldBe("pixel-art");
		result.Seed.ShouldBe("test-seed");
		result.Format.ShouldBe("png");
		result.ImageAttached.ShouldBeTrue();
		result.PlatformResults.Count.ShouldBe(2);
	}

	[Fact]
	public async Task PostsToAllPlatforms_GivenNoPlatformFilter()
	{
		SetupAvatarClient("adventurer", "seed-1", "png");

		var result = await _sut.PostAsync(
			"adventurer", "seed-1", "Hello!", "Alt", [], [], CancellationToken.None);

		result.PlatformResults.Count.ShouldBe(2);
		result.PlatformResults.ShouldContain(r => r.Platform == "bluesky");
		result.PlatformResults.ShouldContain(r => r.Platform == "mastodon");
	}

	[Fact]
	public async Task PostsToSelectedPlatform_GivenSpecificPlatform()
	{
		SetupAvatarClient("bottts", "seed-2", "png");

		var result = await _sut.PostAsync(
			"bottts", "seed-2", "Test", "Alt", [], ["bluesky"], CancellationToken.None);

		result.PlatformResults.Count.ShouldBe(1);
		result.PlatformResults[0].Platform.ShouldBe("bluesky");
	}

	[Fact]
	public async Task UsesDefaultAltText_GivenEmptyAltText()
	{
		SetupAvatarClient("pixel-art", "seed-3", "png");

		await _sut.PostAsync(
			"pixel-art", "seed-3", "Text", "", [], ["bluesky"], CancellationToken.None);

		await _blueskyClient.Received(1).UploadImageAsync(
			Arg.Is<ImageData>(img => img.AltText == "A randomly generated DiceBear avatar"),
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task UsesProvidedAltText_GivenNonEmptyAltText()
	{
		SetupAvatarClient("pixel-art", "seed-4", "png");

		await _sut.PostAsync(
			"pixel-art", "seed-4", "Text", "Custom alt text", [], ["bluesky"], CancellationToken.None);

		await _blueskyClient.Received(1).UploadImageAsync(
			Arg.Is<ImageData>(img => img.AltText == "Custom alt text"),
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task AlwaysRequestsPngFormat_GivenAnyStyle()
	{
		SetupAvatarClient("rings", "seed-5", "png");

		await _sut.PostAsync(
			"rings", "seed-5", "Text", "Alt", [], [], CancellationToken.None);

		await _avatarClient.Received(1).GetAvatarAsync(
			"rings", "png", "seed-5", Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task PropagatesInvalidOperationException_GivenAvatarClientFailure()
	{
		_avatarClient
			.GetAvatarAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
			.ThrowsAsync(new InvalidOperationException("Service unavailable"));

		await Should.ThrowAsync<InvalidOperationException>(async () =>
			await _sut.PostAsync("pixel-art", "seed", "Text", "Alt", [], [], CancellationToken.None));
	}

	[Fact]
	public async Task PassesHashtagsToSocialPost_GivenHashtags()
	{
		SetupAvatarClient("pixel-art", "seed-6", "png");

		_blueskyClient.PostAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<UploadedImage>>(), Arg.Any<CancellationToken>())
			.Returns(callInfo => new PlatformPostResult
			{
				Platform = "bluesky",
				Success = true,
				PublishedText = callInfo.ArgAt<string>(0)
			});

		await _sut.PostAsync(
			"pixel-art", "seed-6", "My post", "Alt", ["avatar", "dicebear"], ["bluesky"], CancellationToken.None);

		await _blueskyClient.Received(1).PostAsync(
			Arg.Any<string>(),
			Arg.Any<IReadOnlyList<UploadedImage>>(),
			Arg.Any<CancellationToken>());
	}

	private void SetupAvatarClient(string style, string seed, string format)
	{
		_avatarClient
			.GetAvatarAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
			.Returns(new AvatarResult
			{
				ImageBytes = [0x89, 0x50, 0x4E, 0x47],
				ContentType = $"image/{format}",
				Style = style,
				Seed = seed,
				Format = format
			});
	}

	private static ISocialPlatformClient CreateMockClient(
		string platformName, int maxChars, long maxImageSize, int maxAltTextLength)
	{
		var client = Substitute.For<ISocialPlatformClient>();
		client.PlatformName.Returns(platformName);
		client.GetConfigurationAsync(Arg.Any<CancellationToken>())
			.Returns(new PlatformConfiguration
			{
				Name = platformName,
				MaxCharacters = maxChars,
				MaxImages = 4,
				MaxImageSizeBytes = maxImageSize,
				MaxAltTextLength = maxAltTextLength
			});
		client.UploadImageAsync(Arg.Any<ImageData>(), Arg.Any<CancellationToken>())
			.Returns(callInfo => new UploadedImage
			{
				PlatformImageId = $"{platformName}-img-1",
				AltText = callInfo.Arg<ImageData>().AltText
			});
		client.PostAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<UploadedImage>>(), Arg.Any<CancellationToken>())
			.Returns(callInfo => new PlatformPostResult
			{
				Platform = platformName,
				Success = true,
				PostId = $"{platformName}-post-1",
				PostUrl = $"https://{platformName}.example/post/1",
				PublishedText = callInfo.ArgAt<string>(0)
			});
		return client;
	}
}

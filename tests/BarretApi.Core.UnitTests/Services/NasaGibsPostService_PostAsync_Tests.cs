using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace BarretApi.Core.UnitTests.Services;

public sealed class NasaGibsPostService_PostAsync_Tests
{
	private readonly INasaGibsClient _gibsClient = Substitute.For<INasaGibsClient>();
	private readonly SocialPostService _socialPostService;
	private readonly ILogger<NasaGibsPostService> _logger = Substitute.For<ILogger<NasaGibsPostService>>();
	private readonly ISocialPlatformClient _blueskyClient;
	private readonly ISocialPlatformClient _mastodonClient;
	private readonly NasaGibsOptions _options;
	private readonly NasaGibsPostService _sut;

	public NasaGibsPostService_PostAsync_Tests()
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

		_socialPostService = new SocialPostService(
			[_blueskyClient, _mastodonClient],
			textShorteningService,
			imageDownloadService,
			hashtagService,
			Substitute.For<ILogger<SocialPostService>>());

		_options = new NasaGibsOptions();
		var optionsWrapper = Substitute.For<IOptions<NasaGibsOptions>>();
		optionsWrapper.Value.Returns(_options);

		_sut = new NasaGibsPostService(_gibsClient, _socialPostService, optionsWrapper, _logger);
	}

	[Fact]
	public async Task ResolvesDateToYesterdayUtc_GivenNullDate()
	{
		var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
		SetupGibsClient();

		var result = await _sut.PostAsync(null, null, [], CancellationToken.None);

		result.Date.ShouldBe(yesterday);
		await _gibsClient.Received(1).GetSnapshotAsync(
			Arg.Any<string>(),
			yesterday,
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task UsesDefaultLayerFromOptions_GivenNullLayer()
	{
		SetupGibsClient();

		var result = await _sut.PostAsync(null, null, [], CancellationToken.None);

		result.Layer.ShouldBe(_options.DefaultLayer);
		await _gibsClient.Received(1).GetSnapshotAsync(
			_options.DefaultLayer,
			Arg.Any<DateOnly>(),
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task BuildsWorldviewUrlWithLonLatOrder_GivenDefaultOptions()
	{
		SetupGibsClient();

		var result = await _sut.PostAsync(null, null, [], CancellationToken.None);

		result.WorldviewUrl.ShouldContain($"?v={_options.BboxWest},{_options.BboxSouth},{_options.BboxEast},{_options.BboxNorth}");
		result.WorldviewUrl.ShouldStartWith("https://worldview.earthdata.nasa.gov/");
	}

	[Fact]
	public async Task IncludesDateAndLayerAndAcknowledgement_GivenPostText()
	{
		var date = new DateOnly(2026, 3, 15);
		var layer = "VIIRS_SNPP_CorrectedReflectance_TrueColor";
		SetupGibsClient();

		_blueskyClient.PostAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<UploadedImage>>(), Arg.Any<CancellationToken>())
			.Returns(callInfo =>
			{
				var text = callInfo.ArgAt<string>(0);
				return new PlatformPostResult { Platform = "bluesky", Success = true, PublishedText = text };
			});

		var result = await _sut.PostAsync(date, layer, ["bluesky"], CancellationToken.None);

		var publishedText = result.PlatformResults[0].PublishedText!;
		publishedText.ShouldContain("2026-03-15");
		publishedText.ShouldContain("VIIRS_SNPP_CorrectedReflectance_TrueColor");
		publishedText.ShouldContain("Imagery: NASA GIBS");
		publishedText.ShouldContain("Satellite view of Ohio");
	}

	[Fact]
	public async Task BuildsAltTextWithDateAndLayer_GivenSnapshotRequest()
	{
		var date = new DateOnly(2026, 3, 15);
		var layer = "MODIS_Terra_CorrectedReflectance_TrueColor";
		SetupGibsClient();

		ImageData? capturedImage = null;
		_blueskyClient.UploadImageAsync(Arg.Any<ImageData>(), Arg.Any<CancellationToken>())
			.Returns(callInfo =>
			{
				capturedImage = callInfo.Arg<ImageData>();
				return new UploadedImage { PlatformImageId = "img1", AltText = capturedImage.AltText };
			});

		await _sut.PostAsync(date, layer, ["bluesky"], CancellationToken.None);

		capturedImage.ShouldNotBeNull();
		capturedImage!.AltText.ShouldContain("2026-03-15");
		capturedImage.AltText.ShouldContain("MODIS_Terra_CorrectedReflectance_TrueColor");
		capturedImage.AltText.ShouldContain("NASA GIBS");
	}

	[Fact]
	public async Task WrapsSnapshotBytesInImageData_GivenSuccessfulSnapshot()
	{
		var imageBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 };
		SetupGibsClient(imageBytes: imageBytes);

		ImageData? capturedImage = null;
		_blueskyClient.UploadImageAsync(Arg.Any<ImageData>(), Arg.Any<CancellationToken>())
			.Returns(callInfo =>
			{
				capturedImage = callInfo.Arg<ImageData>();
				return new UploadedImage { PlatformImageId = "img1", AltText = capturedImage.AltText };
			});

		await _sut.PostAsync(null, null, ["bluesky"], CancellationToken.None);

		capturedImage.ShouldNotBeNull();
		capturedImage!.Content.ShouldBe(imageBytes);
		capturedImage.ContentType.ShouldBe("image/jpeg");
	}

	[Fact]
	public async Task ReturnsCorrectPlatformResults_GivenMultiplePlatforms()
	{
		SetupGibsClient();

		var result = await _sut.PostAsync(null, null, [], CancellationToken.None);

		result.PlatformResults.Count.ShouldBe(2);
		result.PlatformResults.ShouldContain(r => r.Platform == "bluesky");
		result.PlatformResults.ShouldContain(r => r.Platform == "mastodon");
	}

	[Fact]
	public async Task SetsImageAttachedTrue_GivenSuccessfulSnapshot()
	{
		SetupGibsClient();

		var result = await _sut.PostAsync(null, null, [], CancellationToken.None);

		result.ImageAttached.ShouldBeTrue();
		result.ImageResized.ShouldBeFalse();
	}

	[Fact]
	public async Task PropagatesException_GivenGibsClientFailure()
	{
		_gibsClient.GetSnapshotAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
			.ThrowsAsync(new InvalidOperationException("GIBS returned error"));

		await Should.ThrowAsync<InvalidOperationException>(
			() => _sut.PostAsync(null, null, [], CancellationToken.None));
	}

	// --- US2: Date-specific tests ---

	[Fact]
	public async Task PassesExplicitDateToGibsClient_GivenSpecificDate()
	{
		var date = new DateOnly(2026, 2, 14);
		SetupGibsClient();

		await _sut.PostAsync(date, null, [], CancellationToken.None);

		await _gibsClient.Received(1).GetSnapshotAsync(
			Arg.Any<string>(),
			date,
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task IncludesSpecifiedDateInWorldviewUrl_GivenExplicitDate()
	{
		var date = new DateOnly(2026, 2, 14);
		SetupGibsClient();

		var result = await _sut.PostAsync(date, null, [], CancellationToken.None);

		result.WorldviewUrl.ShouldContain("&t=2026-02-14");
	}

	[Fact]
	public async Task ReturnsMatchingDate_GivenSpecificDate()
	{
		var date = new DateOnly(2026, 2, 14);
		SetupGibsClient();

		var result = await _sut.PostAsync(date, null, [], CancellationToken.None);

		result.Date.ShouldBe(date);
	}

	// --- US3: Layer-specific tests ---

	[Fact]
	public async Task PassesExplicitLayerToGibsClient_GivenSpecificLayer()
	{
		var layer = "VIIRS_SNPP_CorrectedReflectance_TrueColor";
		SetupGibsClient();

		await _sut.PostAsync(null, layer, [], CancellationToken.None);

		await _gibsClient.Received(1).GetSnapshotAsync(
			layer,
			Arg.Any<DateOnly>(),
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task IncludesLayerInWorldviewUrl_GivenSpecificLayer()
	{
		var layer = "VIIRS_SNPP_CorrectedReflectance_TrueColor";
		SetupGibsClient();

		var result = await _sut.PostAsync(null, layer, [], CancellationToken.None);

		result.WorldviewUrl.ShouldContain($"&l={layer}");
	}

	[Fact]
	public async Task DefaultsToConfiguredLayer_GivenNullLayer()
	{
		SetupGibsClient();

		var result = await _sut.PostAsync(null, null, [], CancellationToken.None);

		result.Layer.ShouldBe("MODIS_Terra_CorrectedReflectance_TrueColor");
	}

	private void SetupGibsClient(byte[]? imageBytes = null)
	{
		var bytes = imageBytes ?? [0xFF, 0xD8, 0xFF, 0xE0];
		_gibsClient.GetSnapshotAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
			.Returns(new GibsSnapshotEntry(bytes, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), "MODIS_Terra_CorrectedReflectance_TrueColor", 1024, 768, "image/jpeg"));
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
				AltText = callInfo.Arg<ImageData>().AltText
			});
		return client;
	}
}

using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Core.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace BarretApi.Core.UnitTests.Services;

public sealed class NasaApodPostService_PostAsync_Tests
{
    private readonly INasaApodClient _nasaClient = Substitute.For<INasaApodClient>();
    private readonly SocialPostService _socialPostService;
    private readonly IImageResizer _imageResizer = Substitute.For<IImageResizer>();
    private readonly ILogger<NasaApodPostService> _logger = Substitute.For<ILogger<NasaApodPostService>>();
    private readonly ISocialPlatformClient _blueskyClient;
    private readonly ISocialPlatformClient _mastodonClient;
    private readonly NasaApodPostService _sut;

    public NasaApodPostService_PostAsync_Tests()
    {
        _blueskyClient = CreateMockClient("bluesky", 300, 1_048_576, 1000);
        _mastodonClient = CreateMockClient("mastodon", 500, 16_777_216, 1500);

        var textShorteningService = Substitute.For<ITextShorteningService>();
        textShorteningService.Shorten(Arg.Any<string>(), Arg.Any<int>())
            .Returns(callInfo => callInfo.Arg<string>());

        var imageDownloadService = Substitute.For<IImageDownloadService>();
        imageDownloadService.DownloadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new ImageData
            {
                Content = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 },
                ContentType = "image/jpeg",
                AltText = callInfo.ArgAt<string>(1)
            });

        var hashtagService = Substitute.For<IHashtagService>();
        hashtagService.ProcessHashtags(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>())
            .Returns(callInfo => new HashtagProcessingResult
            {
                FinalText = callInfo.Arg<string>(),
                AllHashtags = []
            });

        var socialImageResizer = Substitute.For<IImageResizer>();
        socialImageResizer.ResizeToFit(Arg.Any<byte[]>(), Arg.Any<long>())
            .Returns(callInfo => callInfo.Arg<byte[]>());

        _socialPostService = new SocialPostService(
            [_blueskyClient, _mastodonClient],
            textShorteningService,
            Substitute.For<ITextSplitterService>(),
            imageDownloadService,
            socialImageResizer,
            hashtagService,
            Substitute.For<ILogger<SocialPostService>>());

        _imageResizer.ResizeToFit(Arg.Any<byte[]>(), Arg.Any<long>())
            .Returns(callInfo => callInfo.Arg<byte[]>());

        _sut = new NasaApodPostService(_nasaClient, _socialPostService, _imageResizer, _logger);
    }

    [Fact]
    public async Task ReturnsApodPostResult_GivenSuccessfulImageApod()
    {
        var apod = CreateImageApod();
        _nasaClient.GetApodAsync(Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>())
            .Returns(apod);

        var result = await _sut.PostAsync(null, [], CancellationToken.None);

        result.ShouldNotBeNull();
        result.ApodEntry.ShouldBe(apod);
        result.ImageAttached.ShouldBeTrue();
        result.PlatformResults.Count.ShouldBe(2);
    }

    [Fact]
    public async Task PostsToAllPlatforms_GivenNoPlatformFilter()
    {
        _nasaClient.GetApodAsync(Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>())
            .Returns(CreateImageApod());

        var result = await _sut.PostAsync(null, [], CancellationToken.None);

        result.PlatformResults.Count.ShouldBe(2);
        result.PlatformResults.ShouldContain(r => r.Platform == "bluesky");
        result.PlatformResults.ShouldContain(r => r.Platform == "mastodon");
    }

    [Fact]
    public async Task PostsToSelectedPlatforms_GivenSpecificPlatforms()
    {
        _nasaClient.GetApodAsync(Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>())
            .Returns(CreateImageApod());

        var result = await _sut.PostAsync(null, ["bluesky"], CancellationToken.None);

        result.PlatformResults.Count.ShouldBe(1);
        result.PlatformResults[0].Platform.ShouldBe("bluesky");
    }

    [Fact]
    public async Task BuildsPostTextWithTitleAndHdUrl_GivenImageApodWithHdUrl()
    {
        var apod = CreateImageApod(hdUrl: "https://apod.nasa.gov/hd/img.jpg");
        _nasaClient.GetApodAsync(Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>())
            .Returns(apod);

        _blueskyClient.PostAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<UploadedImage>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var text = callInfo.ArgAt<string>(0);
                return new PlatformPostResult { Platform = "bluesky", Success = true, PublishedText = text };
            });

        var result = await _sut.PostAsync(null, ["bluesky"], CancellationToken.None);

        result.PlatformResults[0].PublishedText.ShouldNotBeNull();
        result.PlatformResults[0].PublishedText!.ShouldContain("The Aurora Tree");
        result.PlatformResults[0].PublishedText!.ShouldContain("https://apod.nasa.gov/hd/img.jpg");
    }

    [Fact]
    public async Task BuildsPostTextWithStandardUrl_GivenImageApodWithoutHdUrl()
    {
        var apod = CreateImageApod(hdUrl: null);
        _nasaClient.GetApodAsync(Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>())
            .Returns(apod);

        _blueskyClient.PostAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<UploadedImage>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var text = callInfo.ArgAt<string>(0);
                return new PlatformPostResult { Platform = "bluesky", Success = true, PublishedText = text };
            });

        var result = await _sut.PostAsync(null, ["bluesky"], CancellationToken.None);

        result.PlatformResults[0].PublishedText.ShouldNotBeNull();
        result.PlatformResults[0].PublishedText!.ShouldContain("https://apod.nasa.gov/apod/image/2603/AuroraTree_960.jpg");
    }

    [Fact]
    public async Task UsesExplanationAsAltText_GivenImageApod()
    {
        var apod = CreateImageApod();
        _nasaClient.GetApodAsync(Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>())
            .Returns(apod);

        ImageData? capturedImage = null;
        _blueskyClient.UploadImageAsync(Arg.Any<ImageData>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedImage = callInfo.Arg<ImageData>();
                return new UploadedImage { PlatformImageId = "img1", AltText = capturedImage.AltText };
            });

        var result = await _sut.PostAsync(null, ["bluesky"], CancellationToken.None);

        capturedImage.ShouldNotBeNull();
        capturedImage!.AltText.ShouldBe("Yes, but can your tree do this?");
    }

    [Fact]
    public async Task SetsImageAttachedTrue_GivenImageMediaType()
    {
        _nasaClient.GetApodAsync(Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>())
            .Returns(CreateImageApod());

        var result = await _sut.PostAsync(null, [], CancellationToken.None);

        result.ImageAttached.ShouldBeTrue();
    }

    [Fact]
    public async Task PropagatesException_GivenNasaApiFailure()
    {
        _nasaClient.GetApodAsync(Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("NASA API returned status 429"));

        await Should.ThrowAsync<HttpRequestException>(
            () => _sut.PostAsync(null, [], CancellationToken.None));
    }

    [Fact]
    public async Task PassesDateToClient_GivenSpecificDate()
    {
        var date = new DateOnly(2026, 2, 14);
        _nasaClient.GetApodAsync(Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>())
            .Returns(CreateImageApod());

        await _sut.PostAsync(date, [], CancellationToken.None);

        await _nasaClient.Received(1).GetApodAsync(date, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PassesNullDateToClient_GivenNoDate()
    {
        _nasaClient.GetApodAsync(Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>())
            .Returns(CreateImageApod());

        await _sut.PostAsync(null, [], CancellationToken.None);

        await _nasaClient.Received(1).GetApodAsync(null, Arg.Any<CancellationToken>());
    }

    // --- US2: Specific-date posting tests ---

    [Fact]
    public async Task ReturnsApodWithMatchingDate_GivenSpecificDate()
    {
        var requestedDate = new DateOnly(2024, 7, 4);
        var apod = new ApodEntry
        {
            Title = "Independence Day Fireworks",
            Date = requestedDate,
            Explanation = "Fireworks over the National Mall",
            Url = "https://apod.nasa.gov/apod/image/2407/fireworks_960.jpg",
            HdUrl = "https://apod.nasa.gov/apod/image/2407/fireworks_2048.jpg",
            MediaType = ApodMediaType.Image
        };
        _nasaClient.GetApodAsync(requestedDate, Arg.Any<CancellationToken>())
            .Returns(apod);

        var result = await _sut.PostAsync(requestedDate, [], CancellationToken.None);

        result.ApodEntry.Date.ShouldBe(requestedDate);
    }

    // --- US3: Video APOD handling tests ---

    [Fact]
    public async Task AttachesThumbnailImage_GivenVideoApodWithThumbnail()
    {
        var apod = new ApodEntry
        {
            Title = "Perseverance Rover Landing",
            Date = new DateOnly(2021, 2, 22),
            Explanation = "The Perseverance rover landing on Mars.",
            Url = "https://youtube.com/embed/4czjS9h4Fpg",
            MediaType = ApodMediaType.Video,
            ThumbnailUrl = "https://img.youtube.com/vi/4czjS9h4Fpg/0.jpg"
        };
        _nasaClient.GetApodAsync(Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>())
            .Returns(apod);

        var result = await _sut.PostAsync(null, ["bluesky"], CancellationToken.None);

        result.ImageAttached.ShouldBeTrue();
    }

    [Fact]
    public async Task PostsTextOnly_GivenVideoApodWithoutThumbnail()
    {
        var apod = new ApodEntry
        {
            Title = "Perseverance Rover Landing",
            Date = new DateOnly(2021, 2, 22),
            Explanation = "The Perseverance rover landing on Mars.",
            Url = "https://youtube.com/embed/4czjS9h4Fpg",
            MediaType = ApodMediaType.Video,
            ThumbnailUrl = null
        };
        _nasaClient.GetApodAsync(Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>())
            .Returns(apod);

        var result = await _sut.PostAsync(null, ["bluesky"], CancellationToken.None);

        result.ImageAttached.ShouldBeFalse();
    }

    [Fact]
    public async Task IncludesVideoUrlInPostText_GivenVideoApod()
    {
        var apod = new ApodEntry
        {
            Title = "Perseverance Rover Landing",
            Date = new DateOnly(2021, 2, 22),
            Explanation = "The Perseverance rover landing on Mars.",
            Url = "https://youtube.com/embed/4czjS9h4Fpg",
            MediaType = ApodMediaType.Video,
            ThumbnailUrl = "https://img.youtube.com/vi/4czjS9h4Fpg/0.jpg"
        };
        _nasaClient.GetApodAsync(Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>())
            .Returns(apod);

        _blueskyClient.PostAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<UploadedImage>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var text = callInfo.ArgAt<string>(0);
                return new PlatformPostResult { Platform = "bluesky", Success = true, PublishedText = text };
            });

        var result = await _sut.PostAsync(null, ["bluesky"], CancellationToken.None);

        result.PlatformResults[0].PublishedText.ShouldNotBeNull();
        result.PlatformResults[0].PublishedText!.ShouldContain("https://youtube.com/embed/4czjS9h4Fpg");
    }

    [Fact]
    public async Task SetsImageAttachedCorrectly_GivenVideoWithAndWithoutThumbnail()
    {
        var apodWithThumb = new ApodEntry
        {
            Title = "Video APOD",
            Date = new DateOnly(2021, 2, 22),
            Explanation = "A video.",
            Url = "https://youtube.com/embed/video1",
            MediaType = ApodMediaType.Video,
            ThumbnailUrl = "https://img.youtube.com/thumb.jpg"
        };
        _nasaClient.GetApodAsync(Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>())
            .Returns(apodWithThumb);

        var resultWithThumb = await _sut.PostAsync(null, ["bluesky"], CancellationToken.None);
        resultWithThumb.ImageAttached.ShouldBeTrue();

        var apodNoThumb = new ApodEntry
        {
            Title = "Video APOD No Thumb",
            Date = new DateOnly(2021, 2, 22),
            Explanation = "A video without thumbnail.",
            Url = "https://youtube.com/embed/video2",
            MediaType = ApodMediaType.Video,
            ThumbnailUrl = null
        };
        _nasaClient.GetApodAsync(Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>())
            .Returns(apodNoThumb);

        var resultNoThumb = await _sut.PostAsync(null, ["bluesky"], CancellationToken.None);
        resultNoThumb.ImageAttached.ShouldBeFalse();
    }

    // --- US4: Copyright attribution tests ---

    [Fact]
    public async Task IncludesCreditLine_GivenCopyrightedApod()
    {
        var apod = CreateImageApod(copyright: "Alyn Wallace");
        _nasaClient.GetApodAsync(Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>())
            .Returns(apod);

        _blueskyClient.PostAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<UploadedImage>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var text = callInfo.ArgAt<string>(0);
                return new PlatformPostResult { Platform = "bluesky", Success = true, PublishedText = text };
            });

        var result = await _sut.PostAsync(null, ["bluesky"], CancellationToken.None);

        result.PlatformResults[0].PublishedText.ShouldNotBeNull();
        result.PlatformResults[0].PublishedText!.ShouldContain("Credit: Alyn Wallace");
    }

    [Fact]
    public async Task OmitsCreditLine_GivenPublicDomainApod()
    {
        var apod = CreateImageApod(copyright: null);
        _nasaClient.GetApodAsync(Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>())
            .Returns(apod);

        _blueskyClient.PostAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<UploadedImage>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var text = callInfo.ArgAt<string>(0);
                return new PlatformPostResult { Platform = "bluesky", Success = true, PublishedText = text };
            });

        var result = await _sut.PostAsync(null, ["bluesky"], CancellationToken.None);

        result.PlatformResults[0].PublishedText.ShouldNotBeNull();
        result.PlatformResults[0].PublishedText!.ShouldNotContain("Credit:");
    }

    [Fact]
    public async Task PlacesCreditAfterUrl_GivenCopyrightedApod()
    {
        var apod = CreateImageApod(hdUrl: "https://apod.nasa.gov/hd/img.jpg", copyright: "John Doe");
        _nasaClient.GetApodAsync(Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>())
            .Returns(apod);

        _blueskyClient.PostAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<UploadedImage>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var text = callInfo.ArgAt<string>(0);
                return new PlatformPostResult { Platform = "bluesky", Success = true, PublishedText = text };
            });

        var result = await _sut.PostAsync(null, ["bluesky"], CancellationToken.None);

        var text = result.PlatformResults[0].PublishedText!;
        var urlIndex = text.IndexOf("https://apod.nasa.gov/hd/img.jpg", StringComparison.Ordinal);
        var creditIndex = text.IndexOf("Credit: John Doe", StringComparison.Ordinal);
        creditIndex.ShouldBeGreaterThan(urlIndex);
    }

    // --- Phase 7: Error scenario tests ---

    [Fact]
    public async Task PropagatesTimeoutException_GivenNasaApiTimeout()
    {
        _nasaClient.GetApodAsync(Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout of 15 seconds elapsing."));

        await Should.ThrowAsync<TaskCanceledException>(
            () => _sut.PostAsync(null, [], CancellationToken.None));
    }

    [Fact]
    public async Task ReturnsAllPlatformsFailed_GivenEveryPlatformErrors()
    {
        _nasaClient.GetApodAsync(Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>())
            .Returns(CreateImageApod());

        _blueskyClient.PostAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<UploadedImage>>(), Arg.Any<CancellationToken>())
            .Returns(new PlatformPostResult { Platform = "bluesky", Success = false, ErrorMessage = "Auth failed", ErrorCode = "AUTH_FAILED" });
        _mastodonClient.PostAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<UploadedImage>>(), Arg.Any<CancellationToken>())
            .Returns(new PlatformPostResult { Platform = "mastodon", Success = false, ErrorMessage = "Rate limited", ErrorCode = "RATE_LIMITED" });

        var result = await _sut.PostAsync(null, [], CancellationToken.None);

        result.PlatformResults.ShouldAllBe(r => !r.Success);
        result.PlatformResults.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ReturnsPartialSuccess_GivenSomePlatformsFail()
    {
        _nasaClient.GetApodAsync(Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>())
            .Returns(CreateImageApod());

        _blueskyClient.PostAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<UploadedImage>>(), Arg.Any<CancellationToken>())
            .Returns(new PlatformPostResult { Platform = "bluesky", Success = true, PostId = "p1" });
        _mastodonClient.PostAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<UploadedImage>>(), Arg.Any<CancellationToken>())
            .Returns(new PlatformPostResult { Platform = "mastodon", Success = false, ErrorMessage = "Auth failed", ErrorCode = "AUTH_FAILED" });

        var result = await _sut.PostAsync(null, [], CancellationToken.None);

        result.PlatformResults.Count(r => r.Success).ShouldBe(1);
        result.PlatformResults.Count(r => !r.Success).ShouldBe(1);
    }

    [Fact]
    public async Task ContinuesWithTextOnly_GivenImageDownloadReturnsNullContent()
    {
        var apod = CreateImageApod();
        _nasaClient.GetApodAsync(Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>())
            .Returns(apod);

        // Even with image download failure, the post should still be created with image URL
        // (SocialPostService handles actual image download, NasaApodPostService just provides URL)
        var result = await _sut.PostAsync(null, ["bluesky"], CancellationToken.None);

        result.ShouldNotBeNull();
        result.PlatformResults.Count.ShouldBe(1);
    }

    private static ApodEntry CreateImageApod(
        string? hdUrl = "https://apod.nasa.gov/hd/img.jpg",
        string? copyright = null)
    {
        return new ApodEntry
        {
            Title = "The Aurora Tree",
            Date = new DateOnly(2026, 3, 8),
            Explanation = "Yes, but can your tree do this?",
            Url = "https://apod.nasa.gov/apod/image/2603/AuroraTree_960.jpg",
            HdUrl = hdUrl,
            MediaType = ApodMediaType.Image,
            Copyright = copyright,
            ThumbnailUrl = null
        };
    }

    private static ISocialPlatformClient CreateMockClient(
        string platformName, int maxChars, long maxImageSize, int maxAltText)
    {
        var client = Substitute.For<ISocialPlatformClient>();
        client.PlatformName.Returns(platformName);
        client.GetConfigurationAsync(Arg.Any<CancellationToken>())
            .Returns(new PlatformConfiguration
            {
                Name = platformName,
                MaxCharacters = maxChars,
                MaxImageSizeBytes = maxImageSize,
                MaxAltTextLength = maxAltText
            });
        client.UploadImageAsync(Arg.Any<ImageData>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new UploadedImage
            {
                PlatformImageId = "img-1",
                AltText = callInfo.Arg<ImageData>().AltText
            });
        client.PostAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<UploadedImage>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new PlatformPostResult
            {
                Platform = platformName,
                Success = true,
                PostId = "post-1",
                PostUrl = $"https://{platformName}.example/post/1",
                PublishedText = callInfo.ArgAt<string>(0)
            });
        return client;
    }
}

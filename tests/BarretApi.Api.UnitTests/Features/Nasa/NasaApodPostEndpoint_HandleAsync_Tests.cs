using BarretApi.Api.Features.Nasa;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Core.Services;
using FastEndpoints;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace BarretApi.Api.UnitTests.Features.Nasa;

public sealed class NasaApodPostEndpoint_HandleAsync_Tests
{
    private readonly NasaApodPostService _service;
    private readonly ILogger<NasaApodPostEndpoint> _logger = Substitute.For<ILogger<NasaApodPostEndpoint>>();

    public NasaApodPostEndpoint_HandleAsync_Tests()
    {
        var socialPostService = new SocialPostService(
            Array.Empty<ISocialPlatformClient>(),
            Substitute.For<ITextShorteningService>(),
            Substitute.For<IImageDownloadService>(),
            Substitute.For<IImageResizer>(),
            Substitute.For<IHashtagService>(),
            Substitute.For<ILogger<SocialPostService>>());

        _service = Substitute.For<NasaApodPostService>(
            Substitute.For<INasaApodClient>(),
            socialPostService,
            Substitute.For<IImageResizer>(),
            Substitute.For<ILogger<NasaApodPostService>>());
    }

    private static ApodPostResult CreateSuccessResult(ApodEntry apod)
    {
        return new ApodPostResult
        {
            ApodEntry = apod,
            PlatformResults =
            [
                new PlatformPostResult
                {
                    Platform = "bluesky",
                    Success = true,
                    PostId = "post-1",
                    PostUrl = "https://bsky.app/post/1"
                }
            ],
            ImageAttached = true,
            ImageResized = false
        };
    }

    private static ApodEntry CreateImageApod()
    {
        return new ApodEntry
        {
            Title = "The Aurora Tree",
            Date = new DateOnly(2026, 3, 8),
            Explanation = "Yes, but can your tree do this?",
            Url = "https://apod.nasa.gov/apod/image/2603/AuroraTree_960.jpg",
            HdUrl = "https://apod.nasa.gov/apod/image/2603/AuroraTree_2048.jpg",
            MediaType = ApodMediaType.Image,
            Copyright = "Alyn Wallace"
        };
    }

    [Fact]
    public async Task ResponseContainsApodMetadata_GivenSuccessfulPost()
    {
        var apod = CreateImageApod();
        var apodResult = CreateSuccessResult(apod);
        _service.PostAsync(Arg.Any<DateOnly?>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(apodResult);

        var ep = Factory.Create<NasaApodPostEndpoint>(_service, _logger);
        var req = new NasaApodPostRequest();

        await ep.HandleAsync(req, default);

        ep.Response.Title.ShouldBe("The Aurora Tree");
        ep.Response.Date.ShouldBe("2026-03-08");
        ep.Response.MediaType.ShouldBe("image");
        ep.Response.ImageUrl.ShouldBe("https://apod.nasa.gov/apod/image/2603/AuroraTree_960.jpg");
        ep.Response.HdImageUrl.ShouldBe("https://apod.nasa.gov/apod/image/2603/AuroraTree_2048.jpg");
        ep.Response.Copyright.ShouldBe("Alyn Wallace");
        ep.Response.ImageAttached.ShouldBeTrue();
        ep.Response.ImageResized.ShouldBeFalse();
    }

    [Fact]
    public async Task DefaultsToToday_GivenEmptyRequest()
    {
        var apod = CreateImageApod();
        _service.PostAsync(Arg.Any<DateOnly?>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(CreateSuccessResult(apod));

        var ep = Factory.Create<NasaApodPostEndpoint>(_service, _logger);
        var req = new NasaApodPostRequest();

        await ep.HandleAsync(req, default);

        await _service.Received(1).PostAsync(null, Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PassesPlatformSelection_GivenPlatformsSpecified()
    {
        _service.PostAsync(Arg.Any<DateOnly?>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(CreateSuccessResult(CreateImageApod()));

        var ep = Factory.Create<NasaApodPostEndpoint>(_service, _logger);
        var req = new NasaApodPostRequest { Platforms = ["bluesky", "mastodon"] };

        await ep.HandleAsync(req, default);

        await _service.Received(1).PostAsync(
            Arg.Any<DateOnly?>(),
            Arg.Is<IReadOnlyList<string>>(p => p.Contains("bluesky") && p.Contains("mastodon")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Returns200_GivenAllPlatformsSucceed()
    {
        _service.PostAsync(Arg.Any<DateOnly?>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(CreateSuccessResult(CreateImageApod()));

        var ep = Factory.Create<NasaApodPostEndpoint>(_service, _logger);
        var req = new NasaApodPostRequest();

        await ep.HandleAsync(req, default);

        ep.HttpContext.Response.StatusCode.ShouldBe(200);
    }

    [Fact]
    public async Task Returns207_GivenPartialSuccess()
    {
        var result = new ApodPostResult
        {
            ApodEntry = CreateImageApod(),
            PlatformResults =
            [
                new PlatformPostResult { Platform = "bluesky", Success = true, PostId = "p1" },
                new PlatformPostResult { Platform = "mastodon", Success = false, ErrorMessage = "Auth failed", ErrorCode = "AUTH_FAILED" }
            ],
            ImageAttached = true,
            ImageResized = false
        };
        _service.PostAsync(Arg.Any<DateOnly?>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(result);

        var ep = Factory.Create<NasaApodPostEndpoint>(_service, _logger);
        var req = new NasaApodPostRequest();

        await ep.HandleAsync(req, default);

        ep.HttpContext.Response.StatusCode.ShouldBe(207);
    }

    [Fact]
    public async Task Returns502_GivenAllPlatformsFail()
    {
        var result = new ApodPostResult
        {
            ApodEntry = CreateImageApod(),
            PlatformResults =
            [
                new PlatformPostResult { Platform = "bluesky", Success = false, ErrorMessage = "fail", ErrorCode = "ERROR" },
                new PlatformPostResult { Platform = "mastodon", Success = false, ErrorMessage = "fail", ErrorCode = "ERROR" }
            ],
            ImageAttached = true,
            ImageResized = false
        };
        _service.PostAsync(Arg.Any<DateOnly?>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(result);

        var ep = Factory.Create<NasaApodPostEndpoint>(_service, _logger);
        var req = new NasaApodPostRequest();

        await ep.HandleAsync(req, default);

        ep.HttpContext.Response.StatusCode.ShouldBe(502);
    }

    [Fact]
    public async Task ReturnsResultsPerPlatform_GivenMultiplePlatforms()
    {
        var result = new ApodPostResult
        {
            ApodEntry = CreateImageApod(),
            PlatformResults =
            [
                new PlatformPostResult { Platform = "bluesky", Success = true, PostId = "p1", PostUrl = "https://bsky.app/post/1" },
                new PlatformPostResult { Platform = "mastodon", Success = true, PostId = "p2", PostUrl = "https://mastodon.social/post/2" }
            ],
            ImageAttached = true,
            ImageResized = false
        };
        _service.PostAsync(Arg.Any<DateOnly?>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(result);

        var ep = Factory.Create<NasaApodPostEndpoint>(_service, _logger);
        var req = new NasaApodPostRequest();

        await ep.HandleAsync(req, default);

        ep.Response.Results.Count.ShouldBe(2);
        ep.Response.Results[0].Platform.ShouldBe("bluesky");
        ep.Response.Results[1].Platform.ShouldBe("mastodon");
    }

    // --- US2: Date Validation Tests ---

    [Fact]
    public async Task PassesParsedDate_GivenValidDateString()
    {
        _service.PostAsync(Arg.Any<DateOnly?>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(CreateSuccessResult(CreateImageApod()));

        var ep = Factory.Create<NasaApodPostEndpoint>(_service, _logger);
        var req = new NasaApodPostRequest { Date = "2026-02-14" };

        await ep.HandleAsync(req, default);

        await _service.Received(1).PostAsync(
            new DateOnly(2026, 2, 14),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void RejectsInvalidDateFormat_GivenMalformedDate()
    {
        var validator = new NasaApodPostValidator();
        var req = new NasaApodPostRequest { Date = "not-a-date" };

        var result = validator.Validate(req);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Date");
    }

    [Fact]
    public void RejectsFutureDate_GivenTomorrowDate()
    {
        var validator = new NasaApodPostValidator();
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var req = new NasaApodPostRequest { Date = tomorrow.ToString("yyyy-MM-dd") };

        var result = validator.Validate(req);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Date");
    }

    [Fact]
    public void RejectsDateBefore19950616_GivenEarlyDate()
    {
        var validator = new NasaApodPostValidator();
        var req = new NasaApodPostRequest { Date = "1995-06-15" };

        var result = validator.Validate(req);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Date");
    }

    [Fact]
    public void AcceptsValidPastDate_GivenDateInRange()
    {
        var validator = new NasaApodPostValidator();
        var req = new NasaApodPostRequest { Date = "2024-01-15" };

        var result = validator.Validate(req);

        result.Errors.ShouldNotContain(e => e.PropertyName == "Date");
    }

    [Fact]
    public void AcceptsNullDate_GivenNoDateProvided()
    {
        var validator = new NasaApodPostValidator();
        var req = new NasaApodPostRequest { Date = null };

        var result = validator.Validate(req);

        result.Errors.ShouldNotContain(e => e.PropertyName == "Date");
    }

    [Fact]
    public void AcceptsFirstApodDate_GivenExactBoundary()
    {
        var validator = new NasaApodPostValidator();
        var req = new NasaApodPostRequest { Date = "1995-06-16" };

        var result = validator.Validate(req);

        result.Errors.ShouldNotContain(e => e.PropertyName == "Date");
    }
}

using BarretApi.Api.Features.Nasa;
using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Core.Services;
using FastEndpoints;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace BarretApi.Api.UnitTests.Features.Nasa;

public sealed class SatellitePostEndpoint_HandleAsync_Tests
{
    private readonly NasaGibsPostService _service;
    private readonly ILogger<SatellitePostEndpoint> _logger = Substitute.For<ILogger<SatellitePostEndpoint>>();

    public SatellitePostEndpoint_HandleAsync_Tests()
    {
        var socialPostService = new SocialPostService(
            Array.Empty<ISocialPlatformClient>(),
            Substitute.For<ITextShorteningService>(),
            Substitute.For<ITextSplitterService>(),
            Substitute.For<IImageDownloadService>(),
            Substitute.For<IImageResizer>(),
            Substitute.For<IHashtagService>(),
            Substitute.For<ILogger<SocialPostService>>());

        var options = Substitute.For<IOptions<NasaGibsOptions>>();
        options.Value.Returns(new NasaGibsOptions());

        _service = Substitute.For<NasaGibsPostService>(
            Substitute.For<INasaGibsClient>(),
            socialPostService,
            options,
            Substitute.For<ILogger<NasaGibsPostService>>());
    }

    private static SatellitePostResult CreateSuccessResult()
    {
        return new SatellitePostResult(
            Date: new DateOnly(2026, 3, 15),
            Layer: "MODIS_Terra_CorrectedReflectance_TrueColor",
            Title: "Satellite view of Ohio",
            WorldviewUrl: "https://worldview.earthdata.nasa.gov/?v=-84.82,38.40,-80.52,42.32&l=MODIS_Terra_CorrectedReflectance_TrueColor&t=2026-03-15",
            BboxSouth: 38.4,
            BboxWest: -84.82,
            BboxNorth: 42.32,
            BboxEast: -80.52,
            ImageWidth: 1024,
            ImageHeight: 768,
            ImageAttached: true,
            ImageResized: false,
            PlatformResults:
            [
                new PlatformPostResult
                {
                    Platform = "bluesky",
                    Success = true,
                    PostId = "post-1",
                    PostUrl = "https://bsky.app/post/1"
                }
            ]);
    }

    [Fact]
    public async Task ResponseContainsSatelliteMetadata_GivenSuccessfulPost()
    {
        var result = CreateSuccessResult();
        _service.PostAsync(Arg.Any<DateOnly?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<double?>(), Arg.Any<double?>(), Arg.Any<double?>(), Arg.Any<double?>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(result);

        var ep = Factory.Create<SatellitePostEndpoint>(_service, _logger);
        var req = new SatellitePostRequest();

        await ep.HandleAsync(req, default);

        ep.Response.Date.ShouldBe("2026-03-15");
        ep.Response.Layer.ShouldBe("MODIS_Terra_CorrectedReflectance_TrueColor");
        ep.Response.Title.ShouldBe("Satellite view of Ohio");
        ep.Response.WorldviewUrl.ShouldContain("worldview.earthdata.nasa.gov");
        ep.Response.ImageWidth.ShouldBe(1024);
        ep.Response.ImageHeight.ShouldBe(768);
        ep.Response.ImageAttached.ShouldBeTrue();
        ep.Response.ImageResized.ShouldBeFalse();
    }

    [Fact]
    public async Task UsesDefaults_GivenEmptyRequest()
    {
        _service.PostAsync(Arg.Any<DateOnly?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<double?>(), Arg.Any<double?>(), Arg.Any<double?>(), Arg.Any<double?>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(CreateSuccessResult());

        var ep = Factory.Create<SatellitePostEndpoint>(_service, _logger);
        var req = new SatellitePostRequest();

        await ep.HandleAsync(req, default);

        await _service.Received(1).PostAsync(
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Returns200_GivenAllPlatformsSucceed()
    {
        _service.PostAsync(Arg.Any<DateOnly?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<double?>(), Arg.Any<double?>(), Arg.Any<double?>(), Arg.Any<double?>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(CreateSuccessResult());

        var ep = Factory.Create<SatellitePostEndpoint>(_service, _logger);
        var req = new SatellitePostRequest();

        await ep.HandleAsync(req, default);

        ep.HttpContext.Response.StatusCode.ShouldBe(200);
    }

    [Fact]
    public async Task Returns207_GivenPartialSuccess()
    {
        var result = new SatellitePostResult(
            Date: new DateOnly(2026, 3, 15),
            Layer: "MODIS_Terra_CorrectedReflectance_TrueColor",
            Title: "Satellite view of Ohio",
            WorldviewUrl: "https://worldview.earthdata.nasa.gov/?v=-84.82,38.40,-80.52,42.32",
            BboxSouth: 38.4,
            BboxWest: -84.82,
            BboxNorth: 42.32,
            BboxEast: -80.52,
            ImageWidth: 1024,
            ImageHeight: 768,
            ImageAttached: true,
            ImageResized: false,
            PlatformResults:
            [
                new PlatformPostResult { Platform = "bluesky", Success = true, PostId = "p1" },
                new PlatformPostResult { Platform = "mastodon", Success = false, ErrorMessage = "Auth failed", ErrorCode = "AUTH_FAILED" }
            ]);
        _service.PostAsync(Arg.Any<DateOnly?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<double?>(), Arg.Any<double?>(), Arg.Any<double?>(), Arg.Any<double?>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(result);

        var ep = Factory.Create<SatellitePostEndpoint>(_service, _logger);
        var req = new SatellitePostRequest();

        await ep.HandleAsync(req, default);

        ep.HttpContext.Response.StatusCode.ShouldBe(207);
    }

    [Fact]
    public async Task Returns502_GivenAllPlatformsFail()
    {
        var result = new SatellitePostResult(
            Date: new DateOnly(2026, 3, 15),
            Layer: "MODIS_Terra_CorrectedReflectance_TrueColor",
            Title: "Satellite view of Ohio",
            WorldviewUrl: "https://worldview.earthdata.nasa.gov/?v=-84.82,38.40,-80.52,42.32",
            BboxSouth: 38.4,
            BboxWest: -84.82,
            BboxNorth: 42.32,
            BboxEast: -80.52,
            ImageWidth: 1024,
            ImageHeight: 768,
            ImageAttached: true,
            ImageResized: false,
            PlatformResults:
            [
                new PlatformPostResult { Platform = "bluesky", Success = false, ErrorMessage = "fail", ErrorCode = "ERROR" },
                new PlatformPostResult { Platform = "mastodon", Success = false, ErrorMessage = "fail", ErrorCode = "ERROR" }
            ]);
        _service.PostAsync(Arg.Any<DateOnly?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<double?>(), Arg.Any<double?>(), Arg.Any<double?>(), Arg.Any<double?>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(result);

        var ep = Factory.Create<SatellitePostEndpoint>(_service, _logger);
        var req = new SatellitePostRequest();

        await ep.HandleAsync(req, default);

        ep.HttpContext.Response.StatusCode.ShouldBe(502);
    }

    [Fact]
    public async Task Returns422_GivenGibsError()
    {
        _service.PostAsync(Arg.Any<DateOnly?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<double?>(), Arg.Any<double?>(), Arg.Any<double?>(), Arg.Any<double?>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("GIBS returned XML error"));

        var ep = Factory.Create<SatellitePostEndpoint>(_service, _logger);
        var req = new SatellitePostRequest();

        await ep.HandleAsync(req, default);

        ep.HttpContext.Response.StatusCode.ShouldBe(422);
    }

    [Fact]
    public async Task ReturnsResultsPerPlatform_GivenMultiplePlatforms()
    {
        var result = new SatellitePostResult(
            Date: new DateOnly(2026, 3, 15),
            Layer: "MODIS_Terra_CorrectedReflectance_TrueColor",
            Title: "Satellite view of Ohio",
            WorldviewUrl: "https://worldview.earthdata.nasa.gov/?v=-84.82,38.40,-80.52,42.32",
            BboxSouth: 38.4,
            BboxWest: -84.82,
            BboxNorth: 42.32,
            BboxEast: -80.52,
            ImageWidth: 1024,
            ImageHeight: 768,
            ImageAttached: true,
            ImageResized: false,
            PlatformResults:
            [
                new PlatformPostResult { Platform = "bluesky", Success = true, PostId = "p1", PostUrl = "https://bsky.app/post/1" },
                new PlatformPostResult { Platform = "mastodon", Success = true, PostId = "p2", PostUrl = "https://mastodon.social/post/2" }
            ]);
        _service.PostAsync(Arg.Any<DateOnly?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<double?>(), Arg.Any<double?>(), Arg.Any<double?>(), Arg.Any<double?>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(result);

        var ep = Factory.Create<SatellitePostEndpoint>(_service, _logger);
        var req = new SatellitePostRequest();

        await ep.HandleAsync(req, default);

        ep.Response.Results.Count.ShouldBe(2);
        ep.Response.Results[0].Platform.ShouldBe("bluesky");
        ep.Response.Results[1].Platform.ShouldBe("mastodon");
    }

    // --- US2: Date-specific endpoint tests ---

    [Fact]
    public async Task PassesParsedDate_GivenValidDateString()
    {
        _service.PostAsync(Arg.Any<DateOnly?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<double?>(), Arg.Any<double?>(), Arg.Any<double?>(), Arg.Any<double?>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(CreateSuccessResult());

        var ep = Factory.Create<SatellitePostEndpoint>(_service, _logger);
        var req = new SatellitePostRequest { Date = "2026-02-14" };

        await ep.HandleAsync(req, default);

        await _service.Received(1).PostAsync(
            new DateOnly(2026, 2, 14),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<double?>(),
            Arg.Any<double?>(),
            Arg.Any<double?>(),
            Arg.Any<double?>(),
            Arg.Any<int?>(),
            Arg.Any<int?>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResponseDateMatchesRequest_GivenExplicitDate()
    {
        var result = new SatellitePostResult(
            Date: new DateOnly(2026, 2, 14),
            Layer: "MODIS_Terra_CorrectedReflectance_TrueColor",
            Title: "Satellite view of Ohio",
            WorldviewUrl: "https://worldview.earthdata.nasa.gov/?v=-84.82,38.40,-80.52,42.32&t=2026-02-14",
            BboxSouth: 38.4,
            BboxWest: -84.82,
            BboxNorth: 42.32,
            BboxEast: -80.52,
            ImageWidth: 1024,
            ImageHeight: 768,
            ImageAttached: true,
            ImageResized: false,
            PlatformResults: [new PlatformPostResult { Platform = "bluesky", Success = true, PostId = "p1" }]);
        _service.PostAsync(Arg.Any<DateOnly?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<double?>(), Arg.Any<double?>(), Arg.Any<double?>(), Arg.Any<double?>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(result);

        var ep = Factory.Create<SatellitePostEndpoint>(_service, _logger);
        var req = new SatellitePostRequest { Date = "2026-02-14" };

        await ep.HandleAsync(req, default);

        ep.Response.Date.ShouldBe("2026-02-14");
    }

    // --- US3: Layer-specific endpoint tests ---

    [Fact]
    public async Task PassesLayerToService_GivenSpecificLayer()
    {
        _service.PostAsync(Arg.Any<DateOnly?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<double?>(), Arg.Any<double?>(), Arg.Any<double?>(), Arg.Any<double?>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(CreateSuccessResult());

        var ep = Factory.Create<SatellitePostEndpoint>(_service, _logger);
        var req = new SatellitePostRequest { Layer = "VIIRS_SNPP_CorrectedReflectance_TrueColor" };

        await ep.HandleAsync(req, default);

        await _service.Received(1).PostAsync(
            Arg.Any<DateOnly?>(),
            "VIIRS_SNPP_CorrectedReflectance_TrueColor",
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<double?>(),
            Arg.Any<double?>(),
            Arg.Any<double?>(),
            Arg.Any<double?>(),
            Arg.Any<int?>(),
            Arg.Any<int?>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResponseLayerMatchesRequest_GivenSpecificLayer()
    {
        var result = new SatellitePostResult(
            Date: new DateOnly(2026, 3, 15),
            Layer: "VIIRS_SNPP_CorrectedReflectance_TrueColor",
            Title: "Satellite view of Ohio",
            WorldviewUrl: "https://worldview.earthdata.nasa.gov/?v=-84.82,38.40,-80.52,42.32",
            BboxSouth: 38.4,
            BboxWest: -84.82,
            BboxNorth: 42.32,
            BboxEast: -80.52,
            ImageWidth: 1024,
            ImageHeight: 768,
            ImageAttached: true,
            ImageResized: false,
            PlatformResults: [new PlatformPostResult { Platform = "bluesky", Success = true, PostId = "p1" }]);
        _service.PostAsync(Arg.Any<DateOnly?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<double?>(), Arg.Any<double?>(), Arg.Any<double?>(), Arg.Any<double?>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(result);

        var ep = Factory.Create<SatellitePostEndpoint>(_service, _logger);
        var req = new SatellitePostRequest { Layer = "VIIRS_SNPP_CorrectedReflectance_TrueColor" };

        await ep.HandleAsync(req, default);

        ep.Response.Layer.ShouldBe("VIIRS_SNPP_CorrectedReflectance_TrueColor");
    }
}

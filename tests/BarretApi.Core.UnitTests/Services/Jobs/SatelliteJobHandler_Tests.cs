using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Core.Services.Jobs;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace BarretApi.Core.UnitTests.Services.Jobs;

public sealed class SatelliteJobHandler_Tests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

    private readonly INasaGibsPostService _service = Substitute.For<INasaGibsPostService>();

    private SatelliteJobHandler CreateSut()
        => new(_service, NullLogger<SatelliteJobHandler>.Instance);

    private static JobExecutionContext CreateContext(string? argumentsJson = null)
        => new("daily-satellite", "satellite", "run-1", Now, argumentsJson);

    private static SatellitePostResult CreateResult(bool success)
        => new(
            new DateOnly(2026, 8, 19),
            "MODIS_Terra_CorrectedReflectance_TrueColor",
            "Satellite view of Ohio",
            "https://worldview.earthdata.nasa.gov/",
            38.0,
            -85.0,
            42.5,
            -80.0,
            1200,
            900,
            true,
            false,
            [new PlatformPostResult { Platform = "bluesky", Success = success, ErrorMessage = success ? null : "boom" }]);

    [Fact]
    public void UsesTheExpectedJobType()
    {
        CreateSut().JobType.ShouldBe("satellite");
    }

    [Fact]
    public async Task PassesNullsSoConfigDefaultsApply_GivenNoArguments()
    {
        _service.PostAsync(
                Arg.Any<DateOnly?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<double?>(), Arg.Any<double?>(), Arg.Any<double?>(), Arg.Any<double?>(),
                Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(CreateResult(success: true));

        await CreateSut().ExecuteAsync(CreateContext());

        await _service.Received(1).PostAsync(
            null, null, null, null, null, null, null, null, null, null,
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PassesEveryConfiguredArgument()
    {
        _service.PostAsync(
                Arg.Any<DateOnly?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<double?>(), Arg.Any<double?>(), Arg.Any<double?>(), Arg.Any<double?>(),
                Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(CreateResult(success: true));

        await CreateSut().ExecuteAsync(CreateContext(
            """{"layer":"VIIRS","title":"Ohio today","description":"Daily view","platforms":["mastodon"],"imageWidth":800,"imageHeight":600}"""));

        await _service.Received(1).PostAsync(
            null,
            "VIIRS",
            "Ohio today",
            "Daily view",
            null, null, null, null,
            800,
            600,
            Arg.Is<IReadOnlyList<string>>(p => p!.Count == 1 && p[0] == "mastodon"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Succeeds_GivenEveryPlatformPublished()
    {
        _service.PostAsync(
                Arg.Any<DateOnly?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<double?>(), Arg.Any<double?>(), Arg.Any<double?>(), Arg.Any<double?>(),
                Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(CreateResult(success: true));

        var result = await CreateSut().ExecuteAsync(CreateContext());

        result.Success.ShouldBeTrue();
        result.Summary!.ShouldContain("Satellite view of Ohio");
    }

    [Fact]
    public async Task Fails_GivenAPlatformFailed()
    {
        _service.PostAsync(
                Arg.Any<DateOnly?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<double?>(), Arg.Any<double?>(), Arg.Any<double?>(), Arg.Any<double?>(),
                Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(CreateResult(success: false));

        var result = await CreateSut().ExecuteAsync(CreateContext());

        result.Success.ShouldBeFalse();
    }
}

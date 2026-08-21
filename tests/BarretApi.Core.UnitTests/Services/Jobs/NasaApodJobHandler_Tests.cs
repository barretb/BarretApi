using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Core.Services.Jobs;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace BarretApi.Core.UnitTests.Services.Jobs;

public sealed class NasaApodJobHandler_Tests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

    private readonly INasaApodPostService _service = Substitute.For<INasaApodPostService>();

    private NasaApodJobHandler CreateSut()
        => new(_service, NullLogger<NasaApodJobHandler>.Instance);

    private static JobExecutionContext CreateContext(string? argumentsJson = null)
        => new("daily-apod", "nasa-apod", "run-1", Now, argumentsJson);

    private static ApodPostResult CreateResult(bool success)
        => new()
        {
            ApodEntry = new ApodEntry
            {
                Title = "Pillars of Creation",
                Date = new DateOnly(2026, 8, 20),
                Explanation = "A nebula.",
                Url = "https://apod.nasa.gov/apod/image/2608/pillars.jpg",
                MediaType = ApodMediaType.Image
            },
            PlatformResults =
            [
                new PlatformPostResult { Platform = "bluesky", Success = success, ErrorMessage = success ? null : "boom" }
            ],
            ImageAttached = true,
            ImageResized = false
        };

    private static ApodPostResult CreatePartialResult()
        => new()
        {
            ApodEntry = new ApodEntry
            {
                Title = "Pillars of Creation",
                Date = new DateOnly(2026, 8, 20),
                Explanation = "A nebula.",
                Url = "https://apod.nasa.gov/apod/image/2608/pillars.jpg",
                MediaType = ApodMediaType.Image
            },
            PlatformResults =
            [
                new PlatformPostResult { Platform = "bluesky", Success = true },
                new PlatformPostResult { Platform = "mastodon", Success = false, ErrorMessage = "boom" }
            ],
            ImageAttached = true,
            ImageResized = false
        };

    [Fact]
    public void UsesTheExpectedJobType()
    {
        CreateSut().JobType.ShouldBe("nasa-apod");
    }

    [Fact]
    public async Task AlwaysRequestsTodaysEntry()
    {
        _service.PostAsync(Arg.Any<DateOnly?>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(CreateResult(success: true));

        await CreateSut().ExecuteAsync(CreateContext());

        await _service.Received(1).PostAsync(
            null,
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PassesTheConfiguredPlatforms()
    {
        _service.PostAsync(Arg.Any<DateOnly?>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(CreateResult(success: true));

        await CreateSut().ExecuteAsync(CreateContext("""{"platforms":["bluesky","mastodon"]}"""));

        await _service.Received(1).PostAsync(
            null,
            Arg.Is<IReadOnlyList<string>>(p => p!.Count == 2 && p[0] == "bluesky"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Succeeds_GivenEveryPlatformPublished()
    {
        _service.PostAsync(Arg.Any<DateOnly?>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(CreateResult(success: true));

        var result = await CreateSut().ExecuteAsync(CreateContext());

        result.Success.ShouldBeTrue();
        result.Summary!.ShouldContain("Pillars of Creation");
    }

    [Fact]
    public async Task Fails_GivenEveryPlatformFailed()
    {
        _service.PostAsync(Arg.Any<DateOnly?>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(CreateResult(success: false));

        var result = await CreateSut().ExecuteAsync(CreateContext());

        result.Success.ShouldBeFalse();
    }

    [Fact]
    public async Task Succeeds_GivenOnlySomePlatformsFailed()
    {
        _service.PostAsync(Arg.Any<DateOnly?>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(CreatePartialResult());

        var result = await CreateSut().ExecuteAsync(CreateContext());

        result.Success.ShouldBeTrue();
        result.Summary!.ShouldContain("bluesky");
        result.Summary!.ShouldContain("mastodon");
        result.Summary!.ShouldContain("boom");
    }
}

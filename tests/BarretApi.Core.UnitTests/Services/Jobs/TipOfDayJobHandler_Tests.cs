using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Core.Services.Jobs;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace BarretApi.Core.UnitTests.Services.Jobs;

public sealed class TipOfDayJobHandler_Tests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

    private readonly ITipOfDayService _service = Substitute.For<ITipOfDayService>();

    private TipOfDayJobHandler CreateSut()
        => new(_service, NullLogger<TipOfDayJobHandler>.Instance);

    private static JobExecutionContext CreateContext(string? argumentsJson)
        => new("daily-tip", "tip-of-day", "run-1", Now, argumentsJson);

    private static TipOfDayPostResult CreateResult(bool success)
        => new()
        {
            SelectedTip = new TipOfDayRecord
            {
                TipId = "tip-1",
                Category = "dotnet",
                Tip = "Use TimeProvider."
            },
            PlatformResults =
            [
                new PlatformPostResult { Platform = "bluesky", Success = success, ErrorMessage = success ? null : "boom" }
            ],
            TipMarkedPosted = success,
            AttemptedAtUtc = Now
        };

    private static TipOfDayPostResult CreatePartialResult()
        => new()
        {
            SelectedTip = new TipOfDayRecord
            {
                TipId = "tip-1",
                Category = "dotnet",
                Tip = "Use TimeProvider."
            },
            PlatformResults =
            [
                new PlatformPostResult { Platform = "bluesky", Success = true },
                new PlatformPostResult { Platform = "linkedin", Success = false, ErrorMessage = "boom" }
            ],
            TipMarkedPosted = true,
            AttemptedAtUtc = Now
        };

    [Fact]
    public void UsesTheExpectedJobType()
    {
        CreateSut().JobType.ShouldBe("tip-of-day");
    }

    [Fact]
    public async Task Fails_GivenNoArguments()
    {
        var result = await CreateSut().ExecuteAsync(CreateContext(null));

        result.Success.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("category");
        await _service.DidNotReceiveWithAnyArgs().SelectAndPostAsync(default!, default);
    }

    [Fact]
    public async Task Fails_GivenABlankCategory()
    {
        var result = await CreateSut().ExecuteAsync(CreateContext("""{"category":"  "}"""));

        result.Success.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("category");
    }

    [Fact]
    public async Task PassesTheCategoryPlatformsAndLeader()
    {
        _service.SelectAndPostAsync(Arg.Any<TipOfDayPostCommand>(), Arg.Any<CancellationToken>())
            .Returns(CreateResult(success: true));

        await CreateSut().ExecuteAsync(CreateContext(
            """{"category":"dotnet","platforms":["bluesky","mastodon"],"leader":"Tip:"}"""));

        await _service.Received(1).SelectAndPostAsync(
            Arg.Is<TipOfDayPostCommand>(c =>
                c!.Category == "dotnet"
                && c.Platforms.Count == 2
                && c.Leader == "Tip:"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Succeeds_GivenEveryPlatformPublished()
    {
        _service.SelectAndPostAsync(Arg.Any<TipOfDayPostCommand>(), Arg.Any<CancellationToken>())
            .Returns(CreateResult(success: true));

        var result = await CreateSut().ExecuteAsync(CreateContext("""{"category":"dotnet"}"""));

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task Fails_GivenEveryPlatformFailed()
    {
        _service.SelectAndPostAsync(Arg.Any<TipOfDayPostCommand>(), Arg.Any<CancellationToken>())
            .Returns(CreateResult(success: false));

        var result = await CreateSut().ExecuteAsync(CreateContext("""{"category":"dotnet"}"""));

        result.Success.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("bluesky");
    }

    [Fact]
    public async Task Succeeds_GivenOnlySomePlatformsFailed()
    {
        _service.SelectAndPostAsync(Arg.Any<TipOfDayPostCommand>(), Arg.Any<CancellationToken>())
            .Returns(CreatePartialResult());

        var result = await CreateSut().ExecuteAsync(CreateContext("""{"category":"dotnet"}"""));

        result.Success.ShouldBeTrue();
        result.Summary!.ShouldContain("bluesky");
        result.Summary!.ShouldContain("linkedin");
        result.Summary!.ShouldContain("boom");
    }

    [Fact]
    public async Task RecordsTheFailedPlatformInErrorMessage_GivenOnlySomePlatformsFailed()
    {
        _service.SelectAndPostAsync(Arg.Any<TipOfDayPostCommand>(), Arg.Any<CancellationToken>())
            .Returns(CreatePartialResult());

        var result = await CreateSut().ExecuteAsync(CreateContext("""{"category":"dotnet"}"""));

        result.Success.ShouldBeTrue();
        result.IsPartialSuccess.ShouldBeTrue();
        result.ErrorMessage!.ShouldContain("linkedin");
        result.ErrorMessage!.ShouldContain("boom");
    }
}

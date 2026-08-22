using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Core.Services.Jobs;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace BarretApi.Core.UnitTests.Services.Jobs;

public sealed class RssPromotionJobHandler_Tests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

    private readonly IBlogPromotionOrchestrator _orchestrator =
        Substitute.For<IBlogPromotionOrchestrator>();

    private RssPromotionJobHandler CreateSut()
        => new(_orchestrator, NullLogger<RssPromotionJobHandler>.Instance);

    private static JobExecutionContext CreateContext(string? argumentsJson = null)
        => new("promote-blog", "rss-promotion", "run-1", Now, argumentsJson);

    private static PromotionRunSummary CreateSummary(params PromotionEntryFailure[] failures)
    {
        var summary = new PromotionRunSummary
        {
            RunId = "promo-run-1",
            StartedAtUtc = Now,
            CompletedAtUtc = Now.AddSeconds(4),
            EntriesEvaluated = 5,
            NewPostsAttempted = 1,
            NewPostsSucceeded = failures.Length == 0 ? 1 : 0
        };

        summary.Failures.AddRange(failures);
        return summary;
    }

    private static PromotionEntryFailure CreateFailure()
        => new()
        {
            EntryIdentity = "entry-1",
            CanonicalUrl = "https://example.com/post",
            Phase = PromotionPhase.Initial,
            Platform = "bluesky",
            ErrorCode = "PUBLISH_FAILED",
            ErrorMessage = "rate limited"
        };

    [Fact]
    public void UsesTheExpectedJobType()
    {
        CreateSut().JobType.ShouldBe("rss-promotion");
    }

    [Fact]
    public async Task PassesNulls_GivenNoArguments()
    {
        _orchestrator.RunAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(CreateSummary());

        await CreateSut().ExecuteAsync(CreateContext());

        await _orchestrator.Received(1).RunAsync(null, null, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PassesEveryConfiguredOverride()
    {
        _orchestrator.RunAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(CreateSummary());

        await CreateSut().ExecuteAsync(CreateContext(
            """{"feedUrl":"https://example.com/feed.xml","header":"New post","recentDaysWindow":14}"""));

        await _orchestrator.Received(1).RunAsync(
            "https://example.com/feed.xml",
            "New post",
            14,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Succeeds_GivenNoFailures()
    {
        _orchestrator.RunAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(CreateSummary());

        var result = await CreateSut().ExecuteAsync(CreateContext());

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task Fails_GivenTheRunReportedFailures()
    {
        _orchestrator.RunAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(CreateSummary(CreateFailure()));

        var result = await CreateSut().ExecuteAsync(CreateContext());

        result.Success.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("rate limited");
    }
}

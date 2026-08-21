using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Core.Services.Jobs;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace BarretApi.Core.UnitTests.Services.Jobs;

public sealed class ProcessScheduledPostsJobHandler_Tests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

    private readonly IScheduledSocialPostProcessor _processor =
        Substitute.For<IScheduledSocialPostProcessor>();

    private ProcessScheduledPostsJobHandler CreateSut()
        => new(_processor, NullLogger<ProcessScheduledPostsJobHandler>.Instance);

    private static JobExecutionContext CreateContext(string? argumentsJson = null)
        => new("drain-scheduled", "process-scheduled-posts", "run-1", Now, argumentsJson);

    private static ScheduledPostProcessingSummary CreateSummary(
        int attempted = 1,
        int succeeded = 1,
        int failed = 0)
        => new()
        {
            RunId = "sched-run-1",
            StartedAtUtc = Now,
            CompletedAtUtc = Now.AddSeconds(2),
            DueCount = attempted,
            AttemptedCount = attempted,
            SucceededCount = succeeded,
            FailedCount = failed,
            SkippedCount = 0
        };

    [Fact]
    public void UsesTheExpectedJobType()
    {
        CreateSut().JobType.ShouldBe("process-scheduled-posts");
    }

    [Fact]
    public async Task PassesNullMaxCount_GivenNoArguments()
    {
        _processor.ProcessDueAsync(Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(CreateSummary());

        await CreateSut().ExecuteAsync(CreateContext());

        await _processor.Received(1).ProcessDueAsync(null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PassesTheConfiguredMaxCount()
    {
        _processor.ProcessDueAsync(Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(CreateSummary());

        await CreateSut().ExecuteAsync(CreateContext("""{"maxCount":25}"""));

        await _processor.Received(1).ProcessDueAsync(25, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Succeeds_GivenNothingWasDue()
    {
        _processor.ProcessDueAsync(Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(CreateSummary(attempted: 0, succeeded: 0));

        var result = await CreateSut().ExecuteAsync(CreateContext());

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task Succeeds_GivenEveryPostPublished()
    {
        _processor.ProcessDueAsync(Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(CreateSummary(attempted: 3, succeeded: 3));

        var result = await CreateSut().ExecuteAsync(CreateContext());

        result.Success.ShouldBeTrue();
        result.Summary!.ShouldContain("3");
    }

    [Fact]
    public async Task Fails_GivenAnyPostFailed()
    {
        _processor.ProcessDueAsync(Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(CreateSummary(attempted: 3, succeeded: 2, failed: 1));

        var result = await CreateSut().ExecuteAsync(CreateContext());

        result.Success.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("1");
    }
}

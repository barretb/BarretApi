using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;

namespace BarretApi.Core.UnitTests.Services;

public sealed class JobDispatcher_RunManuallyAsync_Tests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

    private readonly IScheduledJobRepository _jobRepository = Substitute.For<IScheduledJobRepository>();
    private readonly IJobRunRepository _runRepository = Substitute.For<IJobRunRepository>();
    private readonly IEmailNotificationService _emailNotificationService =
        Substitute.For<IEmailNotificationService>();
    private readonly FakeTimeProvider _timeProvider = new(Now);
    private readonly List<JobRunRecord> _recordedRuns = [];

    public JobDispatcher_RunManuallyAsync_Tests()
    {
        _jobRepository.TryClaimAsync(Arg.Any<ScheduledJobRecord>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var job = callInfo.Arg<ScheduledJobRecord>()!;
                job.RunState = JobRunState.Running;
                job.ClaimedAtUtc = callInfo.ArgAt<DateTimeOffset>(1);
                return true;
            });

        _runRepository.AddAsync(Arg.Any<JobRunRecord>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                _recordedRuns.Add(callInfo.Arg<JobRunRecord>()!);
                return Task.CompletedTask;
            });
    }

    private sealed class StubHandler : IScheduledJobHandler
    {
        public string JobType => "tip-of-day";
        public int CallCount { get; private set; }

        public Task<JobExecutionResult> ExecuteAsync(
            JobExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(JobExecutionResult.Ok("posted"));
        }
    }

    private static ScheduledJobRecord CreateJob()
        => new()
        {
            Name = "daily-tip",
            DisplayName = "Daily tip",
            JobType = "tip-of-day",
            CronExpression = "0 12 * * *",
            TimeZoneId = "UTC",
            IsEnabled = true,
            NextRunUtc = Now.AddHours(6),
            MaxRetryCount = 0,
            RetryBaseDelaySeconds = 0,
            CreatedAtUtc = Now.AddDays(-1),
            UpdatedAtUtc = Now.AddDays(-1),
            ETag = "etag-1"
        };

    private JobDispatcher CreateSut(IScheduledJobHandler handler, int claimTimeoutMinutes = 30)
        => new(
            _jobRepository,
            _runRepository,
            new JobHandlerRegistry([handler]),
            Options.Create(new JobSchedulerOptions
            {
                ClaimTimeoutMinutes = claimTimeoutMinutes,
                TableStorage = new JobSchedulerTableStorageOptions { ConnectionString = "UseDevelopmentStorage=true" }
            }),
            _timeProvider,
            NullLogger<JobDispatcher>.Instance,
            _emailNotificationService);

    [Fact]
    public async Task ReturnsNotFound_GivenNoSuchJob()
    {
        _jobRepository.GetByNameAsync("missing", Arg.Any<CancellationToken>())
            .Returns((ScheduledJobRecord?)null);

        var result = await CreateSut(new StubHandler()).RunManuallyAsync("missing");

        result.Outcome.ShouldBe(ManualRunOutcome.NotFound);
        result.Run.ShouldBeNull();
    }

    [Fact]
    public async Task RunsTheHandler_AndRecordsAManualRun()
    {
        var handler = new StubHandler();
        var job = CreateJob();
        _jobRepository.GetByNameAsync("daily-tip", Arg.Any<CancellationToken>()).Returns(job);

        var result = await CreateSut(handler).RunManuallyAsync("daily-tip");

        result.Outcome.ShouldBe(ManualRunOutcome.Completed);
        handler.CallCount.ShouldBe(1);
        var run = _recordedRuns.ShouldHaveSingleItem();
        run.TriggerType.ShouldBe(JobTriggerType.Manual);
        run.ScheduledForUtc.ShouldBeNull();
        run.Status.ShouldBe(JobRunStatus.Succeeded);
    }

    [Fact]
    public async Task LeavesNextRunUnchanged()
    {
        var job = CreateJob();
        var originalNextRun = job.NextRunUtc;
        _jobRepository.GetByNameAsync("daily-tip", Arg.Any<CancellationToken>()).Returns(job);

        await CreateSut(new StubHandler()).RunManuallyAsync("daily-tip");

        job.NextRunUtc.ShouldBe(originalNextRun);
    }

    [Fact]
    public async Task RunsADisabledJob()
    {
        var handler = new StubHandler();
        var job = CreateJob();
        job.IsEnabled = false;
        _jobRepository.GetByNameAsync("daily-tip", Arg.Any<CancellationToken>()).Returns(job);

        var result = await CreateSut(handler).RunManuallyAsync("daily-tip");

        result.Outcome.ShouldBe(ManualRunOutcome.Completed);
        handler.CallCount.ShouldBe(1);
        job.IsEnabled.ShouldBeFalse();
    }

    [Fact]
    public async Task ReturnsBusy_GivenAFreshClaimIsInProgress()
    {
        var handler = new StubHandler();
        var job = CreateJob();
        job.RunState = JobRunState.Running;
        job.ClaimedAtUtc = Now.AddMinutes(-5);
        _jobRepository.GetByNameAsync("daily-tip", Arg.Any<CancellationToken>()).Returns(job);

        var result = await CreateSut(handler).RunManuallyAsync("daily-tip");

        result.Outcome.ShouldBe(ManualRunOutcome.Busy);
        handler.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task ReturnsBusy_GivenTheClaimIsLostToAnotherWriter()
    {
        _jobRepository.TryClaimAsync(Arg.Any<ScheduledJobRecord>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _jobRepository.GetByNameAsync("daily-tip", Arg.Any<CancellationToken>()).Returns(CreateJob());

        var result = await CreateSut(new StubHandler()).RunManuallyAsync("daily-tip");

        result.Outcome.ShouldBe(ManualRunOutcome.Busy);
    }

    [Fact]
    public async Task RunsAnyway_GivenTheExistingClaimIsStale()
    {
        var handler = new StubHandler();
        var job = CreateJob();
        job.RunState = JobRunState.Running;
        job.ClaimedAtUtc = Now.AddMinutes(-31);
        _jobRepository.GetByNameAsync("daily-tip", Arg.Any<CancellationToken>()).Returns(job);

        var result = await CreateSut(handler, claimTimeoutMinutes: 30).RunManuallyAsync("daily-tip");

        result.Outcome.ShouldBe(ManualRunOutcome.Completed);
        handler.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task DoesNotDisableTheJob_GivenAManualRunOfAMisconfiguredJob()
    {
        var job = CreateJob();
        job.JobType = "gone-missing";
        _jobRepository.GetByNameAsync("daily-tip", Arg.Any<CancellationToken>()).Returns(job);

        var result = await CreateSut(new StubHandler()).RunManuallyAsync("daily-tip");

        result.Outcome.ShouldBe(ManualRunOutcome.Completed);
        job.IsEnabled.ShouldBeTrue();
        _recordedRuns.ShouldHaveSingleItem().Status.ShouldBe(JobRunStatus.Failed);
    }

    [Fact]
    public async Task NotifiesFailure_GivenAManualRunOfAMisconfiguredJob()
    {
        var job = CreateJob();
        job.JobType = "gone-missing";
        _jobRepository.GetByNameAsync("daily-tip", Arg.Any<CancellationToken>()).Returns(job);

        await CreateSut(new StubHandler()).RunManuallyAsync("daily-tip");

        await _emailNotificationService.Received(1).SendPostFailureNotificationAsync(
            "job:daily-tip",
            Arg.Any<string>(),
            Arg.Any<IDictionary<string, string>>(),
            Arg.Any<CancellationToken>());
    }

}

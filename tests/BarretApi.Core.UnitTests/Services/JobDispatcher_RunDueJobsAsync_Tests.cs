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

public sealed class JobDispatcher_RunDueJobsAsync_Tests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

    private readonly IScheduledJobRepository _jobRepository = Substitute.For<IScheduledJobRepository>();
    private readonly IJobRunRepository _runRepository = Substitute.For<IJobRunRepository>();
    private readonly FakeTimeProvider _timeProvider = new(Now);
    private readonly List<JobRunRecord> _recordedRuns = [];

    public JobDispatcher_RunDueJobsAsync_Tests()
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

    private sealed class StubHandler(string jobType, Func<JobExecutionContext, JobExecutionResult> behavior)
        : IScheduledJobHandler
    {
        public string JobType { get; } = jobType;
        public int CallCount { get; private set; }
        public JobExecutionContext? LastContext { get; private set; }

        public Task<JobExecutionResult> ExecuteAsync(
            JobExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastContext = context;
            return Task.FromResult(behavior(context));
        }
    }

    private static ScheduledJobRecord CreateJob(
        string name = "daily-tip",
        string cron = "0 12 * * *",
        DateTimeOffset? nextRunUtc = null)
        => new()
        {
            Name = name,
            DisplayName = name,
            JobType = "tip-of-day",
            CronExpression = cron,
            TimeZoneId = "UTC",
            IsEnabled = true,
            NextRunUtc = nextRunUtc ?? Now.AddMinutes(-1),
            MaxRetryCount = 0,
            RetryBaseDelaySeconds = 0,
            CreatedAtUtc = Now.AddDays(-1),
            UpdatedAtUtc = Now.AddDays(-1),
            ETag = "etag-1"
        };

    private JobDispatcher CreateSut(params IScheduledJobHandler[] handlers)
        => new(
            _jobRepository,
            _runRepository,
            new JobHandlerRegistry(handlers),
            Options.Create(new JobSchedulerOptions
            {
                TableStorage = new JobSchedulerTableStorageOptions { ConnectionString = "UseDevelopmentStorage=true" }
            }),
            _timeProvider,
            NullLogger<JobDispatcher>.Instance);

    private void SetDue(params ScheduledJobRecord[] jobs)
        => _jobRepository.GetDueAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(jobs);

    [Fact]
    public async Task ExecutesTheRegisteredHandler_GivenADueJob()
    {
        var handler = new StubHandler("tip-of-day", _ => JobExecutionResult.Ok("posted"));
        SetDue(CreateJob());

        var executed = await CreateSut(handler).RunDueJobsAsync();

        executed.ShouldBe(1);
        handler.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task PassesTheStoredArgumentsToTheHandler()
    {
        var handler = new StubHandler("tip-of-day", _ => JobExecutionResult.Ok());
        var job = CreateJob();
        job.ArgumentsJson = """{"category":"dotnet"}""";
        SetDue(job);

        await CreateSut(handler).RunDueJobsAsync();

        handler.LastContext!.ArgumentsJson.ShouldBe("""{"category":"dotnet"}""");
        handler.LastContext.JobName.ShouldBe("daily-tip");
        handler.LastContext.RunId.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task RecordsASucceededRun()
    {
        var handler = new StubHandler("tip-of-day", _ => JobExecutionResult.Ok("posted 1 tip"));
        SetDue(CreateJob());

        await CreateSut(handler).RunDueJobsAsync();

        var run = _recordedRuns.ShouldHaveSingleItem();
        run.Status.ShouldBe(JobRunStatus.Succeeded);
        run.TriggerType.ShouldBe(JobTriggerType.Scheduled);
        run.JobName.ShouldBe("daily-tip");
        run.AttemptCount.ShouldBe(1);
        run.Summary.ShouldBe("posted 1 tip");
        run.CompletedAtUtc.ShouldNotBeNull();
    }

    [Fact]
    public async Task ClearsTheClaimAndRecordsLastRunState_OnSuccess()
    {
        var handler = new StubHandler("tip-of-day", _ => JobExecutionResult.Ok());
        var job = CreateJob();
        job.ConsecutiveFailureCount = 3;
        SetDue(job);

        await CreateSut(handler).RunDueJobsAsync();

        job.RunState.ShouldBe(JobRunState.Idle);
        job.ClaimedAtUtc.ShouldBeNull();
        job.LastRunStatus.ShouldBe(JobRunStatus.Succeeded);
        job.LastRunUtc.ShouldBe(Now);
        job.LastRunError.ShouldBeNull();
        job.ConsecutiveFailureCount.ShouldBe(0);
        await _jobRepository.Received().UpdateAsync(job, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CollapsesMissedOccurrencesIntoOneCatchUpRun()
    {
        var handler = new StubHandler("tip-of-day", _ => JobExecutionResult.Ok());
        var job = CreateJob(cron: "0 12 * * *", nextRunUtc: Now.AddDays(-5));
        SetDue(job);

        await CreateSut(handler).RunDueJobsAsync();

        handler.CallCount.ShouldBe(1);
        job.NextRunUtc.ShouldBe(new DateTimeOffset(2026, 8, 21, 12, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task SkipsAJobThatIsAlreadyRunning()
    {
        var handler = new StubHandler("tip-of-day", _ => JobExecutionResult.Ok());
        var job = CreateJob();
        job.RunState = JobRunState.Running;
        job.ClaimedAtUtc = Now.AddMinutes(-1);
        SetDue(job);

        var executed = await CreateSut(handler).RunDueJobsAsync();

        executed.ShouldBe(0);
        handler.CallCount.ShouldBe(0);
        _recordedRuns.ShouldBeEmpty();
    }

    [Fact]
    public async Task ReclaimsAJobWhoseClaimIsOlderThanTheTimeout()
    {
        var handler = new StubHandler("tip-of-day", _ => JobExecutionResult.Ok());
        var job = CreateJob();
        job.RunState = JobRunState.Running;
        job.ClaimedAtUtc = Now.AddHours(-2);
        SetDue(job);

        var executed = await CreateSut(handler).RunDueJobsAsync();

        executed.ShouldBe(1);
        handler.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task TreatsAMissingClaimTimestampAsStale()
    {
        var handler = new StubHandler("tip-of-day", _ => JobExecutionResult.Ok());
        var job = CreateJob();
        job.RunState = JobRunState.Running;
        job.ClaimedAtUtc = null;
        SetDue(job);

        var executed = await CreateSut(handler).RunDueJobsAsync();

        executed.ShouldBe(1);
    }

    [Fact]
    public async Task SkipsAJobWhoseClaimIsLostToAnotherWriter()
    {
        var handler = new StubHandler("tip-of-day", _ => JobExecutionResult.Ok());
        _jobRepository.TryClaimAsync(Arg.Any<ScheduledJobRecord>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(false);
        SetDue(CreateJob());

        var executed = await CreateSut(handler).RunDueJobsAsync();

        executed.ShouldBe(0);
        handler.CallCount.ShouldBe(0);
        _recordedRuns.ShouldBeEmpty();
    }

    [Fact]
    public async Task ContinuesToTheNextJob_GivenOneJobThrows()
    {
        var failing = new StubHandler("tip-of-day", _ => throw new InvalidOperationException("boom"));
        var succeeding = new StubHandler("nasa-apod", _ => JobExecutionResult.Ok());
        var second = CreateJob(name: "apod");
        second.JobType = "nasa-apod";
        SetDue(CreateJob(), second);

        await CreateSut(failing, succeeding).RunDueJobsAsync();

        succeeding.CallCount.ShouldBe(1);
        _recordedRuns.Count.ShouldBe(2);
    }

    [Fact]
    public async Task RecordsAFailedRun_GivenTheHandlerThrows()
    {
        var handler = new StubHandler("tip-of-day", _ => throw new InvalidOperationException("boom"));
        var job = CreateJob();
        SetDue(job);

        await CreateSut(handler).RunDueJobsAsync();

        var run = _recordedRuns.ShouldHaveSingleItem();
        run.Status.ShouldBe(JobRunStatus.Failed);
        run.ErrorMessage!.ShouldContain("boom");
        job.ConsecutiveFailureCount.ShouldBe(1);
        job.LastRunStatus.ShouldBe(JobRunStatus.Failed);
        job.RunState.ShouldBe(JobRunState.Idle);
    }

    [Fact]
    public async Task ReschedulesEvenWhenTheRunFails()
    {
        var handler = new StubHandler("tip-of-day", _ => JobExecutionResult.Fail("nope"));
        var job = CreateJob();
        SetDue(job);

        await CreateSut(handler).RunDueJobsAsync();

        job.NextRunUtc.ShouldBe(new DateTimeOffset(2026, 8, 21, 12, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task DisablesTheJob_GivenNoHandlerIsRegisteredForItsType()
    {
        var job = CreateJob();
        job.JobType = "gone-missing";
        SetDue(job);

        await CreateSut(new StubHandler("tip-of-day", _ => JobExecutionResult.Ok())).RunDueJobsAsync();

        job.IsEnabled.ShouldBeFalse();
        job.RunState.ShouldBe(JobRunState.Idle);
        _recordedRuns.ShouldHaveSingleItem().Status.ShouldBe(JobRunStatus.Failed);
    }

    [Fact]
    public async Task DisablesTheJob_GivenItsCronExpressionCannotBeParsed()
    {
        var handler = new StubHandler("tip-of-day", _ => JobExecutionResult.Ok());
        var job = CreateJob(cron: "not a cron");
        SetDue(job);

        await CreateSut(handler).RunDueJobsAsync();

        job.IsEnabled.ShouldBeFalse();
        handler.CallCount.ShouldBe(0);
        _recordedRuns.ShouldHaveSingleItem().ErrorMessage!.ShouldContain("Cron");
    }

    [Fact]
    public async Task ReturnsZero_GivenNothingIsDue()
    {
        SetDue();

        var executed = await CreateSut(new StubHandler("tip-of-day", _ => JobExecutionResult.Ok()))
            .RunDueJobsAsync();

        executed.ShouldBe(0);
    }
}

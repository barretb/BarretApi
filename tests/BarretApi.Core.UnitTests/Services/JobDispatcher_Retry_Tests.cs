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

public sealed class JobDispatcher_Retry_Tests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

    private readonly IScheduledJobRepository _jobRepository = Substitute.For<IScheduledJobRepository>();
    private readonly IJobRunRepository _runRepository = Substitute.For<IJobRunRepository>();
    private readonly IEmailNotificationService _emailNotificationService =
        Substitute.For<IEmailNotificationService>();
    private readonly FakeTimeProvider _timeProvider = new(Now);
    private readonly List<JobRunRecord> _recordedRuns = [];

    public JobDispatcher_Retry_Tests()
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

    private sealed class SequenceHandler(params Func<JobExecutionResult>[] behaviors) : IScheduledJobHandler
    {
        private int _index;

        public string JobType => "tip-of-day";
        public int CallCount { get; private set; }

        public Task<JobExecutionResult> ExecuteAsync(
            JobExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            var behavior = behaviors[Math.Min(_index++, behaviors.Length - 1)];
            return Task.FromResult(behavior());
        }
    }

    /// <summary>
    /// Simulates a genuine shutdown: cancels the shared token as part of "running" and then
    /// observes that same, now-cancelled token — unlike a mere <c>throw new
    /// OperationCanceledException()</c>, this proves the dispatcher's abort path only fires
    /// when the caller actually asked for cancellation.
    /// </summary>
    private sealed class CancellingHandler(CancellationTokenSource cancellationTokenSource) : IScheduledJobHandler
    {
        public string JobType => "tip-of-day";
        public int CallCount { get; private set; }

        public Task<JobExecutionResult> ExecuteAsync(
            JobExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            cancellationTokenSource.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(JobExecutionResult.Fail("unreachable"));
        }
    }

    /// <summary>
    /// Simulates an HttpClient timeout: throws the same exception type .NET's HttpClient
    /// throws on its default 100s timeout, but without the caller ever requesting
    /// cancellation. This must be retried like any other failure, not treated as shutdown.
    /// </summary>
    private sealed class TimingOutHandler : IScheduledJobHandler
    {
        public string JobType => "tip-of-day";
        public int CallCount { get; private set; }

        public Task<JobExecutionResult> ExecuteAsync(
            JobExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            throw new TaskCanceledException("The request timed out.");
        }
    }

    /// <summary>
    /// Cancels the shared token only after returning a failure, so the cancellation is
    /// observed by <c>Task.Delay</c> during the retry backoff rather than during execution.
    /// </summary>
    private sealed class CancelDuringBackoffHandler(CancellationTokenSource cancellationTokenSource) : IScheduledJobHandler
    {
        public string JobType => "tip-of-day";
        public int CallCount { get; private set; }

        public Task<JobExecutionResult> ExecuteAsync(
            JobExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            cancellationTokenSource.Cancel();
            return Task.FromResult(JobExecutionResult.Fail("transient"));
        }
    }

    private static ScheduledJobRecord CreateJob(int maxRetryCount)
        => new()
        {
            Name = "daily-tip",
            DisplayName = "Daily tip",
            JobType = "tip-of-day",
            CronExpression = "0 12 * * *",
            TimeZoneId = "UTC",
            IsEnabled = true,
            NextRunUtc = Now.AddMinutes(-1),
            MaxRetryCount = maxRetryCount,
            RetryBaseDelaySeconds = 0,
            CreatedAtUtc = Now.AddDays(-1),
            UpdatedAtUtc = Now.AddDays(-1),
            ETag = "etag-1"
        };

    private JobDispatcher CreateSut(IScheduledJobHandler handler)
        => new(
            _jobRepository,
            _runRepository,
            new JobHandlerRegistry([handler]),
            Options.Create(new JobSchedulerOptions
            {
                TableStorage = new JobSchedulerTableStorageOptions { ConnectionString = "UseDevelopmentStorage=true" }
            }),
            _timeProvider,
            NullLogger<JobDispatcher>.Instance,
            _emailNotificationService);

    private void SetDue(ScheduledJobRecord job)
        => _jobRepository.GetDueAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns([job]);

    [Fact]
    public async Task RetriesUntilTheHandlerSucceeds()
    {
        var handler = new SequenceHandler(
            () => JobExecutionResult.Fail("transient"),
            () => JobExecutionResult.Ok("recovered"));
        SetDue(CreateJob(maxRetryCount: 2));

        await CreateSut(handler).RunDueJobsAsync();

        handler.CallCount.ShouldBe(2);
    }

    [Fact]
    public async Task RecordsASingleSucceededRun_GivenASuccessfulRetry()
    {
        var handler = new SequenceHandler(
            () => JobExecutionResult.Fail("transient"),
            () => JobExecutionResult.Ok("recovered"));
        SetDue(CreateJob(maxRetryCount: 2));

        await CreateSut(handler).RunDueJobsAsync();

        var run = _recordedRuns.ShouldHaveSingleItem();
        run.Status.ShouldBe(JobRunStatus.Succeeded);
        run.AttemptCount.ShouldBe(2);
        run.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public async Task StopsAfterMaxRetryCountRetries()
    {
        var handler = new SequenceHandler(() => JobExecutionResult.Fail("always"));
        SetDue(CreateJob(maxRetryCount: 2));

        await CreateSut(handler).RunDueJobsAsync();

        handler.CallCount.ShouldBe(3);
        _recordedRuns.ShouldHaveSingleItem().AttemptCount.ShouldBe(3);
    }

    [Fact]
    public async Task DoesNotRetry_GivenMaxRetryCountIsZero()
    {
        var handler = new SequenceHandler(() => JobExecutionResult.Fail("always"));
        SetDue(CreateJob(maxRetryCount: 0));

        await CreateSut(handler).RunDueJobsAsync();

        handler.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task RetriesWhenTheHandlerThrows_NotOnlyWhenItReturnsFailure()
    {
        var handler = new SequenceHandler(
            () => throw new InvalidOperationException("boom"),
            () => JobExecutionResult.Ok());
        SetDue(CreateJob(maxRetryCount: 1));

        await CreateSut(handler).RunDueJobsAsync();

        handler.CallCount.ShouldBe(2);
        _recordedRuns.ShouldHaveSingleItem().Status.ShouldBe(JobRunStatus.Succeeded);
    }

    [Fact]
    public async Task SendsOneNotification_GivenEveryAttemptFails()
    {
        var handler = new SequenceHandler(() => JobExecutionResult.Fail("always"));
        SetDue(CreateJob(maxRetryCount: 2));

        await CreateSut(handler).RunDueJobsAsync();

        await _emailNotificationService.Received(1).SendPostFailureNotificationAsync(
            "job:daily-tip",
            Arg.Is<string>(details => details!.Contains("always")),
            Arg.Any<IDictionary<string, string>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DoesNotNotify_GivenARetrySucceeds()
    {
        var handler = new SequenceHandler(
            () => JobExecutionResult.Fail("transient"),
            () => JobExecutionResult.Ok());
        SetDue(CreateJob(maxRetryCount: 2));

        await CreateSut(handler).RunDueJobsAsync();

        await _emailNotificationService.DidNotReceiveWithAnyArgs().SendPostFailureNotificationAsync(
            default!, default!, default, default);
    }

    [Fact]
    public async Task StillRecordsTheFailedRun_GivenNotificationThrows()
    {
        _emailNotificationService.SendPostFailureNotificationAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IDictionary<string, string>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("smtp down")));
        var handler = new SequenceHandler(() => JobExecutionResult.Fail("always"));
        SetDue(CreateJob(maxRetryCount: 0));

        await CreateSut(handler).RunDueJobsAsync();

        _recordedRuns.ShouldHaveSingleItem().Status.ShouldBe(JobRunStatus.Failed);
    }

    [Fact]
    public async Task RecordsAnAbortedRun_GivenExecutionIsCancelled()
    {
        using var cts = new CancellationTokenSource();
        var handler = new CancellingHandler(cts);
        var job = CreateJob(maxRetryCount: 2);
        SetDue(job);

        await CreateSut(handler).RunDueJobsAsync(cts.Token);

        handler.CallCount.ShouldBe(1);
        _recordedRuns.ShouldHaveSingleItem().Status.ShouldBe(JobRunStatus.Aborted);
        job.RunState.ShouldBe(JobRunState.Idle);
    }

    [Fact]
    public async Task LeavesNextRunInThePast_GivenExecutionIsCancelled()
    {
        using var cts = new CancellationTokenSource();
        var job = CreateJob(maxRetryCount: 0);
        var originalNextRun = job.NextRunUtc;
        SetDue(job);

        await CreateSut(new CancellingHandler(cts)).RunDueJobsAsync(cts.Token);

        job.NextRunUtc.ShouldBe(originalNextRun);
    }

    [Fact]
    public async Task DoesNotNotify_GivenExecutionIsCancelled()
    {
        using var cts = new CancellationTokenSource();
        SetDue(CreateJob(maxRetryCount: 0));

        await CreateSut(new CancellingHandler(cts)).RunDueJobsAsync(cts.Token);

        await _emailNotificationService.DidNotReceiveWithAnyArgs().SendPostFailureNotificationAsync(
            default!, default!, default, default);
    }

    [Fact]
    public async Task RetriesAndRecordsFailed_GivenTheHandlerTimesOutWithoutShutdownBeingRequested()
    {
        var handler = new TimingOutHandler();
        SetDue(CreateJob(maxRetryCount: 2));

        await CreateSut(handler).RunDueJobsAsync();

        handler.CallCount.ShouldBe(3);
        var run = _recordedRuns.ShouldHaveSingleItem();
        run.Status.ShouldBe(JobRunStatus.Failed);
        run.AttemptCount.ShouldBe(3);
    }

    [Fact]
    public async Task RecordsAnAbortedRunAndReleasesTheClaim_GivenCancellationDuringTheRetryBackoff()
    {
        using var cts = new CancellationTokenSource();
        var handler = new CancelDuringBackoffHandler(cts);
        var job = CreateJob(maxRetryCount: 1);
        job.RetryBaseDelaySeconds = 1;
        SetDue(job);

        await CreateSut(handler).RunDueJobsAsync(cts.Token);

        handler.CallCount.ShouldBe(1);
        var run = _recordedRuns.ShouldHaveSingleItem();
        run.Status.ShouldBe(JobRunStatus.Aborted);
        job.RunState.ShouldBe(JobRunState.Idle);
        job.ClaimedAtUtc.ShouldBeNull();
    }
}

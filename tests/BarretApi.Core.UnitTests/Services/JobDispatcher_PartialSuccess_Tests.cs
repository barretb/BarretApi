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

/// <summary>
/// Covers <see cref="JobExecutionResult.PartialSuccess"/> end to end through the dispatcher:
/// a multi-platform publish where some platforms failed must stay <c>Succeeded</c> (so it is
/// never retried and re-posts nothing), but the failure must still surface in
/// <c>ErrorMessage</c>/<c>LastRunError</c> and keep <c>ConsecutiveFailureCount</c> climbing —
/// otherwise a job that is half-broken every day looks perfectly healthy in <c>GET /api/jobs</c>.
/// </summary>
public sealed class JobDispatcher_PartialSuccess_Tests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

    private readonly IScheduledJobRepository _jobRepository = Substitute.For<IScheduledJobRepository>();
    private readonly IJobRunRepository _runRepository = Substitute.For<IJobRunRepository>();
    private readonly IEmailNotificationService _emailNotificationService =
        Substitute.For<IEmailNotificationService>();
    private readonly FakeTimeProvider _timeProvider = new(Now);
    private readonly List<JobRunRecord> _recordedRuns = [];

    public JobDispatcher_PartialSuccess_Tests()
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

    private sealed class StubHandler(Func<JobExecutionResult> behavior) : IScheduledJobHandler
    {
        public string JobType => "tip-of-day";
        public int CallCount { get; private set; }

        public Task<JobExecutionResult> ExecuteAsync(
            JobExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(behavior());
        }
    }

    private static ScheduledJobRecord CreateJob(int maxRetryCount = 2, int consecutiveFailureCount = 0)
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
            ConsecutiveFailureCount = consecutiveFailureCount,
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
    public async Task RecordsSucceededWithTheFailedPlatformNamed_GivenAPartialSuccess()
    {
        var handler = new StubHandler(() => JobExecutionResult.PartialSuccess(
            "Posted to bluesky. Failed on mastodon.", "mastodon: token expired"));
        SetDue(CreateJob());

        await CreateSut(handler).RunDueJobsAsync();

        var run = _recordedRuns.ShouldHaveSingleItem();
        run.Status.ShouldBe(JobRunStatus.Succeeded);
        run.ErrorMessage.ShouldNotBeNull();
        run.ErrorMessage.ShouldContain("mastodon");
    }

    [Fact]
    public async Task DoesNotRetry_GivenAPartialSuccess()
    {
        var handler = new StubHandler(() => JobExecutionResult.PartialSuccess(
            "Posted to bluesky. Failed on mastodon.", "mastodon: token expired"));
        SetDue(CreateJob(maxRetryCount: 2));

        await CreateSut(handler).RunDueJobsAsync();

        handler.CallCount.ShouldBe(1);
        _recordedRuns.ShouldHaveSingleItem().AttemptCount.ShouldBe(1);
    }

    [Fact]
    public async Task DoesNotNotify_GivenAPartialSuccess()
    {
        var handler = new StubHandler(() => JobExecutionResult.PartialSuccess(
            "Posted to bluesky. Failed on mastodon.", "mastodon: token expired"));
        SetDue(CreateJob());

        await CreateSut(handler).RunDueJobsAsync();

        await _emailNotificationService.DidNotReceiveWithAnyArgs().SendPostFailureNotificationAsync(
            default!, default!, default, default);
    }

    [Fact]
    public async Task DoesNotResetConsecutiveFailureCount_GivenAPartialSuccess()
    {
        var handler = new StubHandler(() => JobExecutionResult.PartialSuccess(
            "Posted to bluesky. Failed on mastodon.", "mastodon: token expired"));
        var job = CreateJob(consecutiveFailureCount: 2);
        SetDue(job);

        await CreateSut(handler).RunDueJobsAsync();

        job.LastRunStatus.ShouldBe(JobRunStatus.Succeeded);
        job.LastRunError.ShouldNotBeNull();
        job.ConsecutiveFailureCount.ShouldBe(3);
    }

    [Fact]
    public async Task ClimbsFurther_GivenRepeatedPartialSuccesses()
    {
        var handler = new StubHandler(() => JobExecutionResult.PartialSuccess(
            "Posted to bluesky. Failed on mastodon.", "mastodon: token expired"));
        var job = CreateJob();
        SetDue(job);
        var sut = CreateSut(handler);

        await sut.RunDueJobsAsync();
        job.NextRunUtc = Now.AddMinutes(-1);
        job.RunState = JobRunState.Idle;
        await sut.RunDueJobsAsync();

        job.ConsecutiveFailureCount.ShouldBe(2);
    }

    [Fact]
    public async Task StillResetsConsecutiveFailureCountAndClearsLastRunError_GivenAFullSuccessAfterAPartialOne()
    {
        var partial = JobExecutionResult.PartialSuccess(
            "Posted to bluesky. Failed on mastodon.", "mastodon: token expired");
        var calls = 0;
        var handler = new StubHandler(() => calls++ == 0 ? partial : JobExecutionResult.Ok("posted"));
        var job = CreateJob();
        SetDue(job);
        var sut = CreateSut(handler);

        await sut.RunDueJobsAsync();
        job.NextRunUtc = Now.AddMinutes(-1);
        job.RunState = JobRunState.Idle;
        await sut.RunDueJobsAsync();

        job.ConsecutiveFailureCount.ShouldBe(0);
        job.LastRunError.ShouldBeNull();
        job.LastRunStatus.ShouldBe(JobRunStatus.Succeeded);
    }
}

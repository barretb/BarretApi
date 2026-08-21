using BarretApi.Api.Features.Jobs;
using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Core.Services;
using FastEndpoints;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;

namespace BarretApi.Api.UnitTests.Features.Jobs;

public sealed class RunJobEndpoint_HandleAsync_Tests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

    private readonly IScheduledJobRepository _jobRepository = Substitute.For<IScheduledJobRepository>();
    private readonly IJobRunRepository _runRepository = Substitute.For<IJobRunRepository>();
    private readonly FakeTimeProvider _timeProvider = new(Now);

    private sealed class StubHandler(bool succeeds) : IScheduledJobHandler
    {
        public string JobType => "tip-of-day";

        public Task<JobExecutionResult> ExecuteAsync(
            JobExecutionContext context,
            CancellationToken cancellationToken = default)
            => Task.FromResult(succeeds
                ? JobExecutionResult.Ok("posted")
                : JobExecutionResult.Fail("platform rejected the post"));
    }

    private JobDispatcher CreateDispatcher(bool handlerSucceeds = true)
        => new(
            _jobRepository,
            _runRepository,
            new JobHandlerRegistry([new StubHandler(handlerSucceeds)]),
            Options.Create(new JobSchedulerOptions
            {
                TableStorage = new JobSchedulerTableStorageOptions { ConnectionString = "UseDevelopmentStorage=true" }
            }),
            _timeProvider,
            NullLogger<JobDispatcher>.Instance);

    private RunJobEndpoint CreateEndpoint(bool handlerSucceeds = true)
        => Factory.Create<RunJobEndpoint>(
            CreateDispatcher(handlerSucceeds),
            NullLogger<RunJobEndpoint>.Instance);

    private static ScheduledJobRecord CreateJob()
        => new()
        {
            Name = "daily-tip",
            DisplayName = "Daily tip",
            JobType = "tip-of-day",
            CronExpression = "0 8 * * *",
            TimeZoneId = "UTC",
            IsEnabled = true,
            NextRunUtc = Now.AddHours(20),
            MaxRetryCount = 0,
            RetryBaseDelaySeconds = 0,
            CreatedAtUtc = Now,
            UpdatedAtUtc = Now,
            ETag = "etag-1"
        };

    private void AllowClaim()
        => _jobRepository.TryClaimAsync(Arg.Any<ScheduledJobRecord>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var job = callInfo.Arg<ScheduledJobRecord>()!;
                job.RunState = JobRunState.Running;
                job.ClaimedAtUtc = callInfo.ArgAt<DateTimeOffset>(1);
                return true;
            });

    [Fact]
    public async Task Returns404_GivenNoSuchJob()
    {
        _jobRepository.GetByNameAsync("missing", Arg.Any<CancellationToken>())
            .Returns((ScheduledJobRecord?)null);
        var ep = CreateEndpoint();

        await ep.HandleAsync(new GetJobRequest { Name = "missing" }, default);

        ep.HttpContext.Response.StatusCode.ShouldBe(404);
    }

    [Fact]
    public async Task Returns409_GivenTheJobIsAlreadyRunning()
    {
        var job = CreateJob();
        job.RunState = JobRunState.Running;
        job.ClaimedAtUtc = Now.AddMinutes(-1);
        _jobRepository.GetByNameAsync("daily-tip", Arg.Any<CancellationToken>()).Returns(job);
        var ep = CreateEndpoint();

        await ep.HandleAsync(new GetJobRequest { Name = "daily-tip" }, default);

        ep.HttpContext.Response.StatusCode.ShouldBe(409);
    }

    [Fact]
    public async Task Returns200WithTheRun_GivenTheRunSucceeded()
    {
        _jobRepository.GetByNameAsync("daily-tip", Arg.Any<CancellationToken>()).Returns(CreateJob());
        AllowClaim();
        var ep = CreateEndpoint();

        await ep.HandleAsync(new GetJobRequest { Name = "daily-tip" }, default);

        ep.Response.Status.ShouldBe("Succeeded");
        ep.Response.TriggerType.ShouldBe("Manual");
        ep.Response.JobName.ShouldBe("daily-tip");
    }

    [Fact]
    public async Task Returns502_GivenTheRunFailed()
    {
        _jobRepository.GetByNameAsync("daily-tip", Arg.Any<CancellationToken>()).Returns(CreateJob());
        AllowClaim();
        var ep = CreateEndpoint(handlerSucceeds: false);

        await ep.HandleAsync(new GetJobRequest { Name = "daily-tip" }, default);

        ep.HttpContext.Response.StatusCode.ShouldBe(502);
    }

    [Fact]
    public async Task LeavesTheScheduleAlone()
    {
        var job = CreateJob();
        var originalNextRun = job.NextRunUtc;
        _jobRepository.GetByNameAsync("daily-tip", Arg.Any<CancellationToken>()).Returns(job);
        AllowClaim();
        var ep = CreateEndpoint();

        await ep.HandleAsync(new GetJobRequest { Name = "daily-tip" }, default);

        job.NextRunUtc.ShouldBe(originalNextRun);
    }
}

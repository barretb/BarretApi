using BarretApi.Api.Features.Jobs;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using FastEndpoints;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace BarretApi.Api.UnitTests.Features.Jobs;

public sealed class ListJobRunsEndpoint_HandleAsync_Tests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

    private readonly IScheduledJobRepository _jobRepository = Substitute.For<IScheduledJobRepository>();
    private readonly IJobRunRepository _runRepository = Substitute.For<IJobRunRepository>();

    private ListJobRunsEndpoint CreateEndpoint()
        => Factory.Create<ListJobRunsEndpoint>(
            _jobRepository,
            _runRepository,
            NullLogger<ListJobRunsEndpoint>.Instance);

    private static ScheduledJobRecord CreateJob()
        => new()
        {
            Name = "daily-tip",
            DisplayName = "Daily tip",
            JobType = "tip-of-day",
            CronExpression = "0 8 * * *",
            CreatedAtUtc = Now,
            UpdatedAtUtc = Now
        };

    private static JobRunRecord CreateRun(string runId = "run-1")
        => new()
        {
            RunId = runId,
            JobName = "daily-tip",
            JobType = "tip-of-day",
            TriggerType = JobTriggerType.Scheduled,
            ScheduledForUtc = Now.AddMinutes(-1),
            StartedAtUtc = Now,
            CompletedAtUtc = Now.AddSeconds(2),
            DurationMs = 2_000,
            Status = JobRunStatus.Succeeded,
            AttemptCount = 1,
            Summary = "posted"
        };

    [Fact]
    public async Task Returns404_GivenNoSuchJob()
    {
        _jobRepository.GetByNameAsync("missing", Arg.Any<CancellationToken>())
            .Returns((ScheduledJobRecord?)null);
        var ep = CreateEndpoint();

        await ep.HandleAsync(new ListJobRunsRequest { Name = "missing" }, default);

        ep.HttpContext.Response.StatusCode.ShouldBe(404);
    }

    [Fact]
    public async Task ReturnsTheRunsForTheJob()
    {
        _jobRepository.GetByNameAsync("daily-tip", Arg.Any<CancellationToken>()).Returns(CreateJob());
        _runRepository.GetByJobAsync("daily-tip", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([CreateRun("run-2"), CreateRun("run-1")]);
        var ep = CreateEndpoint();

        await ep.HandleAsync(new ListJobRunsRequest { Name = "daily-tip" }, default);

        ep.Response.Runs.Count.ShouldBe(2);
        ep.Response.Runs[0].RunId.ShouldBe("run-2");
        ep.Response.Runs[0].Status.ShouldBe("Succeeded");
        ep.Response.Runs[0].TriggerType.ShouldBe("Scheduled");
        ep.Response.Runs[0].DurationMs.ShouldBe(2_000);
    }

    [Fact]
    public async Task DefaultsToFiftyRuns()
    {
        _jobRepository.GetByNameAsync("daily-tip", Arg.Any<CancellationToken>()).Returns(CreateJob());
        _runRepository.GetByJobAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);
        var ep = CreateEndpoint();

        await ep.HandleAsync(new ListJobRunsRequest { Name = "daily-tip" }, default);

        await _runRepository.Received(1).GetByJobAsync("daily-tip", 50, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClampsMaxCountToTheAllowedRange()
    {
        _jobRepository.GetByNameAsync("daily-tip", Arg.Any<CancellationToken>()).Returns(CreateJob());
        _runRepository.GetByJobAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);
        var ep = CreateEndpoint();

        await ep.HandleAsync(new ListJobRunsRequest { Name = "daily-tip", MaxCount = 5_000 }, default);

        await _runRepository.Received(1).GetByJobAsync("daily-tip", 500, Arg.Any<CancellationToken>());
    }
}

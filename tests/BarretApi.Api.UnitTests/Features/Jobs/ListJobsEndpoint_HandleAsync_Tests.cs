using BarretApi.Api.Features.Jobs;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using FastEndpoints;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace BarretApi.Api.UnitTests.Features.Jobs;

public sealed class ListJobsEndpoint_HandleAsync_Tests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

    private readonly IScheduledJobRepository _jobRepository = Substitute.For<IScheduledJobRepository>();

    private static ScheduledJobRecord CreateJob(string name = "daily-tip")
        => new()
        {
            Name = name,
            DisplayName = "Daily tip",
            JobType = "tip-of-day",
            CronExpression = "0 8 * * *",
            TimeZoneId = "America/Chicago",
            ArgumentsJson = """{"category":"dotnet"}""",
            IsEnabled = true,
            NextRunUtc = Now.AddHours(1),
            LastRunUtc = Now.AddDays(-1),
            LastRunStatus = JobRunStatus.Succeeded,
            LastRunDurationMs = 1_234,
            ConsecutiveFailureCount = 0,
            MaxRetryCount = 2,
            RetryBaseDelaySeconds = 30,
            RunState = JobRunState.Idle,
            CreatedAtUtc = Now.AddDays(-30),
            UpdatedAtUtc = Now.AddDays(-1)
        };

    [Fact]
    public async Task ReturnsEveryJob()
    {
        _jobRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns([CreateJob("daily-tip"), CreateJob("daily-apod")]);

        var ep = Factory.Create<ListJobsEndpoint>(
            _jobRepository,
            NullLogger<ListJobsEndpoint>.Instance);

        await ep.HandleAsync(default);

        ep.Response.Jobs.Count.ShouldBe(2);
    }

    [Fact]
    public async Task MapsEveryFieldOntoTheResponse()
    {
        _jobRepository.GetAllAsync(Arg.Any<CancellationToken>()).Returns([CreateJob()]);

        var ep = Factory.Create<ListJobsEndpoint>(
            _jobRepository,
            NullLogger<ListJobsEndpoint>.Instance);

        await ep.HandleAsync(default);

        var job = ep.Response.Jobs[0];
        job.Name.ShouldBe("daily-tip");
        job.DisplayName.ShouldBe("Daily tip");
        job.JobType.ShouldBe("tip-of-day");
        job.CronExpression.ShouldBe("0 8 * * *");
        job.TimeZoneId.ShouldBe("America/Chicago");
        job.ArgumentsJson.ShouldBe("""{"category":"dotnet"}""");
        job.IsEnabled.ShouldBeTrue();
        job.NextRunUtc.ShouldBe(Now.AddHours(1));
        job.LastRunStatus.ShouldBe("Succeeded");
        job.LastRunDurationMs.ShouldBe(1_234);
        job.MaxRetryCount.ShouldBe(2);
        job.RetryBaseDelaySeconds.ShouldBe(30);
        job.IsRunning.ShouldBeFalse();
    }

    [Fact]
    public async Task ReportsAJobAsRunning_GivenItIsClaimed()
    {
        var job = CreateJob();
        job.RunState = JobRunState.Running;
        _jobRepository.GetAllAsync(Arg.Any<CancellationToken>()).Returns([job]);

        var ep = Factory.Create<ListJobsEndpoint>(
            _jobRepository,
            NullLogger<ListJobsEndpoint>.Instance);

        await ep.HandleAsync(default);

        ep.Response.Jobs[0].IsRunning.ShouldBeTrue();
    }

    [Fact]
    public async Task ReturnsAnEmptyList_GivenNoJobsExist()
    {
        _jobRepository.GetAllAsync(Arg.Any<CancellationToken>()).Returns([]);

        var ep = Factory.Create<ListJobsEndpoint>(
            _jobRepository,
            NullLogger<ListJobsEndpoint>.Instance);

        await ep.HandleAsync(default);

        ep.Response.Jobs.ShouldBeEmpty();
    }
}

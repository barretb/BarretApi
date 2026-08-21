using BarretApi.Api.Features.Jobs;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Core.Services;
using FastEndpoints;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;

namespace BarretApi.Api.UnitTests.Features.Jobs;

public sealed class UpdateJobEndpoint_HandleAsync_Tests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

    private readonly IScheduledJobRepository _jobRepository = Substitute.For<IScheduledJobRepository>();
    private readonly FakeTimeProvider _timeProvider = new(Now);
    private readonly JobHandlerRegistry _registry = new([new StubHandler()]);

    private sealed class StubHandler : IScheduledJobHandler
    {
        public string JobType => "tip-of-day";

        public Task<JobExecutionResult> ExecuteAsync(
            JobExecutionContext context,
            CancellationToken cancellationToken = default)
            => Task.FromResult(JobExecutionResult.Ok());
    }

    private UpdateJobEndpoint CreateEndpoint(string routeName = "daily-tip")
        => Factory.Create<UpdateJobEndpoint>(
            ctx => ctx.Request.RouteValues["Name"] = routeName,
            _jobRepository,
            _registry,
            _timeProvider,
            NullLogger<UpdateJobEndpoint>.Instance);

    private static ScheduledJobRecord CreateExisting(bool isEnabled = true)
        => new()
        {
            Name = "daily-tip",
            DisplayName = "Daily tip",
            JobType = "tip-of-day",
            CronExpression = "0 8 * * *",
            TimeZoneId = "UTC",
            IsEnabled = isEnabled,
            NextRunUtc = isEnabled ? Now.AddHours(20) : null,
            MaxRetryCount = 2,
            RetryBaseDelaySeconds = 30,
            CreatedAtUtc = Now.AddDays(-10),
            UpdatedAtUtc = Now.AddDays(-1)
        };

    private static SaveJobRequest CreateRequest(
        string cron = "0 8 * * *",
        bool isEnabled = true)
        => new()
        {
            Name = "daily-tip",
            DisplayName = "Daily tip",
            JobType = "tip-of-day",
            CronExpression = cron,
            TimeZoneId = "UTC",
            IsEnabled = isEnabled,
            MaxRetryCount = 2,
            RetryBaseDelaySeconds = 30
        };

    [Fact]
    public async Task Returns404_GivenNoSuchJob()
    {
        _jobRepository.GetByNameAsync("daily-tip", Arg.Any<CancellationToken>())
            .Returns((ScheduledJobRecord?)null);
        var ep = CreateEndpoint();

        await ep.HandleAsync(CreateRequest(), default);

        ep.HttpContext.Response.StatusCode.ShouldBe(404);
        await _jobRepository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
    }

    [Fact]
    public async Task PersistsTheEditedFields()
    {
        var existing = CreateExisting();
        _jobRepository.GetByNameAsync("daily-tip", Arg.Any<CancellationToken>()).Returns(existing);
        var request = CreateRequest();
        request.ArgumentsJson = """{"category":"azure"}""";
        request.MaxRetryCount = 5;
        var ep = CreateEndpoint();

        await ep.HandleAsync(request, default);

        existing.ArgumentsJson.ShouldBe("""{"category":"azure"}""");
        existing.MaxRetryCount.ShouldBe(5);
        existing.UpdatedAtUtc.ShouldBe(Now);
        await _jobRepository.Received(1).UpdateAsync(existing, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LeavesNextRunAlone_GivenNeitherTheScheduleNorTheEnabledFlagChanged()
    {
        var existing = CreateExisting();
        var originalNextRun = existing.NextRunUtc;
        _jobRepository.GetByNameAsync("daily-tip", Arg.Any<CancellationToken>()).Returns(existing);
        var ep = CreateEndpoint();

        await ep.HandleAsync(CreateRequest(), default);

        existing.NextRunUtc.ShouldBe(originalNextRun);
    }

    [Fact]
    public async Task RecomputesNextRunFromNow_GivenTheCronExpressionChanged()
    {
        var existing = CreateExisting();
        _jobRepository.GetByNameAsync("daily-tip", Arg.Any<CancellationToken>()).Returns(existing);
        var ep = CreateEndpoint();

        await ep.HandleAsync(CreateRequest(cron: "0 15 * * *"), default);

        existing.NextRunUtc.ShouldBe(new DateTimeOffset(2026, 8, 20, 15, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task RecomputesNextRunFromNow_GivenAPausedJobIsEnabled()
    {
        var existing = CreateExisting(isEnabled: false);
        _jobRepository.GetByNameAsync("daily-tip", Arg.Any<CancellationToken>()).Returns(existing);
        var ep = CreateEndpoint();

        await ep.HandleAsync(CreateRequest(isEnabled: true), default);

        existing.NextRunUtc.ShouldBe(new DateTimeOffset(2026, 8, 21, 8, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task ClearsNextRun_GivenTheJobIsDisabled()
    {
        var existing = CreateExisting();
        _jobRepository.GetByNameAsync("daily-tip", Arg.Any<CancellationToken>()).Returns(existing);
        var ep = CreateEndpoint();

        await ep.HandleAsync(CreateRequest(isEnabled: false), default);

        existing.NextRunUtc.ShouldBeNull();
    }

    [Fact]
    public async Task Returns400WithACronExpressionError_GivenAnUnparseableCronExpression()
    {
        _jobRepository.GetByNameAsync("daily-tip", Arg.Any<CancellationToken>()).Returns(CreateExisting());
        var ep = CreateEndpoint();

        await ep.HandleAsync(CreateRequest(cron: "not a cron"), default);

        ep.ValidationFailures.ShouldContain(e => e.PropertyName == "CronExpression");
        ep.ValidationFailures.ShouldNotContain(e => e.PropertyName == "TimeZoneId");
        await _jobRepository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
    }

    [Fact]
    public async Task Returns400WithATimeZoneIdError_GivenAnUnknownTimeZone()
    {
        _jobRepository.GetByNameAsync("daily-tip", Arg.Any<CancellationToken>()).Returns(CreateExisting());
        var ep = CreateEndpoint();
        var request = CreateRequest();
        request.TimeZoneId = "Mars/Olympus_Mons";

        await ep.HandleAsync(request, default);

        ep.ValidationFailures.ShouldContain(e => e.PropertyName == "TimeZoneId");
        ep.ValidationFailures.ShouldNotContain(e => e.PropertyName == "CronExpression");
        await _jobRepository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
    }

    [Fact]
    public async Task UsesTheRouteNameNotTheBodyName_GivenTheyDiffer()
    {
        var existing = CreateExisting();
        _jobRepository.GetByNameAsync("daily-tip", Arg.Any<CancellationToken>()).Returns(existing);
        var request = CreateRequest();
        request.Name = "some-other-job";
        var ep = CreateEndpoint(routeName: "daily-tip");

        await ep.HandleAsync(request, default);

        await _jobRepository.Received(1).GetByNameAsync("daily-tip", Arg.Any<CancellationToken>());
        await _jobRepository.DidNotReceive().GetByNameAsync("some-other-job", Arg.Any<CancellationToken>());
        await _jobRepository.Received(1).UpdateAsync(existing, Arg.Any<CancellationToken>());
    }
}

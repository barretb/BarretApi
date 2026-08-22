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

public sealed class CreateJobEndpoint_HandleAsync_Tests
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

    private CreateJobEndpoint CreateEndpoint()
        => Factory.Create<CreateJobEndpoint>(
            _jobRepository,
            _registry,
            _timeProvider,
            NullLogger<CreateJobEndpoint>.Instance);

    private static SaveJobRequest CreateRequest()
        => new()
        {
            Name = "daily-tip",
            DisplayName = "Daily tip",
            JobType = "tip-of-day",
            CronExpression = "0 8 * * *",
            TimeZoneId = "UTC",
            IsEnabled = true,
            MaxRetryCount = 2,
            RetryBaseDelaySeconds = 30
        };

    [Fact]
    public async Task PersistsTheJob_GivenAValidRequest()
    {
        var ep = CreateEndpoint();

        await ep.HandleAsync(CreateRequest(), default);

        await _jobRepository.Received(1).CreateAsync(
            Arg.Is<ScheduledJobRecord>(j => j!.Name == "daily-tip" && j.JobType == "tip-of-day"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ComputesTheNextRunFromNow()
    {
        ScheduledJobRecord? created = null;
        await _jobRepository.CreateAsync(Arg.Do<ScheduledJobRecord>(j => created = j), Arg.Any<CancellationToken>());
        var ep = CreateEndpoint();

        await ep.HandleAsync(CreateRequest(), default);

        created!.NextRunUtc.ShouldBe(new DateTimeOffset(2026, 8, 21, 8, 0, 0, TimeSpan.Zero));
        ep.Response.NextRunUtc.ShouldBe(created.NextRunUtc);
    }

    [Fact]
    public async Task LeavesNextRunNull_GivenTheJobIsCreatedDisabled()
    {
        ScheduledJobRecord? created = null;
        await _jobRepository.CreateAsync(Arg.Do<ScheduledJobRecord>(j => created = j), Arg.Any<CancellationToken>());
        var request = CreateRequest();
        request.IsEnabled = false;
        var ep = CreateEndpoint();

        await ep.HandleAsync(request, default);

        created!.NextRunUtc.ShouldBeNull();
    }

    [Fact]
    public async Task DefaultsTheDisplayNameToTheName()
    {
        ScheduledJobRecord? created = null;
        await _jobRepository.CreateAsync(Arg.Do<ScheduledJobRecord>(j => created = j), Arg.Any<CancellationToken>());
        var request = CreateRequest();
        request.DisplayName = null;
        var ep = CreateEndpoint();

        await ep.HandleAsync(request, default);

        created!.DisplayName.ShouldBe("daily-tip");
    }

    [Fact]
    public async Task Returns400AndPersistsNothing_GivenAnUnregisteredJobType()
    {
        var request = CreateRequest();
        request.JobType = "gone-missing";
        var ep = CreateEndpoint();

        await ep.HandleAsync(request, default);

        ep.ValidationFailures.ShouldNotBeEmpty();
        await _jobRepository.DidNotReceiveWithAnyArgs().CreateAsync(default!, default);
    }

    [Fact]
    public async Task Returns400WithACronExpressionError_GivenAnUnparseableCronExpression()
    {
        var request = CreateRequest();
        request.CronExpression = "not a cron";
        var ep = CreateEndpoint();

        await ep.HandleAsync(request, default);

        ep.ValidationFailures.ShouldContain(e => e.PropertyName == "CronExpression");
        ep.ValidationFailures.ShouldNotContain(e => e.PropertyName == "TimeZoneId");
        await _jobRepository.DidNotReceiveWithAnyArgs().CreateAsync(default!, default);
    }

    [Fact]
    public async Task Returns400WithATimeZoneIdError_GivenAnUnknownTimeZone()
    {
        var request = CreateRequest();
        request.TimeZoneId = "Mars/Olympus_Mons";
        var ep = CreateEndpoint();

        await ep.HandleAsync(request, default);

        ep.ValidationFailures.ShouldContain(e => e.PropertyName == "TimeZoneId");
        ep.ValidationFailures.ShouldNotContain(e => e.PropertyName == "CronExpression");
        await _jobRepository.DidNotReceiveWithAnyArgs().CreateAsync(default!, default);
    }

    [Fact]
    public async Task Returns409_GivenAJobWithThatNameAlreadyExists()
    {
        _jobRepository.GetByNameAsync("daily-tip", Arg.Any<CancellationToken>())
            .Returns(new ScheduledJobRecord
            {
                Name = "daily-tip",
                DisplayName = "Daily tip",
                JobType = "tip-of-day",
                CronExpression = "0 8 * * *"
            });
        var ep = CreateEndpoint();

        await ep.HandleAsync(CreateRequest(), default);

        ep.HttpContext.Response.StatusCode.ShouldBe(409);
        await _jobRepository.DidNotReceiveWithAnyArgs().CreateAsync(default!, default);
    }
}

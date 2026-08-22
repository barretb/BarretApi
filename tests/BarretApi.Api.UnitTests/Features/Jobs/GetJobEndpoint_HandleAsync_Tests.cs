using BarretApi.Api.Features.Jobs;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using FastEndpoints;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace BarretApi.Api.UnitTests.Features.Jobs;

public sealed class GetJobEndpoint_HandleAsync_Tests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

    private readonly IScheduledJobRepository _jobRepository = Substitute.For<IScheduledJobRepository>();

    private static ScheduledJobRecord CreateJob()
        => new()
        {
            Name = "daily-tip",
            DisplayName = "Daily tip",
            JobType = "tip-of-day",
            CronExpression = "0 8 * * *",
            TimeZoneId = "UTC",
            IsEnabled = true,
            NextRunUtc = Now.AddHours(1),
            CreatedAtUtc = Now,
            UpdatedAtUtc = Now
        };

    [Fact]
    public async Task ReturnsTheJob_GivenItExists()
    {
        _jobRepository.GetByNameAsync("daily-tip", Arg.Any<CancellationToken>()).Returns(CreateJob());

        var ep = Factory.Create<GetJobEndpoint>(
            _jobRepository,
            NullLogger<GetJobEndpoint>.Instance);

        await ep.HandleAsync(new GetJobRequest { Name = "daily-tip" }, default);

        ep.Response.Name.ShouldBe("daily-tip");
    }

    [Fact]
    public async Task Returns404_GivenNoSuchJob()
    {
        _jobRepository.GetByNameAsync("missing", Arg.Any<CancellationToken>())
            .Returns((ScheduledJobRecord?)null);

        var ep = Factory.Create<GetJobEndpoint>(
            _jobRepository,
            NullLogger<GetJobEndpoint>.Instance);

        await ep.HandleAsync(new GetJobRequest { Name = "missing" }, default);

        ep.HttpContext.Response.StatusCode.ShouldBe(404);
    }
}

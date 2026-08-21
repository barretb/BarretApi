using BarretApi.Api.Features.Jobs;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Core.Services;
using FastEndpoints;
using Shouldly;

namespace BarretApi.Api.UnitTests.Features.Jobs;

public sealed class ListJobTypesEndpoint_HandleAsync_Tests
{
    private sealed class StubHandler(string jobType) : IScheduledJobHandler
    {
        public string JobType { get; } = jobType;

        public Task<JobExecutionResult> ExecuteAsync(
            JobExecutionContext context,
            CancellationToken cancellationToken = default)
            => Task.FromResult(JobExecutionResult.Ok());
    }

    [Fact]
    public async Task ReturnsEveryRegisteredJobTypeSorted()
    {
        var registry = new JobHandlerRegistry([new StubHandler("tip-of-day"), new StubHandler("nasa-apod")]);
        var ep = Factory.Create<ListJobTypesEndpoint>(registry);

        await ep.HandleAsync(default);

        ep.Response.JobTypes.ShouldBe(["nasa-apod", "tip-of-day"]);
    }

    [Fact]
    public async Task ReturnsAnEmptyList_GivenNoHandlersAreRegistered()
    {
        var ep = Factory.Create<ListJobTypesEndpoint>(new JobHandlerRegistry([]));

        await ep.HandleAsync(default);

        ep.Response.JobTypes.ShouldBeEmpty();
    }
}

using BarretApi.Api.Scheduling;
using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace BarretApi.Api.UnitTests.Scheduling;

public sealed class JobSchedulerHostedService_Tests
{
    private readonly IScheduledJobRepository _jobRepository = Substitute.For<IScheduledJobRepository>();
    private readonly IJobRunRepository _runRepository = Substitute.For<IJobRunRepository>();

    private sealed class StubHandler : IScheduledJobHandler
    {
        public string JobType => "tip-of-day";

        public Task<JobExecutionResult> ExecuteAsync(
            JobExecutionContext context,
            CancellationToken cancellationToken = default)
            => Task.FromResult(JobExecutionResult.Ok());
    }

    private ServiceProvider BuildServiceProvider(JobSchedulerOptions options)
    {
        var services = new ServiceCollection();
        services.AddSingleton(_jobRepository);
        services.AddSingleton(_runRepository);
        services.AddSingleton<IScheduledJobHandler, StubHandler>();
        services.AddSingleton<JobHandlerRegistry>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(Options.Create(options));
        services.AddLogging();
        services.AddSingleton<JobDispatcher>();
        return services.BuildServiceProvider();
    }

    private static JobSchedulerOptions CreateOptions(bool enabled)
        => new()
        {
            Enabled = enabled,
            TableStorage = new JobSchedulerTableStorageOptions { ConnectionString = "UseDevelopmentStorage=true" }
        };

    private JobSchedulerHostedService CreateSut(bool enabled)
    {
        var provider = BuildServiceProvider(CreateOptions(enabled));
        return new JobSchedulerHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(CreateOptions(enabled)),
            NullLogger<JobSchedulerHostedService>.Instance);
    }

    [Fact]
    public async Task DoesNotQueryForDueJobs_GivenTheSchedulerIsDisabled()
    {
        await CreateSut(enabled: false).StartAsync(CancellationToken.None);

        await _jobRepository.DidNotReceiveWithAnyArgs().GetDueAsync(default, default);
    }

    [Fact]
    public async Task RunsDueJobs_WhenATickExecutes()
    {
        _jobRepository.GetDueAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns([]);

        await CreateSut(enabled: true).RunTickAsync(CancellationToken.None);

        await _jobRepository.Received(1).GetDueAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SwallowsTickFailuresSoTheLoopSurvives()
    {
        _jobRepository.GetDueAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<ScheduledJobRecord>>(_ => throw new InvalidOperationException("storage down"));

        await Should.NotThrowAsync(() => CreateSut(enabled: true).RunTickAsync(CancellationToken.None));
    }
}

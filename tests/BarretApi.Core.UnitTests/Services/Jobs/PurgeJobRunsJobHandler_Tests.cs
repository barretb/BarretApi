using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Core.Services.Jobs;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;

namespace BarretApi.Core.UnitTests.Services.Jobs;

public sealed class PurgeJobRunsJobHandler_Tests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

    private readonly IJobRunRepository _runRepository = Substitute.For<IJobRunRepository>();
    private readonly FakeTimeProvider _timeProvider = new(Now);

    private PurgeJobRunsJobHandler CreateSut(int retentionDays = 30)
        => new(
            _runRepository,
            Options.Create(new JobSchedulerOptions
            {
                RunRetentionDays = retentionDays,
                TableStorage = new JobSchedulerTableStorageOptions { ConnectionString = "UseDevelopmentStorage=true" }
            }),
            _timeProvider,
            NullLogger<PurgeJobRunsJobHandler>.Instance);

    private static JobExecutionContext CreateContext()
        => new("purge-job-runs", "purge-job-runs", "run-1", Now, null);

    [Fact]
    public void UsesTheExpectedJobType()
    {
        CreateSut().JobType.ShouldBe("purge-job-runs");
    }

    [Fact]
    public async Task PurgesUsingTheConfiguredRetentionWindow()
    {
        await CreateSut(retentionDays: 30).ExecuteAsync(CreateContext());

        await _runRepository.Received(1).PurgeOlderThanAsync(
            Now.AddDays(-30),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReportsHowManyRunsWereDeleted()
    {
        _runRepository.PurgeOlderThanAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(7);

        var result = await CreateSut().ExecuteAsync(CreateContext());

        result.Success.ShouldBeTrue();
        result.Summary!.ShouldContain("7");
    }
}

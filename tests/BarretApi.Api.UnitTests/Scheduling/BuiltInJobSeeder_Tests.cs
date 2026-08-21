using BarretApi.Api.Scheduling;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;

namespace BarretApi.Api.UnitTests.Scheduling;

public sealed class BuiltInJobSeeder_Tests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

    private readonly IScheduledJobRepository _jobRepository = Substitute.For<IScheduledJobRepository>();
    private readonly FakeTimeProvider _timeProvider = new(Now);

    private BuiltInJobSeeder CreateSut()
        => new(_jobRepository, _timeProvider, NullLogger<BuiltInJobSeeder>.Instance);

    [Fact]
    public async Task CreatesThePurgeJob_GivenItDoesNotExist()
    {
        _jobRepository.GetByNameAsync("purge-job-runs", Arg.Any<CancellationToken>())
            .Returns((ScheduledJobRecord?)null);

        await CreateSut().SeedAsync();

        await _jobRepository.Received(1).CreateAsync(
            Arg.Is<ScheduledJobRecord>(j =>
                j!.Name == "purge-job-runs"
                && j.JobType == "purge-job-runs"
                && j.CronExpression == "0 3 * * *"
                && j.TimeZoneId == "UTC"
                && j.IsEnabled),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetsTheNextRunFromNow()
    {
        ScheduledJobRecord? created = null;
        _jobRepository.GetByNameAsync("purge-job-runs", Arg.Any<CancellationToken>())
            .Returns((ScheduledJobRecord?)null);
        await _jobRepository.CreateAsync(Arg.Do<ScheduledJobRecord>(j => created = j), Arg.Any<CancellationToken>());

        await CreateSut().SeedAsync();

        created!.NextRunUtc.ShouldBe(new DateTimeOffset(2026, 8, 21, 3, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task LeavesAnExistingPurgeJobAlone()
    {
        _jobRepository.GetByNameAsync("purge-job-runs", Arg.Any<CancellationToken>())
            .Returns(new ScheduledJobRecord
            {
                Name = "purge-job-runs",
                DisplayName = "Purge job run history",
                JobType = "purge-job-runs",
                CronExpression = "0 5 * * *",
                IsEnabled = false
            });

        await CreateSut().SeedAsync();

        await _jobRepository.DidNotReceiveWithAnyArgs().CreateAsync(default!, default);
        await _jobRepository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
    }

    [Fact]
    public async Task DoesNotThrow_GivenStorageIsUnavailable()
    {
        _jobRepository.GetByNameAsync("purge-job-runs", Arg.Any<CancellationToken>())
            .Returns<ScheduledJobRecord?>(_ => throw new InvalidOperationException("storage down"));

        await Should.NotThrowAsync(() => CreateSut().SeedAsync());
    }
}

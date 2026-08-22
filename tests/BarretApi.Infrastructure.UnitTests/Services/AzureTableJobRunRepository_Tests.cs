using Azure;
using Azure.Data.Tables;
using BarretApi.Core.Configuration;
using BarretApi.Core.Models;
using BarretApi.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace BarretApi.Infrastructure.UnitTests.Services;

public sealed class AzureTableJobRunRepository_Tests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

    private readonly TableClient _tableClient = Substitute.For<TableClient>();

    private AzureTableJobRunRepository CreateSut()
        => new(
            _tableClient,
            Options.Create(new JobSchedulerOptions
            {
                TableStorage = new JobSchedulerTableStorageOptions
                {
                    ConnectionString = "UseDevelopmentStorage=true"
                }
            }),
            NullLogger<AzureTableJobRunRepository>.Instance);

    private static JobRunRecord CreateRun(string runId = "run-1", DateTimeOffset? startedAt = null)
        => new()
        {
            RunId = runId,
            JobName = "daily-tip",
            JobType = "tip-of-day",
            TriggerType = JobTriggerType.Scheduled,
            ScheduledForUtc = startedAt ?? Now,
            StartedAtUtc = startedAt ?? Now,
            CompletedAtUtc = (startedAt ?? Now).AddSeconds(3),
            DurationMs = 3_000,
            Status = JobRunStatus.Succeeded,
            AttemptCount = 1,
            Summary = "posted"
        };

    private static TableEntity CreateEntity(string runId, DateTimeOffset startedAt)
        => new("daily-tip", AzureTableJobRunRepository.BuildRowKey(startedAt, runId))
        {
            ["RunId"] = runId,
            ["JobType"] = "tip-of-day",
            ["TriggerType"] = "Scheduled",
            ["ScheduledForUtc"] = startedAt,
            ["StartedAtUtc"] = startedAt,
            ["CompletedAtUtc"] = startedAt.AddSeconds(3),
            ["DurationMs"] = 3_000L,
            ["Status"] = "Succeeded",
            ["AttemptCount"] = 1,
            ["Summary"] = "posted"
        };

    private void SetQueryResult(params TableEntity[] entities)
    {
        var pageable = Substitute.For<AsyncPageable<TableEntity>>();
        pageable.GetAsyncEnumerator(Arg.Any<CancellationToken>())
            .Returns(AsyncEnumeratorOf(entities));
        _tableClient.QueryAsync<TableEntity>(
                Arg.Any<string>(),
                Arg.Any<int?>(),
                Arg.Any<IEnumerable<string>>(),
                Arg.Any<CancellationToken>())
            .Returns(pageable);
    }

    private static async IAsyncEnumerator<T> AsyncEnumeratorOf<T>(params T[] items)
    {
        foreach (var item in items)
        {
            yield return item;
        }

        await Task.CompletedTask;
    }

    [Fact]
    public async Task AddAsync_PartitionsByJobName()
    {
        await CreateSut().AddAsync(CreateRun());

        await _tableClient.Received(1).AddEntityAsync(
            Arg.Is<TableEntity>(e => e!.PartitionKey == "daily-tip"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void BuildRowKey_SortsNewestFirst()
    {
        var older = AzureTableJobRunRepository.BuildRowKey(Now.AddHours(-1), "run-old");
        var newer = AzureTableJobRunRepository.BuildRowKey(Now, "run-new");

        string.CompareOrdinal(newer, older).ShouldBeLessThan(0);
    }

    [Fact]
    public async Task GetByJobAsync_MapsEveryStoredField()
    {
        SetQueryResult(CreateEntity("run-1", Now));

        var runs = await CreateSut().GetByJobAsync("daily-tip", 10);

        var run = runs.ShouldHaveSingleItem();
        run.RunId.ShouldBe("run-1");
        run.JobName.ShouldBe("daily-tip");
        run.JobType.ShouldBe("tip-of-day");
        run.TriggerType.ShouldBe(JobTriggerType.Scheduled);
        run.Status.ShouldBe(JobRunStatus.Succeeded);
        run.AttemptCount.ShouldBe(1);
        run.DurationMs.ShouldBe(3_000);
        run.Summary.ShouldBe("posted");
    }

    [Fact]
    public async Task GetByJobAsync_StopsAtMaxCount()
    {
        SetQueryResult(
            CreateEntity("run-3", Now),
            CreateEntity("run-2", Now.AddMinutes(-1)),
            CreateEntity("run-1", Now.AddMinutes(-2)));

        var runs = await CreateSut().GetByJobAsync("daily-tip", 2);

        runs.Count.ShouldBe(2);
        runs[0].RunId.ShouldBe("run-3");
    }

    [Fact]
    public async Task PurgeOlderThanAsync_DeletesOnlyRunsStartedBeforeTheCutoff()
    {
        SetQueryResult(
            CreateEntity("old", Now.AddDays(-40)),
            CreateEntity("recent", Now.AddDays(-1)));

        var deleted = await CreateSut().PurgeOlderThanAsync(Now.AddDays(-30));

        deleted.ShouldBe(1);
        await _tableClient.Received(1).DeleteEntityAsync(
            "daily-tip",
            Arg.Is<string>(rowKey => rowKey!.EndsWith("old")),
            Arg.Any<ETag>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PurgeOlderThanAsync_ReturnsZero_GivenNothingIsOldEnough()
    {
        SetQueryResult(CreateEntity("recent", Now.AddDays(-1)));

        var deleted = await CreateSut().PurgeOlderThanAsync(Now.AddDays(-30));

        deleted.ShouldBe(0);
        await _tableClient.DidNotReceiveWithAnyArgs().DeleteEntityAsync(default!, default!, default, default);
    }

    [Fact]
    public async Task PurgeOlderThanAsync_ScansWithNoFilterAndSelectsOnlyWhatItNeeds()
    {
        SetQueryResult();

        await CreateSut().PurgeOlderThanAsync(Now.AddDays(-30));

        _tableClient.Received(1).QueryAsync<TableEntity>(
            (string?)null,
            Arg.Any<int?>(),
            Arg.Is<IEnumerable<string>>(select =>
                select != null
                && select.Contains("PartitionKey")
                && select.Contains("RowKey")
                && select.Contains("StartedAtUtc")),
            Arg.Any<CancellationToken>());
    }
}

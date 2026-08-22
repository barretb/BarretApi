using Azure;
using Azure.Data.Tables;
using BarretApi.Core.Configuration;
using BarretApi.Core.Models;
using BarretApi.Infrastructure.Services;
using BarretApi.Infrastructure.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace BarretApi.Infrastructure.UnitTests.Services;

public sealed class AzureTableScheduledJobRepository_Tests
{
    private const string PartitionKey = "scheduled-job";
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

    private readonly TableClient _tableClient = Substitute.For<TableClient>();

    private AzureTableScheduledJobRepository CreateSut()
        => new(
            _tableClient,
            Options.Create(new JobSchedulerOptions
            {
                TableStorage = new JobSchedulerTableStorageOptions
                {
                    ConnectionString = "UseDevelopmentStorage=true",
                    PartitionKey = PartitionKey
                }
            }),
            NullLogger<AzureTableScheduledJobRepository>.Instance);

    private static ScheduledJobRecord CreateJob(string name = "daily-tip")
        => new()
        {
            Name = name,
            DisplayName = "Daily tip",
            JobType = "tip-of-day",
            CronExpression = "0 12 * * *",
            TimeZoneId = "America/Chicago",
            ArgumentsJson = """{"category":"dotnet"}""",
            IsEnabled = true,
            NextRunUtc = Now.AddHours(1),
            MaxRetryCount = 2,
            RetryBaseDelaySeconds = 30,
            CreatedAtUtc = Now,
            UpdatedAtUtc = Now,
            ETag = "etag-1"
        };

    private static TableEntity CreateEntity(
        string name,
        bool isEnabled = true,
        DateTimeOffset? nextRunUtc = null,
        string runState = "Idle")
        => new(PartitionKey, name)
        {
            ["DisplayName"] = "Daily tip",
            ["JobType"] = "tip-of-day",
            ["CronExpression"] = "0 12 * * *",
            ["TimeZoneId"] = "America/Chicago",
            ["ArgumentsJson"] = """{"category":"dotnet"}""",
            ["IsEnabled"] = isEnabled,
            ["NextRunUtc"] = nextRunUtc ?? Now.AddHours(1),
            ["ConsecutiveFailureCount"] = 0,
            ["MaxRetryCount"] = 2,
            ["RetryBaseDelaySeconds"] = 30,
            ["RunState"] = runState,
            ["CreatedAtUtc"] = Now,
            ["UpdatedAtUtc"] = Now
        };

    private void SetQueryResult(params TableEntity[] entities)
    {
        var pageable = Substitute.For<AsyncPageable<TableEntity>>();
        pageable.GetAsyncEnumerator(Arg.Any<CancellationToken>())
            .Returns(AsyncEnumeratorOf(entities));
        _tableClient.QueryAsync<TableEntity>(
                Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(pageable);
    }

    private void SetUpdateResult(string eTag)
    {
        _tableClient.UpdateEntityAsync(
                Arg.Any<TableEntity>(),
                Arg.Any<ETag>(),
                Arg.Any<TableUpdateMode>(),
                Arg.Any<CancellationToken>())
            .Returns(new FakeResponse(eTag));
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
    public async Task GetAllAsync_MapsEveryStoredField()
    {
        SetQueryResult(CreateEntity("daily-tip"));

        var jobs = await CreateSut().GetAllAsync();

        var job = jobs.ShouldHaveSingleItem();
        job.Name.ShouldBe("daily-tip");
        job.JobType.ShouldBe("tip-of-day");
        job.CronExpression.ShouldBe("0 12 * * *");
        job.TimeZoneId.ShouldBe("America/Chicago");
        job.ArgumentsJson.ShouldBe("""{"category":"dotnet"}""");
        job.IsEnabled.ShouldBeTrue();
        job.MaxRetryCount.ShouldBe(2);
        job.RetryBaseDelaySeconds.ShouldBe(30);
        job.RunState.ShouldBe(JobRunState.Idle);
    }

    [Fact]
    public async Task GetDueAsync_ReturnsEnabledJobsWhoseNextRunHasPassed()
    {
        SetQueryResult(
            CreateEntity("due", nextRunUtc: Now.AddMinutes(-1)),
            CreateEntity("not-yet", nextRunUtc: Now.AddMinutes(5)));

        var due = await CreateSut().GetDueAsync(Now);

        due.ShouldHaveSingleItem().Name.ShouldBe("due");
    }

    [Fact]
    public async Task GetDueAsync_ExcludesDisabledJobs()
    {
        SetQueryResult(CreateEntity("paused", isEnabled: false, nextRunUtc: Now.AddMinutes(-1)));

        var due = await CreateSut().GetDueAsync(Now);

        due.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetDueAsync_ExcludesJobsWithNoNextRunTime()
    {
        var entity = CreateEntity("never");
        entity["NextRunUtc"] = null;
        SetQueryResult(entity);

        var due = await CreateSut().GetDueAsync(Now);

        due.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetDueAsync_IncludesRunningJobsSoTheCallerCanJudgeStaleness()
    {
        SetQueryResult(CreateEntity("busy", nextRunUtc: Now.AddMinutes(-1), runState: "Running"));

        var due = await CreateSut().GetDueAsync(Now);

        due.ShouldHaveSingleItem().RunState.ShouldBe(JobRunState.Running);
    }

    [Fact]
    public async Task GetByNameAsync_ReturnsNull_GivenNoSuchJob()
    {
        _tableClient.GetEntityIfExistsAsync<TableEntity>(
                PartitionKey,
                "missing",
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(Substitute.For<NullableResponse<TableEntity>>());

        var job = await CreateSut().GetByNameAsync("missing");

        job.ShouldBeNull();
    }

    [Fact]
    public async Task CreateAsync_WritesTheEntity()
    {
        await CreateSut().CreateAsync(CreateJob());

        await _tableClient.Received(1).AddEntityAsync(
            Arg.Is<TableEntity>(e => e!.RowKey == "daily-tip" && e.PartitionKey == PartitionKey),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TryClaimAsync_MarksTheJobRunning_GivenTheUpdateSucceeds()
    {
        SetUpdateResult("etag-2");
        var job = CreateJob();

        var claimed = await CreateSut().TryClaimAsync(job, Now);

        claimed.ShouldBeTrue();
        job.RunState.ShouldBe(JobRunState.Running);
        job.ClaimedAtUtc.ShouldBe(Now);
        job.ETag.ShouldBe("etag-2");
    }

    [Fact]
    public async Task TryClaimAsync_UsesTheRecordsETagForConcurrency()
    {
        SetUpdateResult("etag-2");

        await CreateSut().TryClaimAsync(CreateJob(), Now);

        await _tableClient.Received(1).UpdateEntityAsync(
            Arg.Any<TableEntity>(),
            new ETag("etag-1"),
            TableUpdateMode.Replace,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TryClaimAsync_ReturnsFalse_GivenAPreconditionFailure()
    {
        _tableClient.UpdateEntityAsync(
                Arg.Any<TableEntity>(),
                Arg.Any<ETag>(),
                Arg.Any<TableUpdateMode>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new RequestFailedException(412, "precondition failed"));
        var job = CreateJob();
        job.UpdatedAtUtc = Now.AddMinutes(-45);

        var claimed = await CreateSut().TryClaimAsync(job, Now);

        claimed.ShouldBeFalse();
        job.RunState.ShouldBe(JobRunState.Idle);
        job.UpdatedAtUtc.ShouldBe(Now.AddMinutes(-45));
    }

    [Fact]
    public async Task TryClaimAsync_Rethrows_GivenAnUnrelatedFailure()
    {
        _tableClient.UpdateEntityAsync(
                Arg.Any<TableEntity>(),
                Arg.Any<ETag>(),
                Arg.Any<TableUpdateMode>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new RequestFailedException(503, "service unavailable"));

        await Should.ThrowAsync<RequestFailedException>(() => CreateSut().TryClaimAsync(CreateJob(), Now));
    }

    [Fact]
    public async Task UpdateAsync_ReplacesUnconditionally()
    {
        SetUpdateResult("etag-2");

        await CreateSut().UpdateAsync(CreateJob());

        await _tableClient.Received(1).UpdateEntityAsync(
            Arg.Any<TableEntity>(),
            ETag.All,
            TableUpdateMode.Replace,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_DeletesByName()
    {
        await CreateSut().DeleteAsync("daily-tip");

        await _tableClient.Received(1).DeleteEntityAsync(
            PartitionKey,
            "daily-tip",
            Arg.Any<ETag>(),
            Arg.Any<CancellationToken>());
    }
}

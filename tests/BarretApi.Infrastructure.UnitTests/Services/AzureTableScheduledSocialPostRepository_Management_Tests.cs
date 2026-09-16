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

public sealed class AzureTableScheduledSocialPostRepository_Management_Tests
{
	private readonly TableClient _table = Substitute.For<TableClient>();
	private static readonly DateTimeOffset Now = new(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);

	[Fact]
	public async Task ReadsLatestVersion_GivenExistingRecord()
	{
		var entity = Entity("post");
		entity.ETag = new ETag("current-version");
		_table.GetEntityIfExistsAsync<TableEntity>("posts", "post", cancellationToken: Arg.Any<CancellationToken>())
			.Returns(Response.FromValue(entity, new FakeResponse()));

		var record = await Repository().GetByIdAsync("post");

		record!.Version.ShouldBe("current-version");
		record.ScheduledPostId.ShouldBe("post");
	}

	[Fact]
	public async Task ReturnsNull_GivenMissingRecord()
	{
		_table.GetEntityIfExistsAsync<TableEntity>("posts", "missing", cancellationToken: Arg.Any<CancellationToken>())
			.Returns(Substitute.For<NullableResponse<TableEntity>>());

		(await Repository().GetByIdAsync("missing")).ShouldBeNull();
	}

	[Fact]
	public async Task UsesFiltersAndContinuation_GivenPagedQuery()
	{
		var pageable = Substitute.For<AsyncPageable<TableEntity>>();
		pageable.AsPages("cursor", 2).Returns(Pages(
			Page<TableEntity>.FromValues([Entity("one"), Entity("two")], "next", new FakeResponse()),
			Page<TableEntity>.FromValues([Entity("three")], null, new FakeResponse())));
		_table.QueryAsync<TableEntity>(Arg.Any<string>(), 2, cancellationToken: Arg.Any<CancellationToken>()).Returns(pageable);

		var result = await Repository("po'sts").ListAsync(new(
			ScheduledPostStatus.NeedsReview, Now, Now.AddDays(1), 2, "cursor"));

		result.Posts.Select(p => p.ScheduledPostId).ShouldBe(["one", "two"]);
		result.ContinuationToken.ShouldBe("next");
		_table.Received(1).QueryAsync<TableEntity>(Arg.Is<string>(filter => filter != null
			&& filter.Contains("PartitionKey eq 'po''sts'") && filter.Contains("Status eq 'NeedsReview'")
			&& filter.Contains("ScheduledForUtc ge") && filter.Contains("ScheduledForUtc le")),
			2, cancellationToken: Arg.Any<CancellationToken>());
		pageable.Received(1).AsPages("cursor", 2);
	}

	[Fact]
	public async Task PreservesContinuation_GivenEmptyStoragePage()
	{
		var pageable = AsyncPageable<TableEntity>.FromPages([Page<TableEntity>.FromValues([], "more", new FakeResponse())]);
		_table.QueryAsync<TableEntity>(Arg.Any<string>(), 50, cancellationToken: Arg.Any<CancellationToken>()).Returns(pageable);

		var result = await Repository().ListAsync(new());

		result.Posts.ShouldBeEmpty();
		result.ContinuationToken.ShouldBe("more");
	}

	[Theory]
	[InlineData(404)]
	[InlineData(412)]
	public async Task ReturnsConflictWithoutWildcardWrite_GivenConcurrentChange(int status)
	{
		_table.UpdateEntityAsync(Arg.Any<TableEntity>(), Arg.Any<ETag>(), TableUpdateMode.Replace, Arg.Any<CancellationToken>())
			.ThrowsAsync(new RequestFailedException(status, "conflict"));
		var record = Record();

		var result = await Repository().TryUpdateAsync(record);

		result.ShouldBeFalse();
		await _table.Received(1).UpdateEntityAsync(Arg.Any<TableEntity>(), new ETag("v1"), TableUpdateMode.Replace, Arg.Any<CancellationToken>());
		record.Version.ShouldBe("v1");
	}

	[Fact]
	public async Task ReturnsNewVersion_GivenSuccessfulConditionalUpdate()
	{
		_table.UpdateEntityAsync(Arg.Any<TableEntity>(), Arg.Any<ETag>(), TableUpdateMode.Replace, Arg.Any<CancellationToken>())
			.Returns(new FakeResponse("v2"));
		var record = Record();

		(await Repository().TryUpdateAsync(record)).ShouldBeTrue();

		record.Version.ShouldBe(new FakeResponse("v2").Headers.ETag!.Value.ToString());
	}

	[Fact]
	public async Task RejectsWildcardVersion_GivenManagementUpdate()
	{
		var record = Record();
		record.Version = "*";

		await Should.ThrowAsync<ArgumentException>(() => Repository().TryUpdateAsync(record));
		await _table.DidNotReceiveWithAnyArgs().UpdateEntityAsync(default(TableEntity)!, default, default, default);
	}

	[Fact]
	public async Task ReportsInvalidCursor_GivenStorageRejection()
	{
		_table.QueryAsync<TableEntity>(Arg.Any<string>(), 50, cancellationToken: Arg.Any<CancellationToken>())
			.Throws(new RequestFailedException(400, "bad cursor"));

		await Should.ThrowAsync<ArgumentException>(() => Repository().ListAsync(new(ContinuationToken: "bad")));
	}

	[Fact]
	public void PreservesManagementMetadata_GivenRoundTrip()
	{
		var record = Record();
		record.Status = ScheduledPostStatus.Cancelled;
		record.UpdatedAtUtc = Now;
		record.LastManagementAction = "Cancelled";
		record.ManagementNote = "No longer needed";
		record.DeliveryConfirmations.Add(new("bluesky", "remote", "https://example.com/post", Now, "Verified"));

		var entity = AzureTableScheduledSocialPostRepository.MapModelToEntity(record, "posts");
		var restored = AzureTableScheduledSocialPostRepository.MapEntityToModel(entity);

		restored.Status.ShouldBe(ScheduledPostStatus.Cancelled);
		restored.ManagementNote.ShouldBe("No longer needed");
		restored.UpdatedAtUtc.ShouldBe(Now);
		restored.DeliveryConfirmations.Single().PostId.ShouldBe("remote");
	}

	[Theory]
	[InlineData(0)]
	[InlineData(101)]
	public async Task RejectsUnboundedPageSize_GivenInvalidQuery(int size)
	{
		await Should.ThrowAsync<ArgumentOutOfRangeException>(() => Repository().ListAsync(new(PageSize: size)));
	}

	private AzureTableScheduledSocialPostRepository Repository(string partition = "posts")
	{
		return new(_table, Substitute.For<TableServiceClient>(),
			Options.Create(new ScheduledSocialPostOptions
			{
				TableStorage = new ScheduledSocialPostTableStorageOptions { ConnectionString = "UseDevelopmentStorage=true", PartitionKey = partition }
			}), NullLogger<AzureTableScheduledSocialPostRepository>.Instance);
	}

	private static ScheduledSocialPostRecord Record()
	{
		return new()
		{
			ScheduledPostId = "post",
			Status = ScheduledPostStatus.Pending,
			Text = "text",
			ScheduledForUtc = Now,
			CreatedAtUtc = Now,
			Version = "v1"
		};
	}

	private static TableEntity Entity(string id)
	{
		return new("posts", id) { ["Status"] = "NeedsReview", ["Text"] = "text", ["ScheduledForUtc"] = Now };
	}

	private static async IAsyncEnumerable<Page<TableEntity>> Pages(params Page<TableEntity>[] pages)
	{
		foreach (var page in pages)
		{
			yield return page;
		}

		await Task.CompletedTask;
	}
}

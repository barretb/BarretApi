using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Core.Services;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;

namespace BarretApi.Core.UnitTests.Services;

public sealed class ScheduledPostManagementService_Tests
{
	private static readonly DateTimeOffset Now = new(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);
	private readonly IScheduledSocialPostRepository _repository = Substitute.For<IScheduledSocialPostRepository>();
	private readonly FakeTimeProvider _clock = new(Now);
	private readonly ScheduledPostManagementService _service;

	public ScheduledPostManagementService_Tests()
	{
		var clients = new[] { "bluesky", "mastodon", "linkedin" }.Select(name =>
		{
			var client = Substitute.For<ISocialPlatformClient>();
			client.PlatformName.Returns(name);
			return client;
		}).ToArray();
		_service = new ScheduledPostManagementService(_repository, clients, _clock);
		_repository.TryUpdateAsync(Arg.Any<ScheduledSocialPostRecord>(), Arg.Any<CancellationToken>()).Returns(call =>
		{
			call.Arg<ScheduledSocialPostRecord>()!.Version = "v2";
			return true;
		});
	}

	[Fact]
	public async Task UpdatesMergedContentAndSchedule_GivenUnattemptedPendingPost()
	{
		var record = Given();
		var command = new UpdateScheduledPostCommand("v1", Text: "updated", Hashtags: ["dotnet"],
			Platforms: ["MASTODON"], AutoThread: true, ScheduledForUtc: Now.AddHours(2));

		var result = await _service.UpdateAsync("post", command);

		result.Outcome.ShouldBe(PostManagementOutcome.Success);
		record.Text.ShouldBe("updated");
		record.TargetPlatforms.ShouldBe(["mastodon"]);
		record.ScheduledForUtc.ShouldBe(Now.AddHours(2));
		record.AutoThread.ShouldBeTrue();
		record.UpdatedAtUtc.ShouldBe(Now);
		result.Post!.Version.ShouldBe("v2");
	}

	[Theory]
	[InlineData(ScheduledPostStatus.Processing)]
	[InlineData(ScheduledPostStatus.Published)]
	[InlineData(ScheduledPostStatus.Failed)]
	[InlineData(ScheduledPostStatus.NeedsReview)]
	[InlineData(ScheduledPostStatus.Cancelled)]
	public async Task RejectsEdit_GivenNonPendingState(ScheduledPostStatus status)
	{
		var record = Given(status);

		var result = await _service.UpdateAsync("post", new("v1", Text: "changed"));

		result.Outcome.ShouldBe(PostManagementOutcome.Conflict);
		record.Text.ShouldBe("original");
		await _repository.DidNotReceiveWithAnyArgs().TryUpdateAsync(default!, default);
	}

	[Fact]
	public async Task RejectsEdit_GivenRetryAlreadyAttempted()
	{
		var record = Given();
		record.AttemptCount = 1;

		var result = await _service.UpdateAsync("post", new("v1", Text: "different post"));

		result.Outcome.ShouldBe(PostManagementOutcome.Conflict);
	}

	[Fact]
	public async Task RejectsEdit_GivenReceiptsEvenWithoutAttemptCount()
	{
		var record = Given();
		record.DeliveryResults.Add(Receipt("bluesky", true));

		var result = await _service.UpdateAsync("post", new("v1", Text: "different post"));

		result.Outcome.ShouldBe(PostManagementOutcome.Conflict);
	}

	[Fact]
	public async Task ReturnsNotFound_GivenMissingRecord()
	{
		var result = await _service.CancelAsync("missing", new("v1", "cancel"));

		result.Outcome.ShouldBe(PostManagementOutcome.NotFound);
	}

	[Fact]
	public async Task RejectsStaleVersionBeforeMutation_GivenChangedPost()
	{
		var record = Given();

		var result = await _service.CancelAsync("post", new("stale", "cancel"));

		result.Outcome.ShouldBe(PostManagementOutcome.Conflict);
		record.Status.ShouldBe(ScheduledPostStatus.Pending);
		await _repository.DidNotReceiveWithAnyArgs().TryUpdateAsync(default!, default);
	}

	[Fact]
	public async Task ReportsConflict_GivenSchedulerWinsConcurrentWrite()
	{
		Given();
		_repository.TryUpdateAsync(Arg.Any<ScheduledSocialPostRecord>(), Arg.Any<CancellationToken>()).Returns(false);

		var result = await _service.CancelAsync("post", new("v1", "cancel"));

		result.Outcome.ShouldBe(PostManagementOutcome.Conflict);
	}

	[Fact]
	public async Task CancelsWithoutDeletingReceipts_GivenQueuedRetry()
	{
		var record = Given();
		record.AttemptCount = 1;
		record.DeliveryResults.Add(Receipt("bluesky", true));

		var result = await _service.CancelAsync("post", new("v1", "No longer needed"));

		result.Outcome.ShouldBe(PostManagementOutcome.Success);
		record.Status.ShouldBe(ScheduledPostStatus.Cancelled);
		record.DeliveryResults.Single().PostId.ShouldBe("published-id");
		ScheduledPostClaimPolicy.IsDue(record, Now.AddDays(1)).ShouldBeFalse();
	}

	[Fact]
	public async Task RejectsCancel_GivenProcessingPost()
	{
		Given(ScheduledPostStatus.Processing);

		var result = await _service.CancelAsync("post", new("v1", "cancel"));

		result.Outcome.ShouldBe(PostManagementOutcome.Conflict);
	}

	[Fact]
	public async Task ReconcilesPublishedPlatformWithoutQueuingOthers_GivenPartialResolution()
	{
		var record = Given(ScheduledPostStatus.NeedsReview);

		var result = await _service.ReconcileAsync("post", new("v1",
			[new("bluesky", "remote-id", "https://example.com/post")], "Verified remotely"));

		result.Outcome.ShouldBe(PostManagementOutcome.Success);
		record.Status.ShouldBe(ScheduledPostStatus.NeedsReview);
		record.DeliveryResults.Single().Success.ShouldBeTrue();
		record.DeliveryConfirmations.Single().Note.ShouldBe("Verified remotely");
		record.DeliveryConfirmations.Single().ConfirmedAtUtc.ShouldBe(Now);
		ScheduledPostClaimPolicy.IsDue(record, Now.AddDays(1)).ShouldBeFalse();
	}

	[Fact]
	public async Task MarksPublished_GivenAllDeliveriesConfirmed()
	{
		var record = Given(ScheduledPostStatus.NeedsReview);
		record.DeliveryResults.Add(Receipt("bluesky", true));

		await _service.ReconcileAsync("post", new("v1", [new("mastodon", "mastodon-id", null)], "Checked provider"));

		record.Status.ShouldBe(ScheduledPostStatus.Published);
		record.PublishedAtUtc.ShouldBe(Now);
		record.LastErrorCode.ShouldBeNull();
		record.DeliveryResults.Count(r => r.Success).ShouldBe(2);
	}

	[Theory]
	[InlineData("linkedin")]
	[InlineData("bluesky")]
	public async Task RejectsConfirmation_GivenNonTargetOrAlreadySuccessfulPlatform(string platform)
	{
		Given(ScheduledPostStatus.NeedsReview).DeliveryResults.Add(Receipt("bluesky", true));

		var result = await _service.ReconcileAsync("post", new("v1", [new(platform, "other", null)], "Verified"));

		result.Outcome.ShouldBe(PostManagementOutcome.Conflict);
		await _repository.DidNotReceiveWithAnyArgs().TryUpdateAsync(default!, default);
	}

	[Fact]
	public async Task QueuesOnlyRemainingDeliveries_GivenExplicitNonDeliveryConfirmation()
	{
		var record = Given(ScheduledPostStatus.NeedsReview);
		record.DeliveryResults.Add(Receipt("bluesky", true));

		var result = await _service.RetryAsync("post", new("v1", ["mastodon"], true, "No post exists remotely", Now.AddHours(1)));

		result.Outcome.ShouldBe(PostManagementOutcome.Success);
		record.Status.ShouldBe(ScheduledPostStatus.Pending);
		record.DeliveryResults.Single().Success.ShouldBeTrue();
		record.ScheduledForUtc.ShouldBe(Now.AddHours(1));
		record.DeliveryTrackingEnabled.ShouldBeTrue();
	}

	[Fact]
	public async Task RejectsRetry_GivenOnlySomeUnresolvedPlatforms()
	{
		Given(ScheduledPostStatus.NeedsReview);

		var result = await _service.RetryAsync("post", new("v1", ["mastodon"], true, "Not delivered"));

		result.Outcome.ShouldBe(PostManagementOutcome.Conflict);
	}

	[Fact]
	public async Task RejectsRetry_GivenSuccessfulPlatformIncluded()
	{
		Given(ScheduledPostStatus.Failed).DeliveryResults.Add(Receipt("bluesky", true));

		var result = await _service.RetryAsync("post", new("v1", ["bluesky", "mastodon"], true, "Retry"));

		result.Outcome.ShouldBe(PostManagementOutcome.Conflict);
	}

	[Fact]
	public async Task RejectsReplay_GivenPartialThread()
	{
		var record = Given(ScheduledPostStatus.NeedsReview);
		record.DeliveryResults.Add(new PlatformPostResult
		{
			Platform = "bluesky",
			Success = false,
			ThreadResults = [Receipt("bluesky", true), Receipt("bluesky", false)]
		});

		var result = await _service.RetryAsync("post", new("v1", ["bluesky", "mastodon"], true, "Retry"));

		result.Outcome.ShouldBe(PostManagementOutcome.Conflict);
	}

	[Fact]
	public async Task ResolvesLegacyImplicitTargets_GivenExplicitRetryOfEveryConfiguredPlatform()
	{
		var record = Given(ScheduledPostStatus.NeedsReview);
		record.TargetPlatforms = [];

		var result = await _service.RetryAsync("post", new("v1", ["bluesky", "mastodon", "linkedin"], true, "Checked all accounts"));

		result.Outcome.ShouldBe(PostManagementOutcome.Success);
		record.TargetPlatforms.Count.ShouldBe(3);
		record.DeliveryTrackingEnabled.ShouldBeTrue();
	}

	[Theory]
	[InlineData(false, "note")]
	[InlineData(true, "")]
	public async Task RequiresExplicitConfirmationAndNote_GivenRetry(bool confirm, string note)
	{
		Given(ScheduledPostStatus.NeedsReview);

		await Should.ThrowAsync<ArgumentException>(() =>
			_service.RetryAsync("post", new("v1", ["bluesky", "mastodon"], confirm, note)));
	}

	[Fact]
	public async Task ValidatesFinalAttachmentCount_GivenExistingUploadedImages()
	{
		var record = Given();
		record.UploadedImages = Enumerable.Range(0, 4).Select(i => new StoredImageData
		{
			BlobName = $"blob-{i}",
			AltText = "image",
			ContentType = "image/png"
		}).ToList();

		await Should.ThrowAsync<ArgumentException>(() => _service.UpdateAsync("post",
			new("v1", Images: [new ImageUrl { Url = "https://example.com/image.png", AltText = "new" }])));
	}

	[Fact]
	public async Task AllowsImageOnlyPost_GivenRetainedUploadedAttachment()
	{
		var record = Given();
		record.UploadedImages = [new StoredImageData { BlobName = "blob", AltText = "image", ContentType = "image/png" }];

		var result = await _service.UpdateAsync("post", new("v1", Text: ""));

		result.Outcome.ShouldBe(PostManagementOutcome.Success);
		record.UploadedImages.Count.ShouldBe(1);
	}

	[Fact]
	public async Task RejectsEmptyContent_GivenRemovalOfLastImage()
	{
		var record = Given();
		record.Text = "";
		record.UploadedImages = [new StoredImageData { BlobName = "blob", AltText = "image", ContentType = "image/png" }];

		await Should.ThrowAsync<ArgumentException>(() => _service.UpdateAsync("post", new("v1", RemoveUploadedImages: true)));
	}

	[Theory]
	[InlineData("")]
	[InlineData("*")]
	public async Task RejectsUnsafeVersion_GivenMutation(string version)
	{
		Given();

		await Should.ThrowAsync<ArgumentException>(() => _service.CancelAsync("post", new(version, "cancel")));
	}

	[Fact]
	public async Task RejectsPastSchedule_GivenReschedule()
	{
		Given();

		await Should.ThrowAsync<ArgumentException>(() => _service.UpdateAsync("post", new("v1", ScheduledForUtc: Now)));
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task RejectsReplay_GivenPublishedIdentifierOrUrl(bool hasId)
	{
		var record = Given(ScheduledPostStatus.NeedsReview);
		record.DeliveryResults.Add(new PlatformPostResult
		{
			Platform = "bluesky",
			Success = false,
			PostId = hasId ? "remote-id" : null,
			PostUrl = hasId ? null : "https://example.com/post"
		});

		var result = await _service.RetryAsync("post", new("v1", ["bluesky", "mastodon"], true, "retry"));

		result.Outcome.ShouldBe(PostManagementOutcome.Conflict);
	}

	[Fact]
	public async Task ReconcilesLegacyDuplicates_GivenRepeatedTargetNames()
	{
		var record = Given(ScheduledPostStatus.NeedsReview);
		record.TargetPlatforms = ["bluesky", "BLUESKY"];

		var result = await _service.ReconcileAsync("post", new("v1", [new("bluesky", "remote-id", null)], "Verified"));

		result.Outcome.ShouldBe(PostManagementOutcome.Success);
		record.TargetPlatforms.Count.ShouldBe(1);
		record.Status.ShouldBe(ScheduledPostStatus.Published);
	}

	private ScheduledSocialPostRecord Given(ScheduledPostStatus status = ScheduledPostStatus.Pending)
	{
		var record = new ScheduledSocialPostRecord
		{
			ScheduledPostId = "post",
			ScheduledForUtc = Now.AddHours(1),
			CreatedAtUtc = Now.AddDays(-1),
			Status = status,
			Text = "original",
			TargetPlatforms = ["bluesky", "mastodon"],
			Version = "v1"
		};
		_repository.GetByIdAsync("post", Arg.Any<CancellationToken>()).Returns(record);
		return record;
	}

	private static PlatformPostResult Receipt(string platform, bool success)
	{
		return new PlatformPostResult { Platform = platform, Success = success, PostId = success ? "published-id" : null };
	}
}

using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace BarretApi.Core.UnitTests.Services;

public sealed class ScheduledSocialPostProcessor_ProcessDueAsync_Tests
{
	private readonly IScheduledSocialPostRepository _repository = Substitute.For<IScheduledSocialPostRepository>();
	private readonly IScheduledPostImageStore _images = Substitute.For<IScheduledPostImageStore>();
	private readonly ISocialPlatformClient _first = Client("bluesky");
	private readonly ISocialPlatformClient _second = Client("mastodon");

	[Fact]
	public async Task RetriesOnlyFailedPlatform_GivenPartialSuccess()
	{
		var record = Record();
		ConfigureRecords(record);
		_second.PostAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<UploadedImage>>(), Arg.Any<CancellationToken>())
			.Returns(Result("mastodon", false, "RATE_LIMITED"), Result("mastodon", true));
		var processor = Processor();

		var firstRun = await processor.ProcessDueAsync(null);
		var secondRun = await processor.ProcessDueAsync(null);

		firstRun.FailedCount.ShouldBe(1);
		secondRun.SucceededCount.ShouldBe(1);
		record.Status.ShouldBe(ScheduledPostStatus.Published);
		await _first.Received(1).PostAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<UploadedImage>>(), Arg.Any<CancellationToken>());
		await _second.Received(2).PostAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<UploadedImage>>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task ContinuesBatch_GivenMissingUploadedImage()
	{
		var broken = Record("broken", [new StoredImageData { BlobName = "missing", ContentType = "image/png", AltText = "image" }]);
		var healthy = Record("healthy");
		ConfigureRecords(broken, healthy);
		_images.DownloadAsync("missing", Arg.Any<CancellationToken>()).ThrowsAsync(new IOException("missing blob"));

		var summary = await Processor().ProcessDueAsync(null);

		summary.FailedCount.ShouldBe(1);
		summary.SucceededCount.ShouldBe(1);
		broken.Status.ShouldBe(ScheduledPostStatus.Failed);
		broken.LastErrorCode.ShouldBe("PREPARATION_FAILED");
		healthy.Status.ShouldBe(ScheduledPostStatus.Published);
	}

	[Fact]
	public async Task FlagsReviewWithoutPublishing_GivenExpiredClaim()
	{
		var record = Record();
		record.Status = ScheduledPostStatus.Processing;
		record.LeaseExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
		ConfigureRecords(record);

		var summary = await Processor().ProcessDueAsync(null);

		summary.FailedCount.ShouldBe(1);
		record.Status.ShouldBe(ScheduledPostStatus.NeedsReview);
		await _first.DidNotReceiveWithAnyArgs().PostAsync(default!, default!, default);
		await _second.DidNotReceiveWithAnyArgs().PostAsync(default!, default!, default);
	}

	[Fact]
	public async Task DoesNotReplayThread_GivenPartialThreadSuccess()
	{
		var record = Record(autoThread: true);
		ConfigureRecords(record);
		_first.PostThreadAsync(Arg.Any<IReadOnlyList<ThreadSegmentPost>>(), Arg.Any<CancellationToken>())
			.Returns(new[] { Result("bluesky", true), Result("bluesky", false, "RATE_LIMITED") });

		await Processor().ProcessDueAsync(null);
		await Processor().ProcessDueAsync(null);

		record.Status.ShouldBe(ScheduledPostStatus.NeedsReview);
		await _first.Received(1).PostThreadAsync(Arg.Any<IReadOnlyList<ThreadSegmentPost>>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task UsesClaimedSnapshot_GivenOutdatedQueryResults()
	{
		var stale = Record();
		var latest = Record();
		latest.DeliveryResults.Add(Result("bluesky", true));
		_repository.GetDueForProcessingAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(new[] { stale });
		_repository.TryClaimAsync(stale.ScheduledPostId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
			.Returns(_ => { ScheduledPostClaimPolicy.TryClaim(latest, DateTimeOffset.UtcNow); return latest; });

		await Processor().ProcessDueAsync(null);

		await _first.DidNotReceiveWithAnyArgs().PostAsync(default!, default!, default);
		latest.Status.ShouldBe(ScheduledPostStatus.Published);
	}

	[Fact]
	public async Task StopsPublishingOtherPlatforms_GivenReceiptStorageFailure()
	{
		var record = Record();
		ConfigureRecords(record);
		_repository.SaveClaimAsync(record, Arg.Any<CancellationToken>()).Returns(_ =>
			record.DeliveryResults.Count > 0 ? Task.FromException(new IOException("storage unavailable")) : Task.CompletedTask);

		var result = await Processor().ProcessDueAsync(null);

		result.FailedCount.ShouldBe(1);
		await _second.DidNotReceiveWithAnyArgs().PostAsync(default!, default!, default);
	}

	[Fact]
	public async Task FlagsReview_GivenAmbiguousProviderFailure()
	{
		var record = Record();
		ConfigureRecords(record);
		_first.PostAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<UploadedImage>>(), Arg.Any<CancellationToken>())
			.Returns(Result("bluesky", false, "PLATFORM_ERROR"));

		await Processor().ProcessDueAsync(null);

		record.Status.ShouldBe(ScheduledPostStatus.NeedsReview);
	}

	[Fact]
	public async Task SkipsPublishing_GivenClaimOwnedByAnotherWorker()
	{
		var record = Record();
		_repository.GetDueForProcessingAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(new[] { record });
		_repository.TryClaimAsync(record.ScheduledPostId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns((ScheduledSocialPostRecord?)null);

		var summary = await Processor().ProcessDueAsync(null);

		summary.SkippedCount.ShouldBe(1);
		await _first.DidNotReceiveWithAnyArgs().PostAsync(default!, default!, default);
	}

	[Fact]
	public async Task DoesNotClaimOrPublish_GivenCancelledRun()
	{
		var record = Record();
		ConfigureRecords(record);
		using var cancellation = new CancellationTokenSource();
		await cancellation.CancelAsync();

		await Should.ThrowAsync<OperationCanceledException>(() => Processor().ProcessDueAsync(null, cancellation.Token));

		await _repository.DidNotReceiveWithAnyArgs().TryClaimAsync(default!, default, default);
		await _first.DidNotReceiveWithAnyArgs().PostAsync(default!, default!, default);
	}

	private void ConfigureRecords(params ScheduledSocialPostRecord[] records)
	{
		_repository.GetDueForProcessingAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
			.Returns(_ => records.Where(r => ScheduledPostClaimPolicy.IsDue(r, DateTimeOffset.UtcNow)).ToArray());
		_repository.TryClaimAsync(Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
			.Returns(call =>
			{
				var record = records.Single(r => r.ScheduledPostId == call.Arg<string>());
				return ScheduledPostClaimPolicy.TryClaim(record, call.Arg<DateTimeOffset>()) ? record : null;
			});
	}

	private ScheduledSocialPostProcessor Processor()
	{
		var service = new SocialPostService([_first, _second], new TextShorteningService(), new TextSplitterService(),
			Substitute.For<IImageDownloadService>(), Substitute.For<IImageResizer>(), new HashtagService(),
			NullLogger<SocialPostService>.Instance);
		return new ScheduledSocialPostProcessor(_repository, service, _images, Options.Create(new ScheduledSocialPostOptions()),
			NullLogger<ScheduledSocialPostProcessor>.Instance);
	}

	private static ISocialPlatformClient Client(string name)
	{
		var client = Substitute.For<ISocialPlatformClient>();
		client.PlatformName.Returns(name);
		client.GetConfigurationAsync(Arg.Any<CancellationToken>()).Returns(new PlatformConfiguration { Name = name, MaxCharacters = 10 });
		client.PostAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<UploadedImage>>(), Arg.Any<CancellationToken>()).Returns(Result(name, true));
		client.PostThreadAsync(Arg.Any<IReadOnlyList<ThreadSegmentPost>>(), Arg.Any<CancellationToken>())
			.Returns(call => call.Arg<IReadOnlyList<ThreadSegmentPost>>()!.Select(_ => Result(name, true)).ToArray());
		return client;
	}

	private static PlatformPostResult Result(string platform, bool success, string? error = null)
	{
		return new PlatformPostResult { Platform = platform, Success = success, ErrorCode = error, PostId = success ? "post-id" : null };
	}

	private static ScheduledSocialPostRecord Record(string id = "post", IReadOnlyList<StoredImageData>? images = null, bool autoThread = false)
	{
		return new ScheduledSocialPostRecord
		{
			ScheduledPostId = id,
			ScheduledForUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
			Status = ScheduledPostStatus.Pending,
			Text = "a long post that requires multiple segments",
			CreatedAtUtc = DateTimeOffset.UtcNow.AddHours(-1),
			TargetPlatforms = ["bluesky", "mastodon"],
			UploadedImages = images ?? [],
			AutoThread = autoThread,
			DeliveryTrackingEnabled = true
		};
	}
}

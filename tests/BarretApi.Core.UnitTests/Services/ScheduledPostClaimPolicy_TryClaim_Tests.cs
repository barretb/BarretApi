using BarretApi.Core.Models;
using BarretApi.Core.Services;
using Shouldly;

namespace BarretApi.Core.UnitTests.Services;

public sealed class ScheduledPostClaimPolicy_TryClaim_Tests
{
	[Theory]
	[InlineData(ScheduledPostStatus.Pending, true, true, ScheduledPostStatus.Processing)]
	[InlineData(ScheduledPostStatus.Failed, true, true, ScheduledPostStatus.Processing)]
	[InlineData(ScheduledPostStatus.Failed, false, true, ScheduledPostStatus.NeedsReview)]
	[InlineData(ScheduledPostStatus.Processing, true, false, ScheduledPostStatus.Processing)]
	[InlineData(ScheduledPostStatus.Published, true, false, ScheduledPostStatus.Published)]
	[InlineData(ScheduledPostStatus.NeedsReview, true, false, ScheduledPostStatus.NeedsReview)]
	public void RespectsDeliveryState_GivenExistingRecord(ScheduledPostStatus status, bool tracked, bool expectedClaim, ScheduledPostStatus expectedStatus)
	{
		var now = DateTimeOffset.UtcNow;
		var record = new ScheduledSocialPostRecord
		{
			ScheduledPostId = "post",
			ScheduledForUtc = now.AddMinutes(-1),
			Text = "text",
			CreatedAtUtc = now.AddHours(-1),
			Status = status,
			DeliveryTrackingEnabled = tracked,
			LeaseExpiresAtUtc = now.AddMinutes(1)
		};

		ScheduledPostClaimPolicy.TryClaim(record, now).ShouldBe(expectedClaim);
		record.Status.ShouldBe(expectedStatus);
	}

	[Fact]
	public void FlagsReview_GivenLegacyProcessingRecordWithoutLease()
	{
		var now = DateTimeOffset.UtcNow;
		var record = new ScheduledSocialPostRecord
		{
			ScheduledPostId = "post",
			ScheduledForUtc = now.AddHours(-1),
			Text = "text",
			CreatedAtUtc = now.AddHours(-2),
			Status = ScheduledPostStatus.Processing,
			LastAttemptedAtUtc = now.AddHours(-1)
		};

		ScheduledPostClaimPolicy.TryClaim(record, now).ShouldBeTrue();
		record.Status.ShouldBe(ScheduledPostStatus.NeedsReview);
		record.LastErrorCode.ShouldBe("DELIVERY_UNCERTAIN");
	}
}

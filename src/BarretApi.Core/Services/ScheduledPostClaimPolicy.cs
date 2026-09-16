using BarretApi.Core.Models;

namespace BarretApi.Core.Services;

public static class ScheduledPostClaimPolicy
{
	public static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(15);

	public static bool IsDue(ScheduledSocialPostRecord record, DateTimeOffset now)
	{
		return record.ScheduledForUtc <= now
			&& (record.Status is ScheduledPostStatus.Pending or ScheduledPostStatus.Failed
				|| (record.Status == ScheduledPostStatus.Processing
					&& (record.LeaseExpiresAtUtc ?? record.LastAttemptedAtUtc?.Add(LeaseDuration) ?? DateTimeOffset.MinValue) <= now));
	}

	public static bool TryClaim(ScheduledSocialPostRecord record, DateTimeOffset now)
	{
		if (!IsDue(record, now))
		{
			return false;
		}

		// A crash or a legacy failure may have occurred after the provider accepted a post.
		// Without a durable receipt, automatically replaying that operation is unsafe.
		if (record.Status == ScheduledPostStatus.Processing
			|| (record.Status == ScheduledPostStatus.Failed && !record.DeliveryTrackingEnabled))
		{
			record.Status = ScheduledPostStatus.NeedsReview;
			record.LastErrorCode = "DELIVERY_UNCERTAIN";
			record.LastErrorMessage = "Check provider posts before manually retrying; a prior delivery may have succeeded.";
			record.LeaseExpiresAtUtc = null;
			return true;
		}

		record.Status = ScheduledPostStatus.Processing;
		record.LastAttemptedAtUtc = now;
		record.LeaseExpiresAtUtc = now + LeaseDuration;
		record.AttemptCount++;
		record.DeliveryTrackingEnabled = true;
		return true;
	}
}

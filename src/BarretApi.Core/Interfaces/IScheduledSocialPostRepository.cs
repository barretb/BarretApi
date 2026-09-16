using BarretApi.Core.Models;

namespace BarretApi.Core.Interfaces;

public interface IScheduledSocialPostRepository
{
	Task SaveScheduledAsync(
		ScheduledSocialPostRecord record,
		CancellationToken cancellationToken = default);

	Task<IReadOnlyList<ScheduledSocialPostRecord>> GetDueForProcessingAsync(
		DateTimeOffset asOfUtc,
		int maxCount,
		CancellationToken cancellationToken = default);

	// Returns the latest claimed snapshot, including its optimistic concurrency version.
	Task<ScheduledSocialPostRecord?> TryClaimAsync(
		string scheduledPostId,
		DateTimeOffset attemptedAtUtc,
		CancellationToken cancellationToken = default);

	Task SaveClaimAsync(
		ScheduledSocialPostRecord record,
		CancellationToken cancellationToken = default);
}

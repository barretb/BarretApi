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

    Task<bool> TryMarkProcessingAsync(
        string scheduledPostId,
        DateTimeOffset attemptedAtUtc,
        CancellationToken cancellationToken = default);

    Task MarkPublishedAsync(
        string scheduledPostId,
        DateTimeOffset publishedAtUtc,
        CancellationToken cancellationToken = default);

    Task MarkFailedAsync(
        string scheduledPostId,
        string errorCode,
        string errorMessage,
        DateTimeOffset attemptedAtUtc,
        CancellationToken cancellationToken = default);
}

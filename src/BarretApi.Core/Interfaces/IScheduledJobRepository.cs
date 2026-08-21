using BarretApi.Core.Models;

namespace BarretApi.Core.Interfaces;

public interface IScheduledJobRepository
{
    Task<IReadOnlyList<ScheduledJobRecord>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<ScheduledJobRecord?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enabled jobs whose <see cref="ScheduledJobRecord.NextRunUtc"/> is at or before
    /// <paramref name="nowUtc"/>. Jobs already marked running are included so the caller
    /// can decide whether the claim is stale.
    /// </summary>
    Task<IReadOnlyList<ScheduledJobRecord>> GetDueAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task CreateAsync(ScheduledJobRecord job, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unconditional update. Refreshes <see cref="ScheduledJobRecord.ETag"/> in place.
    /// </summary>
    Task UpdateAsync(ScheduledJobRecord job, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the job running, conditional on its current ETag. Returns false when another
    /// writer won the race. On success the record's RunState, ClaimedAtUtc and ETag are updated in place.
    /// </summary>
    Task<bool> TryClaimAsync(
        ScheduledJobRecord job,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(string name, CancellationToken cancellationToken = default);
}

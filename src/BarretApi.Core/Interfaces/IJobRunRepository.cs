using BarretApi.Core.Models;

namespace BarretApi.Core.Interfaces;

public interface IJobRunRepository
{
    Task AddAsync(JobRunRecord run, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs for one job, newest first, capped at <paramref name="maxCount"/>.
    /// </summary>
    Task<IReadOnlyList<JobRunRecord>> GetByJobAsync(
        string jobName,
        int maxCount,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes runs that started before <paramref name="cutoffUtc"/>. Returns the number deleted.
    /// </summary>
    Task<int> PurgeOlderThanAsync(
        DateTimeOffset cutoffUtc,
        CancellationToken cancellationToken = default);
}

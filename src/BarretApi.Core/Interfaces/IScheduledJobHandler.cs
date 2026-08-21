using BarretApi.Core.Models;

namespace BarretApi.Core.Interfaces;

/// <summary>
/// Performs the work for one job type. Implementations delegate to existing
/// services rather than duplicating their logic.
/// </summary>
public interface IScheduledJobHandler
{
    /// <summary>
    /// The stable key stored on a job definition, e.g. "tip-of-day". Matched case-insensitively.
    /// </summary>
    string JobType { get; }

    Task<JobExecutionResult> ExecuteAsync(
        JobExecutionContext context,
        CancellationToken cancellationToken = default);
}

using BarretApi.Core.Models;

namespace BarretApi.Core.Interfaces;

public interface IScheduledSocialPostProcessor
{
    Task<ScheduledPostProcessingSummary> ProcessDueAsync(
        int? maxCount,
        CancellationToken cancellationToken = default);
}

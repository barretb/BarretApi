using BarretApi.Core.Models;

namespace BarretApi.Core.Interfaces;

public interface IBlogPromotionOrchestrator
{
    Task<PromotionRunSummary> RunAsync(
        string? feedUrl = null,
        string? header = null,
        int? recentDaysWindow = null,
        CancellationToken cancellationToken = default);
}

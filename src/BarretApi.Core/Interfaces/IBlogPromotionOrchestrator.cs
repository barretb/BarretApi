using BarretApi.Core.Models;

namespace BarretApi.Core.Interfaces;

public interface IBlogPromotionOrchestrator
{
	Task<PromotionRunSummary> RunAsync(
		CancellationToken cancellationToken = default);
}

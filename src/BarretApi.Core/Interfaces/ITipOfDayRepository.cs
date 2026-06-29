using BarretApi.Core.Models;

namespace BarretApi.Core.Interfaces;

public interface ITipOfDayRepository
{
	Task AddAsync(TipOfDayRecord record, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<TipOfDayRecord>> GetEligibleByCategoryAsync(
		string category,
		DateTimeOffset repostCutoffUtc,
		CancellationToken cancellationToken = default);

	Task MarkPostedAsync(
		string tipId,
		DateTimeOffset postedAtUtc,
		CancellationToken cancellationToken = default);
}

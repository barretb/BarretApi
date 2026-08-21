using BarretApi.Core.Models;

namespace BarretApi.Core.Interfaces;

public interface ITipOfDayService
{
    Task<TipOfDayPostResult> SelectAndPostAsync(
        TipOfDayPostCommand command,
        CancellationToken cancellationToken = default);
}

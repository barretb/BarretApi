using BarretApi.Core.Models;

namespace BarretApi.Core.Interfaces;

public interface IRssRandomPostService
{
    Task<RssRandomPostResult> SelectAndPostAsync(
        RssRandomPostQuery query,
        CancellationToken cancellationToken = default);
}

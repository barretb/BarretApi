using BarretApi.Core.Models;

namespace BarretApi.Core.Interfaces;

public interface IBlogFeedReader
{
    Task<IReadOnlyList<BlogFeedEntry>> ReadEntriesAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BlogFeedEntry>> ReadEntriesAsync(
        string feedUrl,
        CancellationToken cancellationToken = default);
}

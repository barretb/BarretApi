using BarretApi.Core.Models;

namespace BarretApi.Core.Interfaces;

public interface IBlogPostPromotionRepository
{
    Task<BlogPostPromotionRecord?> GetByEntryIdentityAsync(
        string entryIdentity,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BlogPostPromotionRecord>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task UpsertAsync(
        BlogPostPromotionRecord record,
        CancellationToken cancellationToken = default);
}

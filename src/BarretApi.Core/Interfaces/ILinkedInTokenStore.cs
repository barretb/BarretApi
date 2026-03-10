using BarretApi.Core.Models;

namespace BarretApi.Core.Interfaces;

public interface ILinkedInTokenStore
{
    Task<LinkedInTokenRecord?> GetTokensAsync(CancellationToken cancellationToken = default);
    Task SaveTokensAsync(LinkedInTokenRecord tokens, CancellationToken cancellationToken = default);
}

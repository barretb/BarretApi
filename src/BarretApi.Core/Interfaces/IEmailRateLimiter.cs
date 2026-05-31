namespace BarretApi.Core.Interfaces;

public interface IEmailRateLimiter
{
    Task<bool> CanSendEmailAsync(string postType, CancellationToken cancellationToken = default);
    Task RecordEmailSentAsync(string postType, CancellationToken cancellationToken = default);
}

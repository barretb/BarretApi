namespace BarretApi.Core.Interfaces;

public interface IEmailNotificationService
{
    Task SendPostFailureNotificationAsync(
        string postType,
        string errorDetails,
        IDictionary<string, string>? additionalContext = null,
        CancellationToken cancellationToken = default);
}

using System.Net;
using System.Net.Mail;
using System.Text;
using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BarretApi.Infrastructure.Services;

public sealed class SmtpEmailNotificationService(
    IOptions<EmailOptions> options,
    IEmailRateLimiter emailRateLimiter,
    ILogger<SmtpEmailNotificationService> logger) : IEmailNotificationService
{
    private readonly EmailOptions _options = options.Value;
    private readonly IEmailRateLimiter _emailRateLimiter = emailRateLimiter;
    private readonly ILogger<SmtpEmailNotificationService> _logger = logger;

    public async Task SendPostFailureNotificationAsync(
        string postType,
        string errorDetails,
        IDictionary<string, string>? additionalContext = null,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogDebug("Email notifications are disabled, skipping notification for {PostType} failure", postType);
            return;
        }

        try
        {
            var subject = $"BarretApi: {postType} Post Failure";
            var body = BuildEmailBody(postType, errorDetails, additionalContext);

            using var smtpClient = new SmtpClient(_options.SmtpHost, _options.SmtpPort)
            {
                EnableSsl = _options.UseSsl,
                Credentials = new NetworkCredential(_options.Username, _options.Password)
            };

            using var message = new MailMessage
            {
                From = new MailAddress(_options.FromAddress, _options.FromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = false
            };
            message.To.Add(_options.ToAddress);

            await smtpClient.SendMailAsync(message, cancellationToken);

            _logger.LogInformation(
                "Failure notification email sent successfully for {PostType} to {ToAddress}",
                postType,
                _options.ToAddress);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send email notification for {PostType} failure",
                postType);
        }
    }

    private static string BuildEmailBody(
        string postType,
        string errorDetails,
        IDictionary<string, string>? additionalContext)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"A {postType} post has failed.");
        sb.AppendLine();
        sb.AppendLine("Error Details:");
        sb.AppendLine(errorDetails);
        sb.AppendLine();

        if (additionalContext is not null && additionalContext.Count > 0)
        {
            sb.AppendLine("Additional Context:");
            foreach (var (key, value) in additionalContext)
            {
                sb.AppendLine($"  {key}: {value}");
            }
            sb.AppendLine();
        }

        sb.AppendLine($"Timestamp: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");

        return sb.ToString();
    }
}

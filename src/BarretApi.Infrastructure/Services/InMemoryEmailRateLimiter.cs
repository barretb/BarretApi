using System.Collections.Concurrent;
using BarretApi.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace BarretApi.Infrastructure.Services;

public sealed class InMemoryEmailRateLimiter(
    ILogger<InMemoryEmailRateLimiter> logger) : IEmailRateLimiter
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastEmailSent = new();
    private readonly ILogger<InMemoryEmailRateLimiter> _logger = logger;

    public Task<bool> CanSendEmailAsync(string postType, CancellationToken cancellationToken = default)
    {
        if (!_lastEmailSent.TryGetValue(postType, out var lastSent))
        {
            return Task.FromResult(true);
        }

        var now = DateTimeOffset.UtcNow;
        var daysSinceLastEmail = (now - lastSent).TotalDays;

        if (daysSinceLastEmail >= 1.0)
        {
            _logger.LogDebug(
                "Email rate limit check passed for {PostType}: {DaysSince:F2} days since last email",
                postType,
                daysSinceLastEmail);
            return Task.FromResult(true);
        }

        _logger.LogInformation(
            "Email rate limit reached for {PostType}: Last email sent {LastSent}, {HoursRemaining:F1} hours until next email allowed",
            postType,
            lastSent,
            24 - (daysSinceLastEmail * 24));

        return Task.FromResult(false);
    }

    public Task RecordEmailSentAsync(string postType, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        _lastEmailSent[postType] = now;

        _logger.LogDebug(
            "Recorded email sent for {PostType} at {Timestamp}",
            postType,
            now);

        return Task.CompletedTask;
    }
}

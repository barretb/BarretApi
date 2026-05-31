# Email Rate Limiting Implementation - Change Summary

## Overview
Updated the email notification system to enforce a rate limit of **1 email per post type per 24 hours** to prevent email flooding when multiple failures occur.

## Files Created

### Core Interfaces
1. **`src\BarretApi.Core\Interfaces\IEmailRateLimiter.cs`**
   - Interface for checking and recording email sends
   - Methods: `CanSendEmailAsync()`, `RecordEmailSentAsync()`

### Infrastructure Implementations
2. **`src\BarretApi.Infrastructure\Services\InMemoryEmailRateLimiter.cs`**
   - Thread-safe in-memory rate limiter using `ConcurrentDictionary`
   - Suitable for development/local testing
   - Resets on application restart

3. **`src\BarretApi.Infrastructure\Services\AzureTableEmailRateLimiter.cs`**
   - Persistent rate limiter using Azure Table Storage
   - Suitable for production (distributed/multi-instance)
   - Stores data in `EmailRateLimit` table
   - Includes error handling (fail-safe: allows email on error)

### Documentation
4. **`docs\EMAIL_RATE_LIMITING.md`**
   - Technical reference for rate limiting
   - Storage backend details
   - Testing and troubleshooting guide

## Files Modified

### Email Service
1. **`src\BarretApi.Infrastructure\Services\SmtpEmailNotificationService.cs`**
   - Added `IEmailRateLimiter` dependency
   - Checks rate limit before sending emails
   - Records successful sends
   - Logs when emails are skipped due to rate limiting

### Service Registration
2. **`src\BarretApi.Api\Program.cs`**
   - Added conditional registration of `IEmailRateLimiter`
   - Uses `AzureTableEmailRateLimiter` when Azure Storage is configured
   - Falls back to `InMemoryEmailRateLimiter` otherwise

### Documentation Updates
3. **`docs\EMAIL_NOTIFICATIONS.md`**
   - Added rate limiting overview in introduction
   - Added "Rate Limiting" section with implementation details
   - Updated "Logs" section with rate limiting examples
   - Updated "Architecture" section to include rate limiter components

## Key Features

### Rate Limiting Logic
- **Limit**: 1 email per post type per 24-hour rolling window
- **Scope**: Each post type tracked independently:
  - `NASA APOD`
  - `NASA GIBS Satellite`
  - `RSS Blog Post`
  - `Scheduled Social Post`
  - `Blog Promotion`

### Storage Backends
- **Production**: Azure Table Storage (persistent, distributed-safe)
- **Development**: In-memory (simple, no dependencies)
- **Auto-detection**: Based on Azure Storage configuration

### Error Handling
- Rate limiter errors are logged but don't prevent emails (fail-safe)
- If `CanSendEmailAsync()` throws, email is allowed
- Ensures critical failure notifications aren't lost

### Logging
- **Debug**: Rate limit checks passed with time since last email
- **Information**: Rate limit reached with time until next allowed
- **Information**: Email skipped due to rate limit
- **Debug**: Email sent recorded with timestamp

## Technical Implementation

### Rate Limit Check Flow
```
1. Check if email enabled → No? Skip
2. Check rate limiter → Can send? → No? Skip (log)
3. Send email via SMTP
4. Record email sent in rate limiter
5. Log success
```

### Azure Table Storage Schema
```
Table: EmailRateLimit
PartitionKey: PostTypeNotifications
RowKey: <sanitized_post_type> (e.g., NASA_APOD)
Fields:
  - PostType: string (original name)
  - LastEmailSentUtc: DateTimeOffset
  - EmailCount: int
  - Timestamp: DateTimeOffset (auto)
  - ETag: ETag (auto)
```

### In-Memory Storage
```
ConcurrentDictionary<string, DateTimeOffset>
Key: post type (e.g., "NASA APOD")
Value: last email sent timestamp
```

## Testing Considerations

### Manual Testing
1. Enable email notifications
2. Trigger first failure → Email sent
3. Trigger second failure (same type, < 24h) → Email skipped
4. Wait 24+ hours or reset rate limit
5. Trigger third failure → Email sent

### Rate Limit Reset
- **Azure Storage**: Delete row from `EmailRateLimit` table
- **In-Memory**: Restart application

### Verification
- Check application logs for rate limit messages
- Inspect Azure Table Storage for `EmailRateLimit` entries
- Verify email inbox (should see only one per 24h period)

## Backward Compatibility

✅ **Fully backward compatible**
- Existing deployments work without changes
- Rate limiter automatically selected based on configuration
- No breaking changes to existing APIs or configurations
- Optional dependency injection maintains existing behavior

## Performance Impact

- **Minimal**: Single table lookup per email notification attempt
- **Azure Storage**: ~1-2ms query latency
- **In-Memory**: Sub-millisecond lookup
- **Network**: No impact (rate limit check is local/Azure)

## Security Considerations

- Rate limiter prevents email flooding from repeated failures
- No PII stored in rate limit data (only post type and timestamps)
- Azure Storage access controlled by existing credentials
- Fail-safe design prevents missing critical alerts

## Deployment Notes

### Azure Table Storage (Recommended for Production)
- Table created automatically on first use
- Uses same credentials as other Azure Table services
- No additional configuration needed if Azure Storage already configured

### In-Memory (Development/Testing)
- Automatically used when Azure Storage not configured
- No persistence (resets on restart)
- Suitable for local development only

## Summary

This update adds intelligent rate limiting to the email notification system, preventing email flooding while maintaining visibility into failures. The implementation is:

- ✅ Automatic (no configuration needed)
- ✅ Reliable (fail-safe on errors)
- ✅ Scalable (distributed-safe with Azure Storage)
- ✅ Backward compatible (works with existing deployments)
- ✅ Well-documented (comprehensive guides and logs)

The rate limiting ensures administrators receive timely failure notifications without being overwhelmed by repeated alerts for the same issue.

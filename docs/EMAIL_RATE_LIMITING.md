# Email Rate Limiting Implementation

## Quick Reference

The email notification system automatically limits notifications to **1 per post type per 24 hours**.

## Post Types Tracked

Each of the following is tracked independently:
- `NASA APOD` - NASA Astronomy Picture of the Day posts
- `NASA GIBS Satellite` - NASA GIBS satellite image posts
- `RSS Blog Post` - Random blog post promotions
- `Scheduled Social Post` - Scheduled posts
- `Blog Promotion` - Blog promotion campaign runs

## Storage Backends

### Azure Table Storage (Production)
Automatically used when Azure Table Storage is configured. Creates an `EmailRateLimit` table.

**Table Schema:**
- **PartitionKey**: `PostTypeNotifications`
- **RowKey**: Sanitized post type name (e.g., `NASA_APOD`)
- **PostType**: Original post type name
- **LastEmailSentUtc**: DateTimeOffset of last sent email
- **EmailCount**: Total number of emails sent for this post type

### In-Memory (Development)
Falls back to in-memory storage when Azure Table Storage is not available.

**Note**: In-memory storage is reset on application restart.

## Example Logs

### Rate Limit Allows Email
```
[DBG] Email rate limit check passed for NASA APOD: 1.25 days since last email
[INF] Failure notification email sent successfully for NASA APOD to admin@example.com
[DBG] Recorded email sent for NASA APOD at 2026-03-09 14:35:22 +00:00
```

### Rate Limit Blocks Email
```
[INF] Email rate limit reached for NASA APOD: Last email sent 2026-03-08 10:00:00, 14.5 hours until next email allowed
[INF] Skipping email notification for NASA APOD due to rate limit (max 1 per day)
```

## Manual Testing

To test rate limiting:

1. **Trigger a failure** - Cause a post to fail (e.g., invalid platform credentials)
2. **Check email** - Verify email was sent
3. **Trigger another failure** - Within 24 hours, cause the same post type to fail again
4. **Check logs** - Should see "Skipping email notification... due to rate limit"
5. **Wait 24+ hours** - Next failure should send email again

## Resetting Rate Limits

### Azure Table Storage
Delete the specific row from the `EmailRateLimit` table:
```bash
# Using Azure Storage Explorer or Azure CLI
az storage entity delete --table-name EmailRateLimit \
  --partition-key PostTypeNotifications \
  --row-key NASA_APOD \
  --account-name <storage-account>
```

### In-Memory
Restart the application.

## Configuration

No additional configuration needed. Rate limiting is always active when email notifications are enabled.

The rate limiter is automatically registered in `Program.cs`:
- Uses Azure Table Storage if available
- Falls back to in-memory storage otherwise

## Error Handling

If the rate limiter encounters an error checking limits:
- It **allows** the email to be sent (fail-safe)
- Logs the error
- Example: `[ERR] Error checking email rate limit for NASA APOD, allowing email`

This ensures that temporary rate limiter issues don't prevent important failure notifications.

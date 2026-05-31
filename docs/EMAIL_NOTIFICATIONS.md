# Email Notification for Social Media Post Failures

## Overview

The BarretApi now supports email notifications when social media post processes fail. When enabled, the system will automatically send detailed email alerts about failures in:

- **NASA APOD Posts** - NASA Astronomy Picture of the Day posts
- **NASA GIBS Posts** - NASA GIBS satellite image posts
- **RSS Blog Posts** - Random blog post promotions from RSS feeds
- **Scheduled Posts** - Posts scheduled to be published at specific times
- **Blog Promotion Runs** - Automated blog promotion campaigns

**Rate Limiting**: To prevent email flooding, the system enforces a rate limit of **1 email per post type per 24 hours**. If multiple failures occur for the same post type within a day, only the first failure will trigger an email notification.

## Configuration

Add the following section to your `appsettings.json` or user secrets:

```json
{
  "Email": {
    "Enabled": true,
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": 587,
    "UseSsl": true,
    "Username": "your-email@gmail.com",
    "Password": "your-app-password",
    "FromAddress": "notifications@yourdomain.com",
    "FromName": "BarretApi Notifications",
    "ToAddress": "admin@yourdomain.com"
  }
}
```

### Configuration Options

| Property | Required | Default | Description |
|----------|----------|---------|-------------|
| `Enabled` | No | `false` | Set to `true` to enable email notifications |
| `SmtpHost` | Yes | - | SMTP server hostname (e.g., smtp.gmail.com) |
| `SmtpPort` | No | `587` | SMTP server port (typically 587 for TLS, 465 for SSL) |
| `UseSsl` | No | `true` | Enable SSL/TLS encryption |
| `Username` | Yes | - | SMTP authentication username |
| `Password` | Yes | - | SMTP authentication password or app password |
| `FromAddress` | Yes | - | Email address to send notifications from |
| `FromName` | Yes | - | Display name for the sender |
| `ToAddress` | Yes | - | Email address to receive notifications |

## Email Content

Each failure notification email includes:

### Header
- Post type (e.g., "NASA APOD", "RSS Blog Post")
- Timestamp of the failure

### Error Details
For each failed platform:
- Platform name (e.g., Bluesky, Mastodon, LinkedIn)
- Error code
- Error message

### Additional Context
Varies by post type but typically includes:
- Content title or description
- Date/time of the post
- Number of platforms targeted
- Number of successful vs. failed platforms
- Relevant identifiers (post IDs, scheduled post IDs, etc.)

## Example Email

```
Subject: BarretApi: NASA APOD Post Failure

A NASA APOD post has failed.

Error Details:
Platform: Bluesky
Error Code: UPLOAD_FAILED
Error Message: Failed to upload image to Bluesky CDN

Platform: Mastodon
Error Code: RATE_LIMIT_EXCEEDED
Error Message: Rate limit exceeded, try again in 15 minutes

Additional Context:
  APOD Title: The Horsehead Nebula
  APOD Date: 2026-03-08
  Media Type: Image
  Total Platforms: 3
  Failed Platforms: 2
  Successful Platforms: 1

Timestamp: 2026-03-08 14:35:22 UTC
```

## Gmail Configuration

If using Gmail, you'll need to:

1. Enable 2-factor authentication on your Google account
2. Generate an "App Password" at https://myaccount.google.com/apppasswords
3. Use the app password in the `Password` field

Example Gmail configuration:
```json
{
  "Email": {
    "Enabled": true,
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": 587,
    "UseSsl": true,
    "Username": "yourname@gmail.com",
    "Password": "abcd efgh ijkl mnop",
    "FromAddress": "yourname@gmail.com",
    "FromName": "BarretApi Notifications",
    "ToAddress": "admin@yourdomain.com"
  }
}
```

## Disabling Notifications

To disable email notifications, set `Enabled` to `false` or remove the Email configuration section entirely. The services will continue to function normally without sending emails.

## Testing

To test your email configuration, trigger a social media post process and intentionally cause a failure (e.g., by providing invalid credentials for a platform). Check your configured email address for the notification.

## Troubleshooting

### Emails Not Being Sent

1. Check that `Email:Enabled` is set to `true`
2. Verify SMTP credentials are correct
3. Check application logs for email sending errors
4. Ensure firewall allows outbound connections on the SMTP port
5. Verify your email provider allows SMTP access

### Authentication Failures

- Gmail: Use an app password, not your regular password
- Office 365: May require OAuth2 authentication (not currently supported)
- Self-hosted: Verify SMTP server settings and authentication requirements

### Logs

Email sending errors are logged at the Error level. Successful sends are logged at the Information level. Check your application logs for details:

```
[INF] Failure notification email sent successfully for NASA APOD to admin@example.com
[ERR] Failed to send email notification for NASA APOD failure
```

## Architecture

The email notification system is implemented as:

- **Interface**: `IEmailNotificationService` in `BarretApi.Core.Interfaces`
- **Configuration**: `EmailOptions` in `BarretApi.Core.Configuration`
- **Implementation**: `SmtpEmailNotificationService` in `BarretApi.Infrastructure.Services`

Each social media post service receives an optional `IEmailNotificationService` dependency and sends notifications only when failures occur and the service is configured.

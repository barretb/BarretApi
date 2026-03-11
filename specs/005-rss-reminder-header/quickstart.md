# Quickstart: RSS Reminder Post Header Update

**Feature**: `005-rss-reminder-header`
**Date**: 2026-03-11

## What Changed

Reminder posts from the RSS blog promotion endpoint (`POST /api/social-posts/rss-promotion`) now begin with a header on its own line:

```
In case you missed it earlier...

My Blog Post Title
https://example.com/my-blog-post
```

Previously, the format was:

```
Did you miss it earlier? My Blog Post Title
https://example.com/my-blog-post
```

## How to Verify

1. Start the application via Aspire AppHost.
2. Ensure at least one blog entry has been initially promoted (has a tracked record with a successful initial post).
3. Ensure reminder posting is enabled (`BlogPromotion:EnableReminderPosts = true`) and the reminder delay has elapsed.
4. Call `POST /api/social-posts/rss-promotion` with a valid `X-Api-Key`.
5. Verify the reminder post text on the social platform begins with "In case you missed it earlier..." on its own line, followed by a blank line, then the entry title and URL.

## No Configuration Changes

No new configuration parameters. The header text is a fixed string.

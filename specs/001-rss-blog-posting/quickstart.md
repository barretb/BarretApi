# Quickstart: RSS Blog Post Promotion

## 1. Configure required settings

Set these configuration values in the AppHost-managed configuration source:

- `BlogPromotion:FeedUrl`
- `BlogPromotion:RecentDaysWindow`
- `BlogPromotion:EnableReminderPosts`
- `BlogPromotion:ReminderDelayHours`
- `BlogPromotion:TableStorage:AccountEndpoint`
- `BlogPromotion:TableStorage:TableName`

## 2. Provision storage and permissions

- Create the Azure Table Storage table specified by `TableName`.
- Grant the application identity data-plane permissions to read/write table entities.
- Confirm identity-based authentication is used in hosted environments.

## 3. Start the API

Run the API through the solution’s normal local run workflow.

## 4. Trigger a promotion run

Call the endpoint:

- `POST /api/social-posts/rss-promotion`
- Include header `X-Api-Key: <api-key>`

Example:

```bash
curl -X POST "https://<host>/api/social-posts/rss-promotion" \
  -H "X-Api-Key: <api-key>"
```

Example response:

```json
{
  "runId": "20260301-7f0f8455",
  "startedAtUtc": "2026-03-01T18:00:00Z",
  "completedAtUtc": "2026-03-01T18:00:04Z",
  "entriesEvaluated": 8,
  "newPostsAttempted": 2,
  "newPostsSucceeded": 2,
  "reminderPostsAttempted": 3,
  "reminderPostsSucceeded": 2,
  "entriesSkippedAlreadyPosted": 2,
  "entriesSkippedOutsideWindow": 1,
  "failures": [
    {
      "entryIdentity": "https://blog.example.com/posts/my-post",
      "canonicalUrl": "https://blog.example.com/posts/my-post",
      "phase": "Reminder",
      "platform": "mastodon",
      "errorCode": "PLATFORM_ERROR",
      "errorMessage": "Timeout"
    }
  ]
}
```

## 5. Verify run behavior

Validate that the response summary reports:

- Total entries evaluated
- New posts attempted/succeeded
- Reminder posts attempted/succeeded
- Skipped counts
- Failures with per-entry details

## 6. Validate idempotency and reminder timing

- Re-run immediately: no duplicate initial posts should be created.
- If reminder is enabled, run again after the configured delay: eligible entries should receive exactly one reminder prefixed with `Did you miss it earlier?`.
- Additional runs after reminder success should not create duplicate reminders.

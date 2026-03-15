# API Contract: RSS Random Post Endpoint

**Feature**: 006-atom-feed-support  
**Date**: 2026-03-14  
**Endpoint**: `POST /api/social-posts/rss-random`

## Request

```json
{
  "feedUrl": "https://example.com/feed.xml",
  "platforms": ["bluesky", "mastodon"],
  "excludeTags": ["personal"],
  "maxAgeDays": 30,
  "header": "Check out this post!"
}
```

| Field | Type | Required | Change | Description |
|-------|------|----------|--------|-------------|
| feedUrl | string | Yes | No change | Absolute HTTP/HTTPS URL to an RSS or Atom feed |
| platforms | string[] | No | No change | Target platforms (omit for all configured) |
| excludeTags | string[] | No | No change | Tags to exclude (case-insensitive) |
| maxAgeDays | int | No | No change | Only entries published within this many days |
| header | string | No | **NEW** | Optional text prepended to post body with trailing newline |

### Header Behavior

- When `header` is populated: post body = `"{header}\n{title}\n{url}"`
- When `header` is absent/empty: post body = `"{title}\n{url}"` (unchanged)
- When reminder leader is also present: `"{reminder leader}\n\n{header}\n{title}\n{url}"`
- No separate length validation; platform limits apply via text shortening

## Response (UNCHANGED)

```json
{
  "selectedTitle": "Example Blog Post Title",
  "selectedUrl": "https://example.com/post",
  "results": [
    {
      "platform": "bluesky",
      "success": true,
      "postId": "abc123",
      "postUrl": "https://bsky.app/...",
      "shortenedText": "Check out this post!\nExample Blog Post Title\nhttps://example.com/post",
      "error": null,
      "errorCode": null
    }
  ],
  "postedAt": "2026-03-14T12:00:00Z"
}
```

## Status Codes (UNCHANGED)

| Code | Condition |
|------|-----------|
| 200 | All platform posts succeeded |
| 207 | At least one succeeded, at least one failed |
| 400 | Request validation failed |
| 401 | Missing/invalid API key |
| 422 | No eligible entries after filtering |
| 502 | Feed unreachable or all platform posts failed |

## Eligibility Change

Entries with no tags/categories are now eligible for selection. Previously, entries with `Tags.Count == 0` were filtered out. This applies to both this endpoint and the `/api/social-posts/rss-promotion` endpoint.

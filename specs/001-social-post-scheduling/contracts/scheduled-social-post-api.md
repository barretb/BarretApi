# API Contract: Scheduled Social Post Publishing

**Feature**: 001-social-post-scheduling  
**Date**: 2026-03-22

## Endpoints

```text
POST /api/social-posts
POST /api/social-posts/upload
POST /api/social-posts/scheduled/process
```

## 1) Create Social Post (JSON) with Optional Scheduling

### Request

**Method/Path**

```text
POST /api/social-posts
```

**Body (application/json)**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| text | string | Conditional | Required when no images are supplied |
| hashtags | array<string> | No | Optional hashtag list |
| platforms | array<string> | No | Optional target platforms |
| images | array<object> | No | URL-based image references |
| images[].url | string | Yes (when image provided) | Absolute HTTP/HTTPS URL |
| images[].altText | string | Yes (when image provided) | Accessibility text |
| scheduledFor | string (ISO 8601 datetime) | No | Future datetime for deferred publishing |

### Behavior

- If scheduledFor is omitted: existing immediate publish flow applies.
- If scheduledFor is in the future: post is persisted as scheduled and not published during create request.
- If scheduledFor is not in the future: validation fails with 400.

### Example Request

```json
{
  "text": "Ship update tonight",
  "hashtags": ["release", "dotnet"],
  "platforms": ["bluesky", "mastodon"],
  "scheduledFor": "2026-03-23T20:00:00Z"
}
```

### Example Success Response (scheduled accepted)

```json
{
  "scheduled": true,
  "scheduledPostId": "sp_01HZYD3M5Q9K6Q",
  "scheduledFor": "2026-03-23T20:00:00Z",
  "postedAt": null,
  "results": []
}
```

## 2) Create Social Post (Multipart Upload) with Optional Scheduling

### Request

**Method/Path**

```text
POST /api/social-posts/upload
```

**Form fields**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| text | string | Conditional | Required when no image files are supplied |
| hashtags | repeated string | No | Optional hashtag entries |
| platforms | repeated string | No | Optional target platforms |
| images | repeated file | No | Image uploads |
| altTexts | repeated string | Conditional | One alt text per image |
| scheduledFor | string (ISO 8601 datetime) | No | Future datetime for deferred publishing |

### Behavior

- Same scheduling rules as JSON endpoint.
- For scheduled uploads, file content and metadata are persisted for later publish.

## 3) Process Due Scheduled Posts

### Request

**Method/Path**

```text
POST /api/social-posts/scheduled/process
```

**Body (application/json)**

```json
{
  "maxCount": 100
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| maxCount | integer | No | Maximum number of due posts to process in one run. Range: 1-1000. |

### Processing Rules

- A record is due when scheduledFor <= current UTC time.
- Only due records in retryable/non-published states are attempted.
- Published records are not reposted on subsequent runs.
- Failures remain retryable and are reported in run summary.

### Success Response (200)

```json
{
  "runId": "sched-run-20260322-001",
  "startedAtUtc": "2026-03-22T18:00:00Z",
  "completedAtUtc": "2026-03-22T18:00:03Z",
  "dueCount": 3,
  "attemptedCount": 3,
  "succeededCount": 2,
  "failedCount": 1,
  "skippedCount": 0,
  "failures": [
    {
      "scheduledPostId": "sp_01HZYD3M5Q9K6Q",
      "scheduledForUtc": "2026-03-22T17:59:00Z",
      "platforms": ["bluesky", "mastodon"],
      "errorCode": "PLATFORM_ERROR",
      "errorMessage": "Mastodon API timeout",
      "attemptedAtUtc": "2026-03-22T18:00:02Z"
    }
  ]
}
```

## Error Responses

### 400 Bad Request

Validation failed (for example non-future scheduledFor).

```json
{
  "statusCode": 400,
  "message": "One or more validation errors occurred.",
  "errors": {
    "scheduledFor": ["scheduledFor must be in the future."]
  }
}
```

### 401 Unauthorized

Missing or invalid API key.

### 502 Bad Gateway

Processing attempted posts but all platform attempts failed or upstream dependencies were unavailable.

## Idempotency Contract

- A scheduled post that has already reached published state must not be published again by repeated processing calls.
- Processing endpoint responses are run-based summaries; counts can differ between runs as due set changes.

## Authentication

All endpoints continue to use API key authentication via the existing API scheme.

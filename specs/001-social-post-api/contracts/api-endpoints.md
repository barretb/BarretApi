# API Contracts: Social Media Post API

**Date**: 2026-02-28 | **Spec**: [spec.md](../spec.md) | **Data Model**: [data-model.md](../data-model.md)

---

## Overview

The Social Media Post API exposes two endpoints for creating cross-platform social media posts. Both endpoints share the same response format and orchestration logic but differ in how images are provided:

1. **JSON endpoint** — images referenced by URL
2. **Multipart endpoint** — images uploaded as binary files

Both endpoints require API key authentication via the `X-Api-Key` header.

---

## Authentication

All endpoints require the `X-Api-Key` header.

| Header | Required | Description |
|--------|----------|-------------|
| `X-Api-Key` | Yes | Pre-shared API key configured in Aspire AppHost |

**Failure response** (missing or invalid key):

```
HTTP/1.1 401 Unauthorized
Content-Type: application/json

{
  "statusCode": 401,
  "message": "Unauthorized"
}
```

---

## Endpoint 1: Create Social Post (JSON)

### Request

```
POST /api/social-posts
Content-Type: application/json
X-Api-Key: {api-key}
```

**Body**:

```json
{
  "text": "Check out this amazing sunset! #photography #nature",
  "hashtags": ["dotnet", "webapi"],
  "platforms": ["bluesky", "mastodon"],
  "images": [
    {
      "url": "https://example.com/sunset.jpg",
      "altText": "A golden sunset over the ocean with silhouetted palm trees"
    }
  ]
}
```

**Fields**:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `text` | string | Conditional | Post text. Required if `images` is empty or omitted. |
| `hashtags` | string[] | No | Additional hashtags to append. Auto-prefixed with `#`. |
| `platforms` | string[] | No | Target platforms. Values: `"bluesky"`, `"mastodon"`. Defaults to all configured. |
| `images` | object[] | No | Max 4. Each requires `url` and `altText`. |
| `images[].url` | string | Yes (per image) | Absolute HTTP(S) URL to the image. |
| `images[].altText` | string | Yes (per image) | Non-empty alt text for accessibility. Max 1,500 chars. |

---

## Endpoint 2: Create Social Post (Multipart)

### Request

```
POST /api/social-posts/upload
Content-Type: multipart/form-data
X-Api-Key: {api-key}
```

**Form fields**:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `text` | string | Conditional | Post text. Required if no images are attached. |
| `hashtags` | string (comma-separated or repeated field) | No | Additional hashtags to append. |
| `platforms` | string (comma-separated or repeated field) | No | Target platforms. Defaults to all configured. |
| `images` | file(s) | No | Max 4 image files. JPEG, PNG, GIF, WebP only. Max 1 MB each. |
| `altTexts` | string (repeated, one per image, in order) | Yes (per image) | Non-empty alt text. Max 1,500 chars. Must match image count. |

**Example** (curl):

```bash
curl -X POST https://localhost:5001/api/social-posts/upload \
  -H "X-Api-Key: my-secret-key" \
  -F "text=Check out this amazing sunset! #photography" \
  -F "hashtags=dotnet" \
  -F "hashtags=webapi" \
  -F "platforms=bluesky" \
  -F "platforms=mastodon" \
  -F "images=@sunset.jpg" \
  -F "altTexts=A golden sunset over the ocean with silhouetted palm trees"
```

---

## Response Format (Both Endpoints)

### Success — All Platforms (HTTP 200)

```json
{
  "results": [
    {
      "platform": "bluesky",
      "success": true,
      "postId": "at://did:plc:abc123/app.bsky.feed.post/xyz789",
      "postUrl": "https://bsky.app/profile/user.bsky.social/post/xyz789",
      "shortenedText": "Check out this amazing sunset! #photography #nature #dotnet #webapi",
      "error": null,
      "errorCode": null
    },
    {
      "platform": "mastodon",
      "success": true,
      "postId": "109876543210",
      "postUrl": "https://mastodon.social/@user/109876543210",
      "shortenedText": "Check out this amazing sunset! #photography #nature #dotnet #webapi",
      "error": null,
      "errorCode": null
    }
  ],
  "postedAt": "2026-02-28T14:30:00.000Z"
}
```

### Partial Success (HTTP 207 Multi-Status)

Returned when at least one platform succeeds and at least one fails.

```json
{
  "results": [
    {
      "platform": "bluesky",
      "success": true,
      "postId": "at://did:plc:abc123/app.bsky.feed.post/xyz789",
      "postUrl": "https://bsky.app/profile/user.bsky.social/post/xyz789",
      "shortenedText": "Check out this amazing sunset!…",
      "error": null,
      "errorCode": null
    },
    {
      "platform": "mastodon",
      "success": false,
      "postId": null,
      "postUrl": null,
      "shortenedText": null,
      "error": "Authentication failed: The access token is invalid",
      "errorCode": "AUTH_FAILED"
    }
  ],
  "postedAt": "2026-02-28T14:30:00.000Z"
}
```

### All Platforms Failed (HTTP 502 Bad Gateway)

Returned when all targeted platforms fail.

```json
{
  "results": [
    {
      "platform": "bluesky",
      "success": false,
      "postId": null,
      "postUrl": null,
      "shortenedText": null,
      "error": "Rate limit exceeded. Resets at 2026-02-28T15:00:00Z",
      "errorCode": "RATE_LIMITED"
    },
    {
      "platform": "mastodon",
      "success": false,
      "postId": null,
      "postUrl": null,
      "shortenedText": null,
      "error": "Service unavailable: Remote data could not be fetched",
      "errorCode": "PLATFORM_ERROR"
    }
  ],
  "postedAt": "2026-02-28T14:30:00.000Z"
}
```

### Validation Error (HTTP 400 Bad Request)

Returned when the request fails validation before contacting any platform.

```json
{
  "statusCode": 400,
  "message": "One or more validation errors occurred.",
  "errors": {
    "text": ["Text is required when no images are provided."],
    "images[0].altText": ["Alt text is required for every image."],
    "images": ["Maximum of 4 images allowed."]
  }
}
```

---

## Error Codes

Machine-readable error codes returned in `PlatformResult.errorCode`:

| Code | Description |
|------|-------------|
| `AUTH_FAILED` | Platform authentication failed (expired token, invalid credentials) |
| `RATE_LIMITED` | Platform rate limit exceeded |
| `VALIDATION_FAILED` | Platform rejected the content (e.g., text too long after shortening attempt) |
| `IMAGE_UPLOAD_FAILED` | Image upload to platform failed |
| `IMAGE_DOWNLOAD_FAILED` | URL-referenced image could not be downloaded |
| `PLATFORM_ERROR` | Transient platform error after all retries exhausted |
| `UNKNOWN_ERROR` | Unexpected error |

---

## HTTP Status Code Summary

| Status | Condition |
|--------|-----------|
| **200 OK** | All targeted platforms succeeded |
| **207 Multi-Status** | Mixed results (at least one success, at least one failure) |
| **400 Bad Request** | Request validation failed (before contacting platforms) |
| **401 Unauthorized** | Missing or invalid `X-Api-Key` header |
| **502 Bad Gateway** | All targeted platforms failed |

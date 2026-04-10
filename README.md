# BarretApi

A cross-platform social-media posting API built with .NET 10, Aspire, and FastEndpoints. Publish to **Bluesky**, **Mastodon**, and **LinkedIn** from a single request, with automatic threading for long posts and automated blog promotion from an RSS feed.

## Table of Contents

- [Project Structure](#project-structure)
- [Getting Started](#getting-started)
- [Authentication](#authentication)
- [API Endpoints](#api-endpoints)
  - [POST /api/social-posts — Create Social Post (JSON)](#post-apisocial-posts--create-social-post-json)
  - [POST /api/social-posts/upload — Create Social Post (Multipart Upload)](#post-apisocial-postsupload--create-social-post-multipart-upload)
  - [POST /api/social-posts/scheduled/process — Process Due Scheduled Posts](#post-apisocial-postsscheduledprocess--process-due-scheduled-posts)
  - [POST /api/social-posts/rss-promotion — Trigger RSS Blog Promotion](#post-apisocial-postsrss-promotion--trigger-rss-blog-promotion)
  - [POST /api/social-posts/rss-random — Post Random RSS Entry](#post-apisocial-postsrss-random--post-random-rss-entry)
  - [POST /api/social-posts/nasa-apod — Post NASA APOD to Social Platforms](#post-apisocial-postsnasa-apod--post-nasa-apod-to-social-platforms)
  - [POST /api/social-posts/satellite — Post Satellite Image](#post-apisocial-postssatellite--post-satellite-image)
  - [GET /api/linkedin/auth — Initiate LinkedIn OAuth Flow](#get-apilinkedinauth--initiate-linkedin-oauth-flow)
  - [GET /api/linkedin/auth/callback — LinkedIn OAuth Callback](#get-apilinkedinauthcallback--linkedin-oauth-callback)
  - [GET /api/linkedin/profile — Get LinkedIn Profile](#get-apilinkedinprofile--get-linkedin-profile)
  - [POST /api/word-cloud — Generate Word Cloud](#post-apiword-cloud--generate-word-cloud)
  - [GET /api/avatars/random — Generate Random Avatar](#get-apiavatarsrandom--generate-random-avatar)
  - [GET /api/github/auth — Initiate GitHub OAuth Flow](#get-apigithubauth--initiate-github-oauth-flow)
  - [GET /api/github/auth/callback — GitHub OAuth Callback](#get-apigithubauthcallback--github-oauth-callback)
  - [GET /api/github/profile — Get GitHub Profile](#get-apigithubprofile--get-github-profile)
  - [POST /api/github/repos/sync — Sync GitHub Repositories](#post-apigithubrepos-sync--sync-github-repositories)
  - [GET /api/github/repos — List GitHub Repositories](#get-apigithubrepos--list-github-repositories)
  - [GET /api/github/repos/{name} — Get Repository Details](#get-apigithubreposname--get-repository-details)
  - [POST /api/github/repos/{name}/issues — Create GitHub Issue](#post-apigithubreposnameissues--create-github-issue)
- [Configuration](#configuration)
- [Production Notes](#production-notes)

## Project Structure

```
src/
├── BarretApi.Api              # FastEndpoints API host
├── BarretApi.AppHost           # Aspire orchestration & configuration
├── BarretApi.Core              # Domain models, interfaces, services
├── BarretApi.Infrastructure    # Platform clients (Bluesky, Mastodon, LinkedIn)
└── BarretApi.ServiceDefaults   # Shared Aspire service defaults

tests/
├── BarretApi.Api.UnitTests
├── BarretApi.Core.UnitTests
└── BarretApi.Integration.Tests
```

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for Azurite table storage in development)

### Run

```bash
# Start with Aspire AppHost (recommended — provisions Azurite automatically)
dotnet run --project src/BarretApi.AppHost/BarretApi.AppHost.csproj

# Or run the API project directly (requires manual configuration)
dotnet run --project src/BarretApi.Api/BarretApi.Api.csproj
```

### Build & Test

```bash
dotnet build
dotnet test
```

Swagger UI is available in development at the root URL when running the API.

## Authentication

All mutating endpoints require an API key passed via the `X-Api-Key` header. The key is configured in the Aspire AppHost as the `Auth:ApiKey` parameter.

The LinkedIn OAuth endpoints (`/api/linkedin/auth`, `/api/linkedin/auth/callback`, `/api/linkedin/profile`) are **anonymous** — they do not require an API key.

The GitHub OAuth endpoints (`/api/github/auth`, `/api/github/auth/callback`, `/api/github/profile`) are also **anonymous** — they do not require an API key. All other GitHub endpoints require the `X-Api-Key` header.

## API Endpoints

---

### POST /api/social-posts — Create Social Post (JSON)

Creates a cross-platform social post. Images are supplied as URL references and downloaded server-side before publishing.

| Detail | Value |
|---|---|
| **Auth** | `X-Api-Key` header |
| **Content-Type** | `application/json` |

#### Request Body

| Field | Type | Required | Description |
|---|---|---|---|
| `text` | `string` | Yes (if no images) | Post body text (max 10,000 chars). |
| `hashtags` | `string[]` | No | Hashtags to append (no spaces, max 100 chars each). |
| `platforms` | `string[]` | No | Target platforms: `bluesky`, `mastodon`, `linkedin`. |
| `scheduledFor` | `string` (ISO 8601) | No | Future UTC datetime for deferred posting. When set, request is queued and not published immediately. |
| `autoThread` | `boolean` | No | When `true`, text exceeding the platform character limit is automatically split into a reply-chain thread. Defaults to `false`. |
| `images` | `object[]` | No | Up to 4 image references. |
| `images[].url` | `string` | Yes | Absolute URL of the image. |
| `images[].altText` | `string` | Yes | Alt text for the image (max 1,500 chars). |

#### Example — Post to All Platforms

```http
POST /api/social-posts
```

```json
{
  "text": "Hello from BarretApi! #dotnet #aspire",
  "hashtags": ["webapi"],
  "platforms": ["linkedin", "bluesky", "mastodon"],
  "images": [
    {
      "url": "https://example.com/photo.jpg",
      "altText": "A descriptive alt text for the image"
    }
  ]
}
```

#### Example — Post to a Single Platform

```http
POST /api/social-posts
```

```json
{
  "text": "Just shipped a new feature!",
  "platforms": ["bluesky"]
}
```

#### Example — Text-Only Post with Hashtags

```http
POST /api/social-posts
```

```json
{
  "text": "Exploring the new .NET 10 features today",
  "hashtags": ["dotnet", "csharp", "aspire"],
  "platforms": ["bluesky", "mastodon"]
}
```

#### Example — Auto-Thread a Long Post

```http
POST /api/social-posts
```

```json
{
  "text": "This is a very long post that exceeds the platform character limit. When autoThread is enabled, the text is automatically split into multiple segments and posted as a reply chain. Each segment breaks at paragraph or word boundaries for readability.",
  "platforms": ["bluesky", "mastodon"],
  "autoThread": true
}
```

#### Example — Schedule a Post for Later

```http
POST /api/social-posts
```

```json
{
  "text": "Launching the release announcement tomorrow morning.",
  "hashtags": ["release", "dotnet"],
  "platforms": ["linkedin", "bluesky"],
  "scheduledFor": "2026-03-23T14:30:00Z"
}
```

#### Response — 200 OK (Scheduled)

```json
{
  "results": [],
  "postedAt": null,
  "scheduled": true,
  "scheduledPostId": "sp_01HZYD3M5Q9K6Q",
  "scheduledFor": "2026-03-23T14:30:00+00:00"
}
```

#### Response — 200 OK (Threaded Post)

When `autoThread` is `true` and the text exceeds the platform limit, the response includes per-segment details:

```json
{
  "results": [
    {
      "platform": "bluesky",
      "success": true,
      "postId": "at://did:plc:abc123/app.bsky.feed.post/xyz789",
      "postUrl": "https://bsky.app/profile/handle/post/xyz789",
      "shortenedText": "First segment content",
      "threaded": true,
      "threadedPosts": [
        {
          "success": true,
          "postId": "at://did:plc:abc123/app.bsky.feed.post/xyz789",
          "postUrl": "https://bsky.app/profile/handle/post/xyz789",
          "publishedText": "First segment"
        },
        {
          "success": true,
          "postId": "at://did:plc:abc123/app.bsky.feed.post/abc456",
          "postUrl": "https://bsky.app/profile/handle/post/abc456",
          "publishedText": "Second segment"
        }
      ]
    }
  ],
  "postedAt": "2026-03-25T12:00:00+00:00"
}
```

#### Response — 200 OK (All Platforms Succeeded)

```json
{
  "results": [
    {
      "platform": "linkedin",
      "success": true,
      "postId": "urn:li:share:123456789",
      "postUrl": "https://www.linkedin.com/feed/update/urn%3Ali%3Ashare%3A123456789",
      "shortenedText": "Hello from BarretApi! #dotnet #aspire #webapi"
    },
    {
      "platform": "bluesky",
      "success": true,
      "postId": "at://did:plc:abc123/app.bsky.feed.post/xyz789",
      "postUrl": "https://bsky.app/profile/your-handle.bsky.social/post/xyz789",
      "shortenedText": "Hello from BarretApi! #dotnet #aspire #webapi"
    },
    {
      "platform": "mastodon",
      "success": true,
      "postId": "109876543210",
      "postUrl": "https://mastodon.social/@you/109876543210",
      "shortenedText": "Hello from BarretApi! #dotnet #aspire #webapi"
    }
  ],
  "postedAt": "2026-03-01T12:00:00+00:00"
}
```

#### Response — 207 Multi-Status (Partial Success)

Returned when at least one platform succeeded and at least one failed.

```json
{
  "results": [
    {
      "platform": "linkedin",
      "success": false,
      "error": "LinkedIn API rejected the content",
      "errorCode": "VALIDATION_FAILED"
    },
    {
      "platform": "bluesky",
      "success": true,
      "postId": "at://did:plc:abc123/app.bsky.feed.post/xyz789",
      "postUrl": "https://bsky.app/profile/your-handle.bsky.social/post/xyz789",
      "shortenedText": "Hello from BarretApi! #dotnet #aspire #webapi"
    }
  ],
  "postedAt": "2026-03-01T12:00:00+00:00"
}
```

#### Response — 502 Bad Gateway (All Platforms Failed)

```json
{
  "results": [
    {
      "platform": "linkedin",
      "success": false,
      "error": "Authentication failed",
      "errorCode": "AUTH_FAILED"
    },
    {
      "platform": "bluesky",
      "success": false,
      "error": "Rate limit exceeded",
      "errorCode": "RATE_LIMITED"
    }
  ],
  "postedAt": "2026-03-01T12:00:00+00:00"
}
```

#### Status Codes

| Code | Meaning |
|---|---|
| **200** | All targeted platforms succeeded. |
| **207** | Partial success — at least one platform succeeded and at least one failed. |
| **400** | Request validation failed. |
| **401** | Missing or invalid `X-Api-Key`. |
| **502** | All targeted platforms failed. |

#### Auto-Threading Behavior

When `autoThread` is `true`, text that exceeds a platform's character limit is split into multiple segments and posted as a reply chain. Text is split intelligently using grapheme cluster counting (correct for multi-byte Unicode) with the following break priority:

1. Paragraph boundaries (`\n\n`)
2. Line breaks (`\n`)
3. Word boundaries
4. Hard cut (last resort)

Images are attached only to the **first segment** of a thread. If any segment fails to post, subsequent segments are marked with error code `THREAD_BROKEN`.

| Platform | Thread Support | Character Limit | Chaining Mechanism |
|---|---|---|---|
| **Bluesky** | Yes | 300 grapheme clusters | Reply chain with root + parent references |
| **Mastodon** | Yes | Instance-dependent (typically 500) | Sequential replies via `in_reply_to_id` |
| **LinkedIn** | No native threading | 3,000 characters | Each segment posted independently |

---

### POST /api/social-posts/upload — Create Social Post (Multipart Upload)

Creates a cross-platform social post with images uploaded as files via `multipart/form-data`. Alt texts are paired with images in order.

| Detail | Value |
|---|---|
| **Auth** | `X-Api-Key` header |
| **Content-Type** | `multipart/form-data` |

#### Form Fields

| Field | Type | Required | Description |
|---|---|---|---|
| `text` | `string` | Yes (if no images) | Post body text (max 10,000 chars). |
| `hashtags` | `string[]` | No | Hashtags to append. |
| `platforms` | `string[]` | No | Target platforms: `bluesky`, `mastodon`, `linkedin`. |
| `scheduledFor` | `string` (ISO 8601) | No | Future UTC datetime for deferred posting. |
| `images` | `file[]` | No | Up to 4 image files (JPEG, PNG, GIF, WebP; max 1 MB each). |
| `altTexts` | `string[]` | Yes (if images) | One alt text per image, matched by position (max 1,500 chars each). |

#### Example — Upload with Images

```http
POST /api/social-posts/upload
Content-Type: multipart/form-data
```

| Field | Value |
|---|---|
| `text` | `Check out this screenshot!` |
| `hashtags` | `dotnet`, `aspire` |
| `platforms` | `bluesky`, `mastodon` |
| `images` | `screenshot.png` |
| `altTexts` | `Screenshot of the new dashboard` |

#### Example — Multiple Images

```http
POST /api/social-posts/upload
Content-Type: multipart/form-data
```

| Field | Value |
|---|---|
| `text` | `Before and after comparison` |
| `platforms` | `bluesky`, `linkedin` |
| `images` | `before.jpg`, `after.jpg` |
| `altTexts` | `Before the refactor`, `After the refactor` |

#### Example — Schedule an Upload Post for Later

```http
POST /api/social-posts/upload
Content-Type: multipart/form-data
```

| Field | Value |
|---|---|
| `text` | `Scheduled image post for tomorrow` |
| `platforms` | `bluesky`, `mastodon` |
| `scheduledFor` | `2026-03-23T16:00:00Z` |
| `images` | `launch-banner.png` |
| `altTexts` | `Launch banner showing feature highlights` |

#### Response

Same response shape and status codes as [`POST /api/social-posts`](#post-apisocial-posts--create-social-post-json).

When `scheduledFor` is provided and is in the future, the response indicates the post was scheduled:

```json
{
  "results": [],
  "postedAt": null,
  "scheduled": true,
  "scheduledPostId": "sp_01HZYD3M5Q9K6Q",
  "scheduledFor": "2026-03-23T20:00:00+00:00"
}
```

#### Image Constraints

- **Max 4 images** per request.
- Allowed content types: `image/jpeg`, `image/png`, `image/gif`, `image/webp`.
- **Max 1 MB** per image.
- Alt text count **must** match image count.
- Alt texts must not be blank and must not exceed 1,500 characters.

---

### POST /api/social-posts/scheduled/process — Process Due Scheduled Posts

Processes scheduled posts that are due (`scheduledFor <= now`), posts them to configured target platforms, and returns run metrics.

| Detail | Value |
|---|---|
| **Auth** | `X-Api-Key` header |
| **Content-Type** | `application/json` |

#### Request Body

| Field | Type | Required | Description |
|---|---|---|---|
| `maxCount` | `int` | No | Optional cap for number of due posts to process in a single run (1-1000). |

#### Example

```http
POST /api/social-posts/scheduled/process
```

```json
{
  "maxCount": 100
}
```

#### Response — 200 OK (All Attempted or Partial Success)

```json
{
  "runId": "sched-run-20260322180000-a1b2c3",
  "startedAtUtc": "2026-03-22T18:00:00+00:00",
  "completedAtUtc": "2026-03-22T18:00:03+00:00",
  "dueCount": 3,
  "attemptedCount": 3,
  "succeededCount": 2,
  "failedCount": 1,
  "skippedCount": 0,
  "failures": [
    {
      "scheduledPostId": "sp_01HZYD3M5Q9K6Q",
      "scheduledForUtc": "2026-03-22T17:59:00+00:00",
      "platforms": ["bluesky", "mastodon"],
      "errorCode": "PLATFORM_ERROR",
      "errorMessage": "No platform succeeded for the scheduled post.",
      "attemptedAtUtc": "2026-03-22T18:00:02+00:00"
    }
  ]
}
```

#### Response — 502 Bad Gateway (All Attempts Failed)

Returned when at least one due post was attempted and no attempts succeeded.

#### Status Codes

| Code | Meaning |
|---|---|
| **200** | Processing completed with at least one success, or no due posts to process. |
| **400** | Request validation failed. |
| **401** | Missing or invalid `X-Api-Key`. |
| **502** | Due posts were attempted and all attempts failed. |

---

### POST /api/social-posts/rss-promotion — Trigger RSS Blog Promotion

Reads the configured RSS feed, posts newly published entries first, then posts any eligible reminder entries. Tracks which entries have been posted using Azure Table Storage to avoid duplicates. Initial posts contain the entry title and URL. Reminder posts are prefixed with *"In case you missed it earlier..."* followed by a blank line before the entry title and URL.

Supports both **standard Atom 2.0 / RSS 2.0 feeds** and the **custom blog feed format**. All entries are eligible regardless of whether they have tags — the same standard feed fallbacks described in the [rss-random endpoint](#post-apisocial-postsrss-random--post-random-rss-entry) apply here (summary, hero image, and tag extraction from standard feed elements).

| Detail | Value |
|---|---|
| **Auth** | `X-Api-Key` header |
| **Content-Type** | `application/json` (optional — body may be omitted or empty) |

#### Request Body

All fields are optional. When the body is omitted entirely, the endpoint uses the configured defaults.

| Field | Type | Required | Default | Description |
|---|---|---|---|---|
| `feedUrl` | `string` | No | Server config | URL of the RSS/Atom feed to read. Must be an absolute `http` or `https` URL. Falls back to the configured default when omitted or empty. |
| `header` | `string` | No | _(none)_ | Text prepended to every post (initial and reminder). Separated from the post body by a blank line. |
| `recentDaysWindow` | `int` | No | Server config | Number of days back to look for posts to promote. Must be greater than 0 when provided. Overrides the configured `RecentDaysWindow`. |

#### Example — Default Feed (No Body)

```http
POST /api/social-posts/rss-promotion
```

#### Example — Custom Feed URL with Header

```http
POST /api/social-posts/rss-promotion
Content-Type: application/json

{
  "feedUrl": "https://example.com/custom-feed.xml",
  "header": "Check out this blog post!",
  "recentDaysWindow": 14
}
```

#### Response — 200 OK

```json
{
  "runId": "promo-a1b2c3d4",
  "startedAtUtc": "2026-03-04T10:00:00+00:00",
  "completedAtUtc": "2026-03-04T10:00:05+00:00",
  "entriesEvaluated": 12,
  "newPostsAttempted": 2,
  "newPostsSucceeded": 2,
  "reminderPostsAttempted": 1,
  "reminderPostsSucceeded": 1,
  "entriesSkippedAlreadyPosted": 8,
  "entriesSkippedOutsideWindow": 1,
  "failures": [],
  "lastTwoBlogPosts": [
    {
      "entryIdentity": "https://example.com/blog/post-1",
      "canonicalUrl": "https://example.com/blog/post-1",
      "title": "My Latest Blog Post",
      "publishedAtUtc": "2026-03-03T08:00:00+00:00"
    },
    {
      "entryIdentity": "https://example.com/blog/post-2",
      "canonicalUrl": "https://example.com/blog/post-2",
      "title": "Another Great Post",
      "publishedAtUtc": "2026-03-01T14:30:00+00:00"
    }
  ]
}
```

#### Response — 502 (Feed Read Failed or All Posts Failed)

```json
{
  "runId": "failed-a1b2c3d4",
  "startedAtUtc": "2026-03-04T10:00:00+00:00",
  "completedAtUtc": "2026-03-04T10:00:00+00:00",
  "entriesEvaluated": 0,
  "newPostsAttempted": 0,
  "newPostsSucceeded": 0,
  "reminderPostsAttempted": 0,
  "reminderPostsSucceeded": 0,
  "entriesSkippedAlreadyPosted": 0,
  "entriesSkippedOutsideWindow": 0,
  "failures": [
    {
      "entryIdentity": "rss-feed",
      "canonicalUrl": "",
      "phase": "Initial",
      "platform": "rss-promotion",
      "errorCode": "UNHANDLED_EXCEPTION",
      "errorMessage": "Unable to read RSS feed"
    }
  ],
  "lastTwoBlogPosts": []
}
```

#### Status Codes

| Code | Meaning |
|---|---|
| **200** | Promotion run completed (may include partial failures in the `failures` array). |
| **400** | Invalid blog-promotion configuration. |
| **401** | Missing or invalid `X-Api-Key`. |
| **502** | Feed read failed or all posting attempts failed. |

---

### POST /api/social-posts/rss-random — Post Random RSS Entry

Fetches an RSS feed, applies optional filters (tag exclusion, recency, platform targeting), randomly selects one eligible entry, and posts it to the targeted social platforms. The post text is prefixed with *"From the archives…"* and includes the entry title, URL, qualifying hashtags, and hero image if available. This endpoint is **stateless** — it does not track previously posted entries.

Supports both **standard Atom 2.0 / RSS 2.0 feeds** and the **custom blog feed format** with `https://barretblake.dev/ns/` namespace extensions. When custom extensions are absent, the endpoint falls back to standard feed elements:

- **Summary**: Prefers `<summary>`, falls back to Atom `<content>`. HTML is stripped to plain text automatically.
- **Hero image**: Prefers custom `<hero>` extension, falls back to enclosure links with `image/*` media type, then Media RSS `<media:thumbnail>` / `<media:content>`.
- **Tags**: Prefers custom `<tags>` extension, falls back to standard `<category>` elements. All entries are eligible regardless of whether they have tags.

| Detail | Value |
|---|---|
| **Auth** | `X-Api-Key` header |
| **Content-Type** | `application/json` |

#### Request Body

| Field | Type | Required | Default | Description |
|---|---|---|---|---|
| `feedUrl` | `string` | Yes | — | Absolute URL of the RSS feed (`http` or `https`). |
| `platforms` | `string[]` | No | All configured | Target platforms: `bluesky`, `mastodon`, `linkedin`. |
| `excludeTags` | `string[]` | No | `[]` | Tags to exclude (case-insensitive match against entry tags). |
| `maxAgeDays` | `int` | No | No limit | Only include entries published within this many days. Must be > 0. |
| `header` | `string` | No | — | Optional header text prepended between the leader line and the entry title. |

#### Example — Minimal Request

```bash
curl -s -X POST https://localhost:7042/api/social-posts/rss-random \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: YOUR_API_KEY" \
  -d '{
    "feedUrl": "https://example.com/blog/feed.xml"
  }'
```

#### Example — With Filters and Header

```bash
curl -s -X POST https://localhost:7042/api/social-posts/rss-random \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: YOUR_API_KEY" \
  -d '{
    "feedUrl": "https://example.com/blog/feed.xml",
    "platforms": ["bluesky", "mastodon"],
    "excludeTags": ["personal", "draft"],
    "maxAgeDays": 30,
    "header": "Check this out!"
  }'
```

#### Response — 200 (All Platforms Succeeded)

```json
{
  "selectedTitle": "Building APIs with .NET Aspire",
  "selectedUrl": "https://example.com/blog/aspire-apis",
  "results": [
    {
      "platform": "bluesky",
      "success": true,
      "postId": "at://did:plc:abc123/app.bsky.feed.post/xyz789",
      "postUrl": "https://bsky.app/profile/handle.bsky.social/post/xyz789",
      "shortenedText": "From the archives...\n\nBuilding APIs with .NET Aspire\nhttps://example.com/blog/aspire-apis #dotnet #aspire"
    },
    {
      "platform": "mastodon",
      "success": true,
      "postId": "109876543210",
      "postUrl": "https://mastodon.social/@you/109876543210",
      "shortenedText": "From the archives...\n\nBuilding APIs with .NET Aspire\nhttps://example.com/blog/aspire-apis #dotnet #aspire"
    }
  ],
  "postedAt": "2026-03-04T12:00:00+00:00"
}
```

#### Response — 207 (Partial Success)

```json
{
  "selectedTitle": "Building APIs with .NET Aspire",
  "selectedUrl": "https://example.com/blog/aspire-apis",
  "results": [
    {
      "platform": "bluesky",
      "success": true,
      "postId": "at://did:plc:abc123/app.bsky.feed.post/xyz789",
      "postUrl": "https://bsky.app/profile/handle.bsky.social/post/xyz789",
      "shortenedText": "From the archives...\n\nBuilding APIs with .NET Aspire\nhttps://example.com/blog/aspire-apis #dotnet #aspire"
    },
    {
      "platform": "mastodon",
      "success": false,
      "error": "Authentication failed",
      "errorCode": "AUTH_FAILED"
    }
  ],
  "postedAt": "2026-03-04T12:00:00+00:00"
}
```

#### Status Codes

| Code | Meaning |
|---|---|
| **200** | All targeted platforms succeeded. |
| **207** | Partial success — at least one platform succeeded and at least one failed. |
| **400** | Request validation failed (missing `feedUrl`, invalid URL, invalid platform, `maxAgeDays` ≤ 0). |
| **401** | Missing or invalid `X-Api-Key`. |
| **422** | No eligible entries remain after filtering. |
| **502** | Feed could not be read, or all targeted platform posts failed. |

---

### POST /api/social-posts/nasa-apod — Post NASA APOD to Social Platforms

Fetches the NASA Astronomy Picture of the Day and posts it to selected social media platforms. Supports image and video APODs, with automatic image resizing and copyright attribution.

| Detail | Value |
|---|---|
| **Auth** | `X-Api-Key` header |
| **Content-Type** | `application/json` |

#### Request Body

| Field | Type | Required | Description |
|---|---|---|---|
| `date` | `string` | No | Date in `YYYY-MM-DD` format. Defaults to today. Must be between `1995-06-16` and today. |
| `platforms` | `string[]` | No | Target platforms: `bluesky`, `mastodon`, `linkedin`. Defaults to all configured. |

#### Example — Post Today's APOD to All Platforms

```http
POST /api/social-posts/nasa-apod
```

```json
{}
```

#### Example — Post a Specific Date to Selected Platforms

```http
POST /api/social-posts/nasa-apod
```

```json
{
  "date": "2026-02-14",
  "platforms": ["bluesky", "mastodon"]
}
```

#### Response — 200 OK (All Platforms Succeeded)

```json
{
  "title": "The Aurora Tree",
  "date": "2026-03-08",
  "mediaType": "image",
  "imageUrl": "https://apod.nasa.gov/apod/image/2603/AuroraTree_Wallace_960.jpg",
  "hdImageUrl": "https://apod.nasa.gov/apod/image/2603/AuroraTree_Wallace_2048.jpg",
  "copyright": "Alyn Wallace",
  "imageAttached": true,
  "imageResized": false,
  "results": [
    {
      "platform": "bluesky",
      "success": true,
      "postId": "at://did:plc:abc123/app.bsky.feed.post/xyz789",
      "postUrl": "https://bsky.app/profile/user/post/xyz789"
    },
    {
      "platform": "mastodon",
      "success": true,
      "postId": "109876543210",
      "postUrl": "https://mastodon.social/@user/109876543210"
    }
  ],
  "postedAt": "2026-03-08T15:30:00Z"
}
```

#### Response — 207 Multi-Status (Partial Success)

```json
{
  "title": "The Aurora Tree",
  "date": "2026-03-08",
  "mediaType": "image",
  "imageUrl": "https://apod.nasa.gov/apod/image/2603/AuroraTree_Wallace_960.jpg",
  "imageAttached": true,
  "imageResized": false,
  "results": [
    {
      "platform": "bluesky",
      "success": true,
      "postId": "at://did:plc:abc123/app.bsky.feed.post/xyz789",
      "postUrl": "https://bsky.app/profile/user/post/xyz789"
    },
    {
      "platform": "mastodon",
      "success": false,
      "error": "Rate limit exceeded",
      "errorCode": "RATE_LIMITED"
    }
  ],
  "postedAt": "2026-03-08T15:30:00Z"
}
```

#### Response — 422 Unprocessable Entity (NASA API Error)

```json
{
  "statusCode": 422,
  "message": "Failed to fetch APOD from NASA API: Response status code does not indicate success: 429 (Too Many Requests)."
}
```

#### Behavior Details

- **Image APOD**: Downloads the image, uses the APOD `explanation` field as alt text, attaches the image to the post. The HD image URL is included in the post text.
- **Video APOD**: Uses the video thumbnail as the post image if available; otherwise posts text-only with the video URL.
- **Copyright**: If the APOD has a copyright holder, a `Credit: {holder}` line is appended to the post text.
- **Image Resizing**: Images are automatically resized to fit each platform's limits using a quality-first strategy (JPEG quality 85→45), falling back to dimension reduction if needed.

#### Status Codes

| Code | Meaning |
|---|---|
| **200** | All targeted platforms succeeded. |
| **207** | Partial success — at least one platform succeeded and at least one failed. |
| **400** | Request validation failed (invalid date format, date out of range, invalid platform). |
| **401** | Missing or invalid `X-Api-Key`. |
| **422** | NASA API returned an error or the APOD could not be fetched. |
| **502** | All targeted platforms failed. |

---

### POST /api/social-posts/satellite — Post Satellite Image

Fetches a satellite image from NASA GIBS (Global Imagery Browse Services) and posts it to selected social media platforms. The image is captured from the Worldview Snapshot API using configurable satellite imagery layers and date.

| Detail | Value |
|---|---|
| **Auth** | `X-Api-Key` header |
| **Content-Type** | `application/json` |

#### Request Body

| Field | Type | Required | Description |
|---|---|---|---|
| `date` | `string` | No | Date in `YYYY-MM-DD` format. Defaults to yesterday (UTC). Must not be before the selected layer's start date or in the future. |
| `layer` | `string` | No | Satellite imagery layer. Defaults to `MODIS_Terra_CorrectedReflectance_TrueColor`. See supported layers below. |
| `platforms` | `string[]` | No | Target platforms: `bluesky`, `mastodon`, `linkedin`. Defaults to all configured. |
| `title` | `string` | No | Custom title for the post caption. Defaults to `"Satellite view of Ohio"` (configurable default). Max 200 characters. |
| `description` | `string` | No | Custom alt text for the image. Defaults to auto-generated text with date and layer. Max 1000 characters. |
| `bboxSouth` | `number` | No | Southern boundary latitude (-90 to 90). Defaults to `38.40`. Must be less than `bboxNorth`. |
| `bboxWest` | `number` | No | Western boundary longitude (-180 to 180). Defaults to `-84.82`. Must be less than `bboxEast`. |
| `bboxNorth` | `number` | No | Northern boundary latitude (-90 to 90). Defaults to `42.32`. Must be greater than `bboxSouth`. |
| `bboxEast` | `number` | No | Eastern boundary longitude (-180 to 180). Defaults to `-80.52`. Must be greater than `bboxWest`. |
| `imageWidth` | `integer` | No | Snapshot image width in pixels (1–8192). Defaults to `1024`. |
| `imageHeight` | `integer` | No | Snapshot image height in pixels (1–8192). Defaults to `768`. |

#### Supported Layers

| Layer | Instrument | Available From |
|---|---|---|
| `MODIS_Terra_CorrectedReflectance_TrueColor` | MODIS (Terra) | 2000-02-24 |
| `MODIS_Aqua_CorrectedReflectance_TrueColor` | MODIS (Aqua) | 2002-07-04 |
| `VIIRS_SNPP_CorrectedReflectance_TrueColor` | VIIRS (Suomi NPP) | 2015-11-24 |
| `VIIRS_NOAA20_CorrectedReflectance_TrueColor` | VIIRS (NOAA-20) | 2017-12-01 |
| `VIIRS_NOAA21_CorrectedReflectance_TrueColor` | VIIRS (NOAA-21) | 2024-01-17 |

#### Example — Post Yesterday's Image with Defaults

```http
POST /api/social-posts/satellite
```

```json
{}
```

#### Example — Post a Specific Date and Layer

```http
POST /api/social-posts/satellite
```

```json
{
  "date": "2026-02-14",
  "layer": "VIIRS_SNPP_CorrectedReflectance_TrueColor",
  "platforms": ["bluesky", "mastodon"]
}
```

#### Example — Post a Custom Region (Grand Canyon)

```http
POST /api/social-posts/satellite
```

```json
{
  "title": "Satellite view of the Grand Canyon",
  "description": "Aerial satellite image of the Grand Canyon, Arizona, captured by NASA GIBS.",
  "bboxSouth": 35.9,
  "bboxWest": -112.6,
  "bboxNorth": 36.5,
  "bboxEast": -111.6,
  "imageWidth": 1280,
  "imageHeight": 960,
  "platforms": ["bluesky"]
}
```

#### Response — 200 OK (All Platforms Succeeded)

```json
{
  "date": "2026-03-15",
  "layer": "MODIS_Terra_CorrectedReflectance_TrueColor",
  "title": "Satellite view of Ohio",
  "worldviewUrl": "https://worldview.earthdata.nasa.gov/?v=-84.82,38.40,-80.52,42.32&l=MODIS_Terra_CorrectedReflectance_TrueColor&t=2026-03-15",
  "bboxSouth": 38.40,
  "bboxWest": -84.82,
  "bboxNorth": 42.32,
  "bboxEast": -80.52,
  "imageWidth": 1024,
  "imageHeight": 768,
  "imageAttached": true,
  "imageResized": false,
  "results": [
    {
      "platform": "bluesky",
      "success": true,
      "postId": "at://did:plc:abc123/app.bsky.feed.post/xyz789",
      "postUrl": "https://bsky.app/profile/user/post/xyz789"
    },
    {
      "platform": "mastodon",
      "success": true,
      "postId": "109876543210",
      "postUrl": "https://mastodon.social/@user/109876543210"
    }
  ],
  "postedAt": "2026-03-15T15:30:00Z"
}
```

#### Response — 422 Unprocessable Entity (GIBS API Error)

```json
{
  "statusCode": 422,
  "message": "Failed to fetch snapshot from NASA GIBS: GIBS returned an error: Missing or invalid TIME parameter"
}
```

#### Behavior Details

- **Default Date**: Uses yesterday's date (UTC) when no date is specified, since same-day imagery may not yet be available.
- **Custom Region**: Override `bboxSouth`, `bboxWest`, `bboxNorth`, and `bboxEast` to capture any geographic region. Defaults to a preconfigured bounding box.
- **Custom Dimensions**: Override `imageWidth` and `imageHeight` (1–8192) to control the snapshot resolution. Defaults to 1024×768.
- **Image Attachment**: The GIBS snapshot (JPEG, typically 80–400 KB at default resolution) is attached directly to the social post.
- **Post Caption**: Includes the title (customizable), date, layer name, a Worldview link for interactive exploration, and NASA GIBS acknowledgement.
- **Alt Text**: Uses `description` if provided; otherwise auto-generates alt text from the date and layer.
- **Hashtags**: Posts include `#satellite`, `#NASA`, and `#EarthObservation`.
- **Worldview Link**: Each post includes a link to NASA Worldview showing the same view, allowing viewers to explore the imagery interactively.
- **No API Key Required**: NASA GIBS is publicly accessible — no NASA API key is needed.

#### Configuration Overrides

All GIBS parameters have sensible defaults and are configured in the Aspire AppHost. See [NASA GIBS](#nasa-gibs) in the Configuration section for the full reference.

| Config Key | Aspire Parameter | Environment Variable | Default | Description |
|---|---|---|---|---|
| `NasaGibs:BaseUrl` | `gibs-base-url` | `NasaGibs__BaseUrl` | `https://wvs.earthdata.nasa.gov/api/v1/snapshot` | GIBS Worldview Snapshot API base URL. |
| `NasaGibs:DefaultLayer` | `gibs-default-layer` | `NasaGibs__DefaultLayer` | `MODIS_Terra_CorrectedReflectance_TrueColor` | Default imagery layer. |
| `NasaGibs:BboxSouth` | `gibs-bbox-south` | `NasaGibs__BboxSouth` | `38.40` | Southern boundary (latitude). |
| `NasaGibs:BboxWest` | `gibs-bbox-west` | `NasaGibs__BboxWest` | `-84.82` | Western boundary (longitude). |
| `NasaGibs:BboxNorth` | `gibs-bbox-north` | `NasaGibs__BboxNorth` | `42.32` | Northern boundary (latitude). |
| `NasaGibs:BboxEast` | `gibs-bbox-east` | `NasaGibs__BboxEast` | `-80.52` | Eastern boundary (longitude). |
| `NasaGibs:ImageWidth` | `gibs-image-width` | `NasaGibs__ImageWidth` | `1024` | Snapshot image width in pixels. |
| `NasaGibs:ImageHeight` | `gibs-image-height` | `NasaGibs__ImageHeight` | `768` | Snapshot image height in pixels. |

#### Status Codes

| Code | Meaning |
|---|---|
| **200** | All targeted platforms succeeded. |
| **207** | Partial success — at least one platform succeeded and at least one failed. |
| **400** | Request validation failed (invalid date, unsupported layer, invalid platform, bbox out of range, image dimensions out of range, title/description too long). |
| **401** | Missing or invalid `X-Api-Key`. |
| **422** | NASA GIBS returned an error or the snapshot could not be fetched. |
| **502** | All targeted platforms failed. |

---

### GET /api/linkedin/auth — Initiate LinkedIn OAuth Flow

Starts the LinkedIn OAuth 2.0 authorization flow. Open this URL directly in a **browser** to be redirected to LinkedIn's consent screen. When called from a non-browser API client, returns a JSON object with the authorization URL.

| Detail | Value |
|---|---|
| **Auth** | None (anonymous) |

#### Example — Browser

Navigate to:

```
https://<your-api-host>/api/linkedin/auth
```

You will be redirected to LinkedIn to authorize the application.

#### Example — API Client

```http
GET /api/linkedin/auth
Accept: application/json
```

#### Response — 200 OK (API Client)

```json
{
  "authUrl": "https://www.linkedin.com/oauth/v2/authorization?response_type=code&client_id=YOUR_CLIENT_ID&redirect_uri=https%3A%2F%2Fyour-api-host%2Fapi%2Flinkedin%2Fauth%2Fcallback&state=abc123&scope=openid%20profile%20w_member_social"
}
```

---

### GET /api/linkedin/auth/callback — LinkedIn OAuth Callback

Receives the authorization code from LinkedIn after the user approves access. Exchanges the code for access and refresh tokens and persists them to Azure Table Storage.

| Detail | Value |
|---|---|
| **Auth** | None (anonymous) |

This endpoint is called automatically by LinkedIn after authorization. You do not need to call it manually.

#### Query Parameters

| Parameter | Description |
|---|---|
| `code` | Authorization code from LinkedIn. |
| `state` | State parameter for CSRF protection. |
| `error` | Error code if authorization was denied. |
| `error_description` | Human-readable error description. |

#### Response — 200 OK

```json
{
  "success": true,
  "message": "LinkedIn authorization successful. Tokens have been saved."
}
```

#### Response — 400 Bad Request

```json
{
  "success": false,
  "message": "LinkedIn authorization denied: user_cancelled_authorize"
}
```

#### Response — 502 Bad Gateway

```json
{
  "success": false,
  "message": "Token exchange failed: LinkedIn token exchange failed: invalid_grant"
}
```

---

### GET /api/linkedin/profile — Get LinkedIn Profile

Returns your LinkedIn profile info, including the member URN (`sub`) needed for the `LinkedIn:AuthorUrn` configuration value.

| Detail | Value |
|---|---|
| **Auth** | None (anonymous) |

> **Note:** You must complete the LinkedIn OAuth flow (`/api/linkedin/auth`) before calling this endpoint.

#### Example

```http
GET /api/linkedin/profile
```

#### Response — 200 OK

```json
{
  "sub": "abc123def456",
  "name": "John Doe",
  "given_name": "John",
  "family_name": "Doe",
  "picture": "https://media.licdn.com/...",
  "email": "john@example.com"
}
```

Use the `sub` value to construct your `AuthorUrn`: `urn:li:person:<sub>`.

#### Response — 400 (No Tokens)

```json
{
  "error": "No LinkedIn tokens found. Visit /api/linkedin/auth first."
}
```

---

### POST /api/word-cloud — Generate Word Cloud

Generates a PNG word cloud image from the visible text content of a web page. Common English stop words are excluded and words are sized proportionally to their frequency on the page.

| Detail | Value |
|---|---|
| **Auth** | `X-Api-Key` header |
| **Content-Type** | `application/json` |
| **Response Content-Type** | `image/png` |

#### Request Body

| Field | Type | Required | Default | Description |
|---|---|---|---|---|
| `url` | `string` | Yes | — | Absolute HTTP or HTTPS URL of the target web page. |
| `width` | `integer` | No | `800` | Output image width in pixels (200–2000). |
| `height` | `integer` | No | `600` | Output image height in pixels (200–2000). |

#### Example — Default Dimensions

```http
POST /api/word-cloud
```

```json
{
  "url": "https://en.wikipedia.org/wiki/.NET"
}
```

#### Example — Custom Dimensions

```json
{
  "url": "https://en.wikipedia.org/wiki/.NET",
  "width": 1200,
  "height": 800
}
```

#### Response — 200 OK

Binary PNG image data with `Content-Type: image/png`.

```bash
curl -X POST http://localhost:5000/api/word-cloud \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: YOUR_API_KEY" \
  -d '{"url": "https://en.wikipedia.org/wiki/.NET"}' \
  --output word-cloud.png
```

#### Response — 400 (Validation Error)

```json
{
  "statusCode": 400,
  "message": "One or more validation errors occurred.",
  "errors": {
    "url": ["The URL must be a valid absolute HTTP or HTTPS URL."]
  }
}
```

#### Response — 422 (Insufficient Text)

```json
{
  "statusCode": 422,
  "message": "The page contains insufficient text content to generate a word cloud."
}
```

#### Response — 502 (Fetch Failed)

```json
{
  "statusCode": 502,
  "message": "Failed to fetch the web page. The target URL is unreachable or returned an error."
}
```

#### Limits

| Aspect | Value |
|---|---|
| Fetch timeout | 30 seconds |
| Max HTML size | 500 KB |
| Max words in cloud | 100 |
| Min word length | 3 characters |
| Image size range | 200×200 to 2000×2000 px |

---

### GET /api/avatars/random — Generate Random Avatar

Generates a random avatar image using the [DiceBear](https://www.dicebear.com/) API (v9.x). Returns the raw image bytes directly. Optionally specify a style, format, and seed for customization.

| Detail | Value |
|---|---|
| **Auth** | `X-Api-Key` header |
| **Method** | `GET` |
| **Response Content-Type** | Varies by format (default `image/svg+xml`) |

#### Query Parameters

| Parameter | Type | Required | Default | Description |
|---|---|---|---|---|
| `style` | `string` | No | Random | One of the 31 supported DiceBear styles (e.g., `pixel-art`, `adventurer`, `bottts`). |
| `format` | `string` | No | `svg` | Output format: `svg`, `png`, `jpg`, `webp`, or `avif`. |
| `seed` | `string` | No | Random GUID | Seed for reproducible avatars. Max 256 characters. Same seed + style = same avatar. |

#### Supported Styles

`adventurer`, `adventurer-neutral`, `avataaars`, `avataaars-neutral`, `big-ears`, `big-ears-neutral`, `big-smile`, `bottts`, `bottts-neutral`, `croodles`, `croodles-neutral`, `dylan`, `fun-emoji`, `glass`, `icons`, `identicon`, `initials`, `lorelei`, `lorelei-neutral`, `micah`, `miniavs`, `notionists`, `notionists-neutral`, `open-peeps`, `personas`, `pixel-art`, `pixel-art-neutral`, `rings`, `shapes`, `thumbs`, `toon-head`

#### Example — Random Avatar (Default SVG)

```bash
curl http://localhost:5000/api/avatars/random \
  -H "X-Api-Key: YOUR_API_KEY" \
  --output avatar.svg
```

#### Example — Specific Style and Format

```bash
curl "http://localhost:5000/api/avatars/random?style=pixel-art&format=png" \
  -H "X-Api-Key: YOUR_API_KEY" \
  --output avatar.png
```

#### Example — Reproducible Avatar with Seed

```bash
curl "http://localhost:5000/api/avatars/random?seed=john-doe&style=bottts&format=webp" \
  -H "X-Api-Key: YOUR_API_KEY" \
  --output avatar.webp
```

#### Response — 200 OK

Binary image data with the appropriate `Content-Type` header:

| Format | Content-Type |
|---|---|
| `svg` | `image/svg+xml` |
| `png` | `image/png` |
| `jpg` | `image/jpeg` |
| `webp` | `image/webp` |
| `avif` | `image/avif` |

#### Response — 400 (Validation Error)

```json
{
  "statusCode": 400,
  "message": "One or more validation errors occurred.",
  "errors": {
    "style": ["'Style' must be one of the supported styles: adventurer, adventurer-neutral, ..."]
  }
}
```

#### Response — 502 (Upstream Service Unavailable)

```json
{
  "statusCode": 502,
  "message": "The avatar generation service is temporarily unavailable."
}
```

#### Status Codes

| Code | Meaning |
|---|---|
| **200** | Avatar image generated successfully. |
| **400** | Invalid style, format, or seed (exceeds 256 chars). |
| **401** | Missing or invalid `X-Api-Key`. |
| **502** | DiceBear API is unreachable or returned an error. |

---

### GET /api/github/auth — Initiate GitHub OAuth Flow

Starts the GitHub OAuth authorization flow. Open this URL directly in a **browser** to be redirected to GitHub's consent screen. When called from a non-browser API client, returns a JSON object with the authorization URL.

| Detail | Value |
|---|---|
| **Auth** | None (anonymous) |

#### Example — Browser

Navigate to:

```
https://<your-api-host>/api/github/auth
```

You will be redirected to GitHub to authorize the application.

#### Example — API Client

```http
GET /api/github/auth
Accept: application/json
```

#### Response — 200 OK (API Client)

```json
{
  "authUrl": "https://github.com/login/oauth/authorize?client_id=YOUR_CLIENT_ID&redirect_uri=https%3A%2F%2Fyour-api-host%2Fapi%2Fgithub%2Fauth%2Fcallback&scope=repo&state=abc123"
}
```

---

### GET /api/github/auth/callback — GitHub OAuth Callback

Receives the authorization code from GitHub after the user approves access. Exchanges the code for an access token, retrieves the user profile, and persists the token to Azure Table Storage.

| Detail | Value |
|---|---|
| **Auth** | None (anonymous) |

This endpoint is called automatically by GitHub after authorization. You do not need to call it manually.

#### Query Parameters

| Parameter | Description |
|---|---|
| `code` | Authorization code from GitHub. |
| `state` | State parameter for CSRF protection. |
| `error` | Error code if authorization was denied. |
| `error_description` | Human-readable error description. |

#### Response — 200 OK

```json
{
  "username": "octocat",
  "status": "connected",
  "scope": "repo"
}
```

#### Response — 400 Bad Request

```json
{
  "statusCode": 400,
  "message": "GitHub authorization denied: access_denied"
}
```

---

### GET /api/github/profile — Get GitHub Profile

Returns the currently connected GitHub user info, or indicates no connection exists.

| Detail | Value |
|---|---|
| **Auth** | None (anonymous) |

#### Example

```http
GET /api/github/profile
```

#### Response — 200 OK (Connected)

```json
{
  "username": "octocat",
  "connected": true,
  "scope": "repo",
  "connectedAtUtc": "2026-03-25T14:00:00Z"
}
```

#### Response — 200 OK (Not Connected)

```json
{
  "username": null,
  "connected": false,
  "scope": null,
  "connectedAtUtc": null
}
```

---

### POST /api/github/repos/sync — Sync GitHub Repositories

Fetches all repositories owned by the authenticated GitHub user and stores them locally, replacing any previously stored data.

| Detail | Value |
|---|---|
| **Auth** | `X-Api-Key` header |

#### Example

```http
POST /api/github/repos/sync
X-Api-Key: YOUR_API_KEY
```

#### Response — 200 OK

```json
{
  "count": 42,
  "syncedAtUtc": "2026-03-25T14:35:00Z",
  "username": "octocat"
}
```

#### Response — 401 Unauthorized

```json
{
  "statusCode": 401,
  "message": "GitHub authentication required. Visit /api/github/auth to connect."
}
```

#### Response — 429 Too Many Requests

```json
{
  "statusCode": 429,
  "message": "GitHub API rate limit exceeded. Resets at 2026-03-25T15:00:00Z."
}
```

#### Response — 502 Bad Gateway

```json
{
  "statusCode": 502,
  "message": "GitHub API error: 500 — Internal Server Error"
}
```

---

### GET /api/github/repos — List GitHub Repositories

Returns all locally stored GitHub repositories.

| Detail | Value |
|---|---|
| **Auth** | `X-Api-Key` header |

#### Example

```http
GET /api/github/repos
X-Api-Key: YOUR_API_KEY
```

#### Response — 200 OK

```json
{
  "repositories": [
    {
      "name": "my-repo",
      "fullName": "octocat/my-repo",
      "description": "A sample repository",
      "isPrivate": false,
      "defaultBranch": "main",
      "htmlUrl": "https://github.com/octocat/my-repo",
      "updatedAtUtc": "2026-03-20T10:00:00Z"
    }
  ],
  "count": 1,
  "syncedAtUtc": "2026-03-25T14:35:00Z"
}
```

#### Response — 200 OK (No Repositories Synced)

```json
{
  "repositories": [],
  "count": 0,
  "syncedAtUtc": null
}
```

---

### GET /api/github/repos/{name} — Get Repository Details

Returns details for a single stored repository by name.

| Detail | Value |
|---|---|
| **Auth** | `X-Api-Key` header |

#### Path Parameters

| Parameter | Type | Required | Description |
|---|---|---|---|
| `name` | `string` | Yes | Repository name (e.g., `my-repo`) |

#### Example

```http
GET /api/github/repos/my-repo
X-Api-Key: YOUR_API_KEY
```

#### Response — 200 OK

```json
{
  "name": "my-repo",
  "fullName": "octocat/my-repo",
  "description": "A sample repository",
  "isPrivate": false,
  "defaultBranch": "main",
  "htmlUrl": "https://github.com/octocat/my-repo",
  "updatedAtUtc": "2026-03-20T10:00:00Z",
  "syncedAtUtc": "2026-03-25T14:35:00Z"
}
```

#### Response — 404 Not Found

```json
{
  "statusCode": 404,
  "message": "Repository 'unknown-repo' not found. Run POST /api/github/repos/sync to refresh."
}
```

---

### POST /api/github/repos/{name}/issues — Create GitHub Issue

Creates a new issue on the specified GitHub repository.

| Detail | Value |
|---|---|
| **Auth** | `X-Api-Key` header |
| **Content-Type** | `application/json` |

#### Path Parameters

| Parameter | Type | Required | Description |
|---|---|---|---|
| `name` | `string` | Yes | Repository name (e.g., `my-repo`) |

#### Request Body

| Field | Type | Required | Description |
|---|---|---|---|
| `title` | `string` | Yes | Issue title. |
| `body` | `string` | No | Issue body (Markdown supported). |
| `labels` | `string[]` | No | List of label names to apply. |

#### Example — Minimal

```http
POST /api/github/repos/my-repo/issues
X-Api-Key: YOUR_API_KEY
Content-Type: application/json
```

```json
{
  "title": "Fix login page styling"
}
```

#### Example — Full

```http
POST /api/github/repos/my-repo/issues
X-Api-Key: YOUR_API_KEY
Content-Type: application/json
```

```json
{
  "title": "Add dark mode support",
  "body": "## Description\n\nUsers have requested a dark mode option.",
  "labels": ["enhancement", "ui"]
}
```

#### Response — 201 Created

```json
{
  "number": 42,
  "title": "Add dark mode support",
  "htmlUrl": "https://github.com/octocat/my-repo/issues/42",
  "state": "open"
}
```

#### Response — 400 Bad Request

```json
{
  "statusCode": 400,
  "message": "One or more validation errors occurred.",
  "errors": {
    "title": ["'title' must not be empty."]
  }
}
```

#### Response — 404 Not Found

```json
{
  "statusCode": 404,
  "message": "Repository 'unknown-repo' not found. Run POST /api/github/repos/sync to refresh."
}
```

#### Response — 401 Unauthorized

```json
{
  "statusCode": 401,
  "message": "GitHub authentication required. Visit /api/github/auth to connect."
}
```

#### Response — 429 Too Many Requests

```json
{
  "statusCode": 429,
  "message": "GitHub API rate limit exceeded. Resets at 2026-03-25T15:00:00Z."
}
```

#### Response — 502 Bad Gateway

```json
{
  "statusCode": 502,
  "message": "GitHub API error: 422 — Validation Failed (Issues are disabled for this repository)"
}
```

---

### POST /api/hero-image — Generate Hero Image

Composites a 1280×720 PNG hero image with title (and optional subtitle) overlaid on a faded background, with the author's face in the lower-right and logo in the lower-left. Returns the PNG as binary image data.

| Detail | Value |
|---|---|
| **Auth** | `X-Api-Key` header |
| **Content-Type** | `multipart/form-data` |
| **Response** | `image/png` binary (1280×720) |

#### Request Fields

| Field | Type | Required | Constraints |
|---|---|---|---|
| `title` | `string` | **Yes** | Non-empty, max 200 characters |
| `subtitle` | `string` | No | Max 300 characters |
| `backgroundImage` | `file` | No | JPEG or PNG, max 10 MB |

#### Example — Title Only

```http
POST /api/hero-image
X-Api-Key: YOUR_API_KEY
Content-Type: multipart/form-data
```

```bash
curl -X POST http://localhost:5000/api/hero-image \
  -H "X-Api-Key: YOUR_API_KEY" \
  -F "title=Getting Started with .NET 10" \
  -o hero.png
```

#### Example — Title and Subtitle

```bash
curl -X POST http://localhost:5000/api/hero-image \
  -H "X-Api-Key: YOUR_API_KEY" \
  -F "title=Blazor Deep Dive" \
  -F "subtitle=Part 3: Component Lifecycle" \
  -o hero.png
```

#### Example — Custom Background

```bash
curl -X POST http://localhost:5000/api/hero-image \
  -H "X-Api-Key: YOUR_API_KEY" \
  -F "title=Azure Functions" \
  -F "backgroundImage=@my-background.jpg" \
  -o hero.png
```

#### Response — 200 OK

Binary PNG image data (1280×720). Set the `Accept` header or rely on `Content-Type: image/png` to handle the binary response.

#### Response — 400 Bad Request

```json
{
  "statusCode": 400,
  "message": "One or more validation errors occurred.",
  "errors": {
    "title": ["Title is required."]
  }
}
```

#### Response — 422 Unprocessable Entity

Returned when a background image file is uploaded but cannot be decoded as a valid image.

```json
{
  "statusCode": 422,
  "message": "Failed to decode custom background image."
}
```

#### Response — 500 Internal Server Error

```json
{
  "statusCode": 500,
  "message": "An unexpected error occurred during image generation."
}
```

---

## Configuration

All configuration is managed through the **Aspire AppHost** project. Do not add `appsettings.json` entries in other projects. Use **User Secrets** in the AppHost for sensitive values during development.

Each setting has up to three identifiers:

| Name | Format | Example | Used In |
|---|---|---|---|
| **Config Key** | Colon-separated | `Bluesky:Handle` | `IOptions<T>`, `appsettings.json` |
| **Aspire Parameter** | Kebab-case | `bluesky-handle` | `builder.AddParameter()`, User Secrets |
| **Environment Variable** | Double-underscore | `Bluesky__Handle` | `.WithEnvironment()`, Azure App Service, containers |

Settings shown with `—` for the Aspire parameter are hardcoded in the AppHost (e.g., connection strings that use Azurite locally).

### Platform Credentials

#### Bluesky

| Config Key | Aspire Parameter | Environment Variable | Required | Description |
|---|---|---|---|---|
| `Bluesky:Handle` | `bluesky-handle` | `Bluesky__Handle` | Yes | Your Bluesky handle (e.g., `your-handle.bsky.social`). |
| `Bluesky:AppPassword` | `bluesky-app-password` | `Bluesky__AppPassword` | Yes | App password generated from Bluesky settings. |
| `Bluesky:ServiceUrl` | — | `Bluesky__ServiceUrl` | No | Defaults to `https://bsky.social`. Hardcoded in AppHost. |

#### Mastodon

| Config Key | Aspire Parameter | Environment Variable | Required | Description |
|---|---|---|---|---|
| `Mastodon:InstanceUrl` | `mastodon-instance-url` | `Mastodon__InstanceUrl` | Yes | Your Mastodon instance URL (e.g., `https://mastodon.social`). |
| `Mastodon:AccessToken` | `mastodon-access-token` | `Mastodon__AccessToken` | Yes | Access token from your Mastodon application. |

#### LinkedIn

| Config Key | Aspire Parameter | Environment Variable | Required | Description |
|---|---|---|---|---|
| `LinkedIn:ClientId` | `linkedin-client-id` | `LinkedIn__ClientId` | Yes | OAuth application client ID. |
| `LinkedIn:ClientSecret` | `linkedin-client-secret` | `LinkedIn__ClientSecret` | Yes | OAuth application client secret. |
| `LinkedIn:AuthorUrn` | `linkedin-author-urn` | `LinkedIn__AuthorUrn` | Yes | URN of the posting author (e.g., `urn:li:person:abc123`). |
| `LinkedIn:ApiBaseUrl` | `linkedin-api-base-url` | `LinkedIn__ApiBaseUrl` | No | Defaults to `https://api.linkedin.com`. |
| `LinkedIn:OAuthBaseUrl` | `linkedin-oauth-base-url` | `LinkedIn__OAuthBaseUrl` | No | Defaults to `https://www.linkedin.com`. |
| `LinkedIn:TokenStorage:ConnectionString` | — | `LinkedIn__TokenStorage__ConnectionString` | No | Azure Table Storage connection string. Hardcoded to Azurite in AppHost. |
| `LinkedIn:TokenStorage:TableName` | `linkedin-token-storage-table-name` | `LinkedIn__TokenStorage__TableName` | No | Defaults to `linkedintokens`. |

#### GitHub

| Config Key | Aspire Parameter | Environment Variable | Required | Description |
|---|---|---|---|---|
| `GitHub:ClientId` | `github-client-id` | `GitHub__ClientId` | Yes | GitHub OAuth application client ID. |
| `GitHub:ClientSecret` | `github-client-secret` | `GitHub__ClientSecret` | Yes | GitHub OAuth application client secret. |
| `GitHub:ApiBaseUrl` | `github-api-base-url` | `GitHub__ApiBaseUrl` | No | Defaults to `https://api.github.com`. |
| `GitHub:OAuthBaseUrl` | `github-oauth-base-url` | `GitHub__OAuthBaseUrl` | No | Defaults to `https://github.com`. |
| `GitHub:TokenStorage:ConnectionString` | — | `GitHub__TokenStorage__ConnectionString` | No | Azure Table Storage connection string. Hardcoded to Azurite in AppHost. |
| `GitHub:TokenStorage:TableName` | `github-token-storage-table-name` | `GitHub__TokenStorage__TableName` | No | Defaults to `githubtokens`. |
| `GitHub:RepoStorage:ConnectionString` | — | `GitHub__RepoStorage__ConnectionString` | No | Azure Table Storage connection string. Hardcoded to Azurite in AppHost. |
| `GitHub:RepoStorage:TableName` | `github-repo-storage-table-name` | `GitHub__RepoStorage__TableName` | No | Defaults to `githubrepositories`. |

### API Authentication

| Config Key | Aspire Parameter | Environment Variable | Required | Description |
|---|---|---|---|---|
| `Auth:ApiKey` | `auth-api-key` | `Auth__ApiKey` | Yes | The API key clients must send in the `X-Api-Key` header. |

### Blog Promotion (RSS)

| Config Key | Aspire Parameter | Environment Variable | Required | Default | Description |
|---|---|---|---|---|---|
| `BlogPromotion:FeedUrl` | `blog-promotion-feed-url` | `BlogPromotion__FeedUrl` | Yes | — | Absolute URL of the RSS feed. |
| `BlogPromotion:RecentDaysWindow` | `blog-promotion-recent-days-window` | `BlogPromotion__RecentDaysWindow` | No | `7` | Days to look back for new entries. |
| `BlogPromotion:EnableReminderPosts` | `blog-promotion-enable-reminder-posts` | `BlogPromotion__EnableReminderPosts` | No | `false` | Post reminders for previously promoted entries. |
| `BlogPromotion:ReminderDelayHours` | `blog-promotion-reminder-delay-hours` | `BlogPromotion__ReminderDelayHours` | No | `24` | Hours before a reminder is eligible. |
| `BlogPromotion:TableStorage:ConnectionString` | — | `BlogPromotion__TableStorage__ConnectionString` | No | — | Azure Table Storage connection string. Hardcoded to Azurite in AppHost. |
| `BlogPromotion:TableStorage:AccountEndpoint` | — | — | No | — | Azure Table Storage account endpoint (when ConnectionString is not set). |
| `BlogPromotion:TableStorage:TableName` | `blog-promotion-table-storage-table-name` | `BlogPromotion__TableStorage__TableName` | No | `blogpostpromotions` | Table name for promotion tracking records. |
| `BlogPromotion:TableStorage:PartitionKey` | `blog-promotion-table-storage-partition-key` | `BlogPromotion__TableStorage__PartitionKey` | No | `blog-promotion` | Partition key for promotion records. |

### Scheduled Social Posts

Scheduled posts are stored in Azure Table Storage and processed on-demand via `/api/social-posts/scheduled/process`. At least one storage option must be configured.

| Config Key | Aspire Parameter | Environment Variable | Required | Default | Description |
|---|---|---|---|---|---|
| `ScheduledSocialPost:MaxBatchSize` | `scheduled-social-post-max-batch-size` | `ScheduledSocialPost__MaxBatchSize` | No | `100` | Max posts to process per request (1–1000). |
| `ScheduledSocialPost:TableStorage:ConnectionString` | — | `ScheduledSocialPost__TableStorage__ConnectionString` | No* | — | Azure Table Storage connection string. **See note below.** |
| `ScheduledSocialPost:TableStorage:AccountEndpoint` | — | `ScheduledSocialPost__TableStorage__AccountEndpoint` | No* | — | Azure Table Storage account endpoint (e.g., `https://myaccount.table.core.windows.net`). Use with managed identity. |
| `ScheduledSocialPost:TableStorage:TableName` | `scheduled-social-post-table-storage-table-name` | `ScheduledSocialPost__TableStorage__TableName` | No | `scheduledsocialposts` | Table name for scheduled post records. |
| `ScheduledSocialPost:TableStorage:PartitionKey` | `scheduled-social-post-table-storage-partition-key` | `ScheduledSocialPost__TableStorage__PartitionKey` | No | `scheduled-social-post` | Partition key for scheduled post records. |

**Storage Configuration Note:**  
Either `ConnectionString` **or** `AccountEndpoint` must be set. **In production, you can reuse the same storage account connection string across LinkedIn token storage, blog promotion, and scheduled posts** — they use different table names, so no conflict occurs. For example, if you already have `LinkedIn__TokenStorage__ConnectionString` configured, you can set:
```
ScheduledSocialPost__TableStorage__ConnectionString = (same value as LinkedIn__TokenStorage__ConnectionString)
```
- **Option A (Shared Connection String):** Set `ScheduledSocialPost__TableStorage__ConnectionString` to your Azure Storage account connection string (simplest for single-account deployments).
- **Option B (Managed Identity):** Set `ScheduledSocialPost__TableStorage__AccountEndpoint` and configure managed identity on your Azure resource.
- **Table Name Rule:** `ScheduledSocialPost__TableStorage__TableName` must be 3-63 chars, start with a letter, and contain letters/numbers only. Use lowercase values such as `scheduledsocialposts`.

### NASA APOD

| Config Key | Aspire Parameter | Environment Variable | Required | Default | Description |
|---|---|---|---|---|---|
| `NasaApod:ApiKey` | `nasa-apod-api-key` | `NasaApod__ApiKey` | Yes | — | NASA API key. Register free at <https://api.nasa.gov/>. |
| `NasaApod:BaseUrl` | — | — | No | `https://api.nasa.gov/planetary/apod` | NASA APOD API base URL. Not mapped in AppHost. |

### NASA GIBS

No API key required — NASA GIBS is publicly accessible.

| Config Key | Aspire Parameter | Environment Variable | Required | Default | Description |
|---|---|---|---|---|---|
| `NasaGibs:BaseUrl` | `gibs-base-url` | `NasaGibs__BaseUrl` | No | `https://wvs.earthdata.nasa.gov/api/v1/snapshot` | GIBS Worldview Snapshot API base URL. |
| `NasaGibs:DefaultLayer` | `gibs-default-layer` | `NasaGibs__DefaultLayer` | No | `MODIS_Terra_CorrectedReflectance_TrueColor` | Default imagery layer. |
| `NasaGibs:BboxSouth` | `gibs-bbox-south` | `NasaGibs__BboxSouth` | No | `38.40` | Southern boundary (latitude). |
| `NasaGibs:BboxWest` | `gibs-bbox-west` | `NasaGibs__BboxWest` | No | `-84.82` | Western boundary (longitude). |
| `NasaGibs:BboxNorth` | `gibs-bbox-north` | `NasaGibs__BboxNorth` | No | `42.32` | Northern boundary (latitude). |
| `NasaGibs:BboxEast` | `gibs-bbox-east` | `NasaGibs__BboxEast` | No | `-80.52` | Eastern boundary (longitude). |
| `NasaGibs:ImageWidth` | `gibs-image-width` | `NasaGibs__ImageWidth` | No | `1024` | Snapshot image width in pixels. |
| `NasaGibs:ImageHeight` | `gibs-image-height` | `NasaGibs__ImageHeight` | No | `768` | Snapshot image height in pixels. |

## Production Notes

### HTTPS Requirement

Always call production endpoints using `https://`. Using `http://` may result in redirect behavior where clients retry with the wrong method and receive `405 Method Not Allowed`.

### Scheduled Posts Configuration

Before deploying to production, ensure **at least one** of the following is configured:
- `ScheduledSocialPost__TableStorage__ConnectionString` — Can reuse your existing Azure Storage account (same as LinkedIn or Blog Promotion if using one storage account)
- `ScheduledSocialPost__TableStorage__AccountEndpoint` — Set to your table storage account endpoint and configure managed identity

**Unified Storage Pattern (Recommended):**  
If you have a single Azure Storage account for all features:
```bash
# Set the same connection string for all table storage features
export LinkedIn__TokenStorage__ConnectionString="DefaultEndpointsProtocol=..."
export BlogPromotion__TableStorage__ConnectionString="DefaultEndpointsProtocol=..."  # Same
export ScheduledSocialPost__TableStorage__ConnectionString="DefaultEndpointsProtocol=..."  # Same
```
Each feature uses its own table name (`linkedintokens`, `blogpostpromotions`, `scheduledsocialposts`), so there's no conflict.

**Error:** If neither `ConnectionString` nor `AccountEndpoint` is set, the API will fail at startup with `OptionsValidationException: ScheduledSocialPost:TableStorage:ConnectionString or AccountEndpoint must be configured.`

If scheduled-post requests fail with a table initialization error, verify `ScheduledSocialPost__TableStorage__TableName` is lowercase alphanumeric (example: `scheduledsocialposts`) and restart the app after updating app settings.

Some production environments restrict table creation at runtime. In that case, pre-create the scheduled-post table and grant the app identity table data-plane permissions before calling scheduled endpoints.

### LinkedIn Rollout Checklist

1. Add LinkedIn configuration values to deployment settings before enabling LinkedIn in client requests.
2. Complete the OAuth flow by visiting `/api/linkedin/auth` in a browser and authorizing the application.
3. Retrieve your profile URN from `/api/linkedin/profile` and set `LinkedIn:AuthorUrn` to `urn:li:person:<sub>`.
4. Validate with a `linkedin`-only request first, then test mixed-platform requests.
5. Confirm mixed-platform failure handling returns `207` when LinkedIn fails and another platform succeeds.

### Security

- Use least-privilege LinkedIn app permissions.
- Rotate access tokens regularly.
- Never log or return token values in API responses.
- Store all secrets in Aspire AppHost User Secrets during development and in secure deployment configuration for production.

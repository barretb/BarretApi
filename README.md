# BarretApi

A cross-platform social-media posting API built with .NET 10, Aspire, and FastEndpoints. Publish to **Bluesky**, **Mastodon**, and **LinkedIn** from a single request, and automate blog promotion from an RSS feed.

## Table of Contents

- [Project Structure](#project-structure)
- [Getting Started](#getting-started)
- [Authentication](#authentication)
- [API Endpoints](#api-endpoints)
  - [POST /api/social-posts — Create Social Post (JSON)](#post-apisocial-posts--create-social-post-json)
  - [POST /api/social-posts/upload — Create Social Post (Multipart Upload)](#post-apisocial-postsupload--create-social-post-multipart-upload)
  - [POST /api/social-posts/rss-promotion — Trigger RSS Blog Promotion](#post-apisocial-postsrss-promotion--trigger-rss-blog-promotion)
  - [GET /api/linkedin/auth — Initiate LinkedIn OAuth Flow](#get-apilinkedinauth--initiate-linkedin-oauth-flow)
  - [GET /api/linkedin/auth/callback — LinkedIn OAuth Callback](#get-apilinkedinauthcallback--linkedin-oauth-callback)
  - [GET /api/linkedin/profile — Get LinkedIn Profile](#get-apilinkedinprofile--get-linkedin-profile)
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
| `images` | `object[]` | No | Up to 4 image references. |
| `images[].url` | `string` | Yes | Absolute URL of the image. |
| `images[].altText` | `string` | Yes | Alt text for the image (max 1,500 chars). |

#### Example — Post to All Platforms

```bash
curl -X POST "https://<your-api-host>/api/social-posts" \
  -H "X-Api-Key: <api-key>" \
  -H "Content-Type: application/json" \
  -d '{
    "text": "Hello from BarretApi! #dotnet #aspire",
    "hashtags": ["webapi"],
    "platforms": ["linkedin", "bluesky", "mastodon"],
    "images": [
      {
        "url": "https://example.com/photo.jpg",
        "altText": "A descriptive alt text for the image"
      }
    ]
  }'
```

#### Example — Post to a Single Platform

```bash
curl -X POST "https://<your-api-host>/api/social-posts" \
  -H "X-Api-Key: <api-key>" \
  -H "Content-Type: application/json" \
  -d '{
    "text": "Just shipped a new feature!",
    "platforms": ["bluesky"]
  }'
```

#### Example — Text-Only Post with Hashtags

```bash
curl -X POST "https://<your-api-host>/api/social-posts" \
  -H "X-Api-Key: <api-key>" \
  -H "Content-Type: application/json" \
  -d '{
    "text": "Exploring the new .NET 10 features today",
    "hashtags": ["dotnet", "csharp", "aspire"],
    "platforms": ["bluesky", "mastodon"]
  }'
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
| `images` | `file[]` | No | Up to 4 image files (JPEG, PNG, GIF, WebP; max 1 MB each). |
| `altTexts` | `string[]` | Yes (if images) | One alt text per image, matched by position (max 1,500 chars each). |

#### Example — Upload with Images

```bash
curl -X POST "https://<your-api-host>/api/social-posts/upload" \
  -H "X-Api-Key: <api-key>" \
  -F "text=Check out this screenshot!" \
  -F "hashtags=dotnet" \
  -F "hashtags=aspire" \
  -F "platforms=bluesky" \
  -F "platforms=mastodon" \
  -F "images=@./screenshot.png" \
  -F "altTexts=Screenshot of the new dashboard"
```

#### Example — Multiple Images

```bash
curl -X POST "https://<your-api-host>/api/social-posts/upload" \
  -H "X-Api-Key: <api-key>" \
  -F "text=Before and after comparison" \
  -F "platforms=bluesky" \
  -F "platforms=linkedin" \
  -F "images=@./before.jpg" \
  -F "images=@./after.jpg" \
  -F "altTexts=Before the refactor" \
  -F "altTexts=After the refactor"
```

#### Response

Same response shape and status codes as [`POST /api/social-posts`](#post-apisocial-posts--create-social-post-json).

#### Image Constraints

- **Max 4 images** per request.
- Allowed content types: `image/jpeg`, `image/png`, `image/gif`, `image/webp`.
- **Max 1 MB** per image.
- Alt text count **must** match image count.
- Alt texts must not be blank and must not exceed 1,500 characters.

---

### POST /api/social-posts/rss-promotion — Trigger RSS Blog Promotion

Reads the configured RSS feed, posts newly published entries first, then posts any eligible reminder entries. Tracks which entries have been posted using Azure Table Storage to avoid duplicates.

| Detail | Value |
|---|---|
| **Auth** | `X-Api-Key` header |
| **Content-Type** | None (no request body) |

#### Example

```bash
curl -X POST "https://<your-api-host>/api/social-posts/rss-promotion" \
  -H "X-Api-Key: <api-key>"
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

```bash
curl "https://<your-api-host>/api/linkedin/auth" \
  -H "Accept: application/json"
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

```bash
curl "https://<your-api-host>/api/linkedin/profile"
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

## Configuration

All configuration is managed through the **Aspire AppHost** project. Do not add `appsettings.json` entries in other projects. Use **User Secrets** in the AppHost for sensitive values during development.

### Platform Credentials

#### Bluesky

| Key | Required | Description |
|---|---|---|
| `Bluesky:Handle` | Yes | Your Bluesky handle (e.g., `your-handle.bsky.social`). |
| `Bluesky:AppPassword` | Yes | App password generated from Bluesky settings. |
| `Bluesky:ServiceUrl` | No | Defaults to `https://bsky.social`. |

#### Mastodon

| Key | Required | Description |
|---|---|---|
| `Mastodon:InstanceUrl` | Yes | Your Mastodon instance URL (e.g., `https://mastodon.social`). |
| `Mastodon:AccessToken` | Yes | Access token from your Mastodon application. |

#### LinkedIn

| Key | Required | Description |
|---|---|---|
| `LinkedIn:ClientId` | Yes | OAuth application client ID. |
| `LinkedIn:ClientSecret` | Yes | OAuth application client secret. |
| `LinkedIn:AuthorUrn` | Yes | URN of the posting author (e.g., `urn:li:person:abc123`). |
| `LinkedIn:ApiBaseUrl` | No | Defaults to `https://api.linkedin.com`. |
| `LinkedIn:OAuthBaseUrl` | No | Defaults to `https://www.linkedin.com`. |
| `LinkedIn:TokenStorage:ConnectionString` | No | Azure Table Storage connection string (uses Azurite in dev). |
| `LinkedIn:TokenStorage:TableName` | No | Defaults to `linkedintokens`. |

### API Authentication

| Key | Required | Description |
|---|---|---|
| `Auth:ApiKey` | Yes | The API key clients must send in the `X-Api-Key` header. |

### Blog Promotion (RSS)

| Key | Required | Default | Description |
|---|---|---|---|
| `BlogPromotion:FeedUrl` | Yes | — | Absolute URL of the RSS feed. |
| `BlogPromotion:RecentDaysWindow` | No | `7` | Number of days to look back for new entries. |
| `BlogPromotion:EnableReminderPosts` | No | `false` | Whether to post reminders for previously promoted entries. |
| `BlogPromotion:ReminderDelayHours` | No | `24` | Hours after the initial post before a reminder is eligible. |
| `BlogPromotion:TableStorage:ConnectionString` | No | — | Azure Table Storage connection string (uses Azurite in dev). |
| `BlogPromotion:TableStorage:AccountEndpoint` | No | — | Azure Table Storage account endpoint (used when ConnectionString is not set). |
| `BlogPromotion:TableStorage:TableName` | No | `blogpostpromotions` | Table name for promotion tracking records. |
| `BlogPromotion:TableStorage:PartitionKey` | No | `blog-promotion` | Partition key for promotion records. |

## Production Notes

### HTTPS Requirement

Always call production endpoints using `https://`. Using `http://` may result in redirect behavior where clients retry with the wrong method and receive `405 Method Not Allowed`.

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

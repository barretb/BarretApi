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
  - [POST /api/social-posts/nasa-apod — Post NASA APOD to Social Platforms](#post-apisocial-postsnasa-apod--post-nasa-apod-to-social-platforms)
  - [POST /api/social-posts/ohio-satellite — Post Ohio Satellite Image](#post-apisocial-postsohio-satellite--post-ohio-satellite-image)
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

```http
POST /api/social-posts/rss-promotion
```

No request body. The endpoint reads its RSS feed configuration from the server.

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

### POST /api/social-posts/ohio-satellite — Post Ohio Satellite Image

Fetches a satellite image of Ohio from NASA GIBS (Global Imagery Browse Services) and posts it to selected social media platforms. The image is captured from the Worldview Snapshot API using configurable satellite imagery layers and date.

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
POST /api/social-posts/ohio-satellite
```

```json
{}
```

#### Example — Post a Specific Date and Layer

```http
POST /api/social-posts/ohio-satellite
```

```json
{
  "date": "2026-02-14",
  "layer": "VIIRS_SNPP_CorrectedReflectance_TrueColor",
  "platforms": ["bluesky", "mastodon"]
}
```

#### Response — 200 OK (All Platforms Succeeded)

```json
{
  "date": "2026-03-15",
  "layer": "MODIS_Terra_CorrectedReflectance_TrueColor",
  "worldviewUrl": "https://worldview.earthdata.nasa.gov/?v=-84.82,38.40,-80.52,42.32&l=MODIS_Terra_CorrectedReflectance_TrueColor&t=2026-03-15",
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
- **Image Attachment**: The GIBS snapshot (JPEG, typically 80–400 KB at 1024×768) is attached directly to the social post.
- **Post Caption**: Includes the date, layer name, a Worldview link for interactive exploration, and NASA GIBS acknowledgement.
- **Hashtags**: Posts include `#Ohio`, `#satellite`, `#NASA`, and `#EarthObservation`.
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
| **400** | Request validation failed (invalid date, unsupported layer, invalid platform). |
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

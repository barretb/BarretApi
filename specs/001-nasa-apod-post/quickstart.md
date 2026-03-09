# Quickstart: NASA APOD Social Posting

**Feature Branch**: `001-nasa-apod-post`

## Prerequisites

- .NET 10.0 SDK installed
- Aspire 13 workload installed (`dotnet workload install aspire`)
- NASA API key (register free at <https://api.nasa.gov/>)
- At least one social platform configured (Bluesky, Mastodon, or LinkedIn)

## Setup

### 1. Add NASA API Key to AppHost Secrets

```bash
cd src/BarretApi.AppHost
dotnet user-secrets set "NasaApod:ApiKey" "YOUR_NASA_API_KEY"
```

### 2. Build and Run

```bash
# From repository root
dotnet build BarretApi.slnx
dotnet run --project src/BarretApi.AppHost/BarretApi.AppHost.csproj
```

The Aspire dashboard will open, showing the API project running.

## Usage

### Post Today's APOD to All Platforms

```bash
curl -X POST http://localhost:5000/api/social-posts/nasa-apod \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: YOUR_API_KEY" \
  -d '{}'
```

### Post a Specific Date's APOD to Selected Platforms

```bash
curl -X POST http://localhost:5000/api/social-posts/nasa-apod \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: YOUR_API_KEY" \
  -d '{
    "date": "2026-02-14",
    "platforms": ["bluesky", "mastodon"]
  }'
```

### Example Response (200 OK)

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

### Example Response (207 Partial Success)

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
      "success": false,
      "error": "Unauthorized: Invalid access token",
      "errorCode": "AUTH_FAILED"
    }
  ],
  "postedAt": "2026-03-08T15:30:00Z"
}
```

### Error Responses

| Status | Cause | Example |
|--------|-------|---------|
| 400 | Invalid date or unknown platform | `{"statusCode":400,"message":"Validation failed","errors":{"date":["Date must not be in the future."]}}` |
| 401 | Missing or invalid `X-Api-Key` | Standard 401 response |
| 422 | NASA API error (unreachable, rate limited, etc.) | `{"statusCode":422,"message":"Failed to fetch APOD from NASA API","error":"NASA API returned status 429"}` |
| 502 | All platforms failed | Same response shape as 200 but all results have `success: false` |

## Post Text Format

The post text is constructed as:

```text
{APOD Title}
{HD Image URL (if available, otherwise standard URL)}
Credit: {Copyright holder (if copyrighted)}
```

Example:

```text
The Aurora Tree
https://apod.nasa.gov/apod/image/2603/AuroraTree_Wallace_2048.jpg
Credit: Alyn Wallace
```

## Image Handling

- **Source**: Standard-resolution image (`url` field, typically ~960px wide)
- **Alt text**: The full APOD `explanation` from the NASA API (truncated per platform limits)
- **Resizing**: If the image exceeds a platform's max size, it is automatically resized to JPEG with quality-first reduction
- **Videos**: When the APOD is a video, the thumbnail is used as the image (if available); otherwise text-only post

## Running Tests

```bash
dotnet test BarretApi.slnx
```

Test projects with NASA APOD coverage:

- `tests/BarretApi.Core.UnitTests/` — `NasaApodPostService_PostAsync_Tests.cs`
- `tests/BarretApi.Api.UnitTests/` — `NasaApodPostEndpoint_HandleAsync_Tests.cs`
- `tests/BarretApi.Infrastructure.UnitTests/` — `NasaApodClient_GetApodAsync_Tests.cs`, `SkiaImageResizer_ResizeAsync_Tests.cs`

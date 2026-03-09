# Quickstart: NASA GIBS Ohio Satellite Image Social Posting

**Feature Branch**: `003-nasa-gibs-post`

## Prerequisites

- .NET 10.0 SDK installed
- Aspire 13 workload installed (`dotnet workload install aspire`)
- At least one social platform configured (Bluesky, Mastodon, or LinkedIn)
- **No NASA API key required** — the GIBS Worldview Snapshot API is public

## Setup

### 1. Build and Run

```bash
# From repository root
dotnet build BarretApi.slnx
dotnet run --project src/BarretApi.AppHost/BarretApi.AppHost.csproj
```

The Aspire dashboard will open, showing the API project running.

No additional secrets or configuration are needed for the GIBS feature — sensible defaults (Ohio bounding box, MODIS Terra layer, 1024×768 image dimensions) are built into the `NasaGibsOptions` class and can be optionally overridden via AppHost environment variables.

## Usage

### Post Yesterday's Ohio Satellite Image to All Platforms

```http
POST /api/social-posts/ohio-satellite HTTP/1.1
Host: localhost:5000
Content-Type: application/json
X-Api-Key: YOUR_API_KEY

{}
```

### Post a Specific Date's Image with a Chosen Layer

```http
POST /api/social-posts/ohio-satellite HTTP/1.1
Host: localhost:5000
Content-Type: application/json
X-Api-Key: YOUR_API_KEY
```

```json
{
  "date": "2026-02-14",
  "layer": "VIIRS_SNPP_CorrectedReflectance_TrueColor",
  "platforms": ["bluesky", "mastodon"]
}
```

### Example Response (200 OK)

```json
{
  "date": "2026-03-08",
  "layer": "MODIS_Terra_CorrectedReflectance_TrueColor",
  "worldviewUrl": "https://worldview.earthdata.nasa.gov/?v=-84.82,38.40,-80.52,42.32&l=MODIS_Terra_CorrectedReflectance_TrueColor&t=2026-03-08",
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
  "postedAt": "2026-03-08T15:30:00Z"
}
```

### Example Response (207 Partial Success)

```json
{
  "date": "2026-03-08",
  "layer": "MODIS_Terra_CorrectedReflectance_TrueColor",
  "worldviewUrl": "https://worldview.earthdata.nasa.gov/?v=-84.82,38.40,-80.52,42.32&l=MODIS_Terra_CorrectedReflectance_TrueColor&t=2026-03-08",
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
| 400 | Invalid date, unsupported layer, or unknown platform | `{"statusCode":400,"message":"Validation failed","errors":{"layer":["Layer 'INVALID' is not supported."]}}` |
| 401 | Missing or invalid `X-Api-Key` | Standard 401 response |
| 422 | NASA GIBS API returned an error | `{"statusCode":422,"message":"Failed to fetch snapshot from NASA GIBS","error":"ServiceException: LayerNotDefined"}` |
| 502 | All platforms failed | Same response shape as 200 but all results have `success: false` |

## Supported Imagery Layers

| Layer Identifier | Instrument | Platform | Data Available Since |
|-----------------|------------|----------|---------------------|
| `MODIS_Terra_CorrectedReflectance_TrueColor` | MODIS | Terra | 2000-02-24 |
| `MODIS_Aqua_CorrectedReflectance_TrueColor` | MODIS | Aqua | 2002-07-04 |
| `VIIRS_SNPP_CorrectedReflectance_TrueColor` | VIIRS | Suomi NPP | 2015-11-24 |
| `VIIRS_NOAA20_CorrectedReflectance_TrueColor` | VIIRS | NOAA-20 | 2017-12-01 |
| `VIIRS_NOAA21_CorrectedReflectance_TrueColor` | VIIRS | NOAA-21 | 2024-01-17 |

The default layer is `MODIS_Terra_CorrectedReflectance_TrueColor` — it has the longest data record and is the most widely recognized true-color satellite imagery layer.

## Post Text Format

The post text is constructed as:

```text
Satellite view of Ohio — March 8, 2026
Imagery: MODIS_Terra_CorrectedReflectance_TrueColor via NASA GIBS
Explore: https://worldview.earthdata.nasa.gov/?v=-84.82,38.40,-80.52,42.32&l=MODIS_Terra_CorrectedReflectance_TrueColor&t=2026-03-08

Imagery: NASA GIBS #Ohio #satellite #NASA #EarthObservation
```

- The Worldview link opens an interactive view of Ohio for the same date
- Text is automatically shortened per platform character limits
- Hashtags may be trimmed if space is limited

## Image Handling

- **Source**: GIBS Worldview Snapshot API — returns raw JPEG bytes directly (no separate download step)
- **Default dimensions**: 1024×768 pixels
- **Typical size**: 80–400 KB (JPEG) — within all platform limits
- **Alt text**: `"Satellite image of Ohio captured on {date} by {instrument}"`
- **Resizing**: Automatic via SkiaSharp if image exceeds a platform's max size (unlikely at default dimensions)

## Running Tests

```bash
dotnet test BarretApi.slnx
```

Test projects with NASA GIBS coverage:

- `tests/BarretApi.Core.UnitTests/` — `NasaGibsPostService_PostAsync_Tests.cs`
- `tests/BarretApi.Api.UnitTests/` — `OhioSatellitePostEndpoint_HandleAsync_Tests.cs`, `OhioSatellitePostValidator_Tests.cs`
- `tests/BarretApi.Infrastructure.UnitTests/` — `NasaGibsClient_GetSnapshotAsync_Tests.cs`

## Configuration (Optional Overrides)

All settings have sensible defaults. To override, add parameters in the Aspire AppHost:

| Setting | Default | AppHost Environment Variable |
|---------|---------|------------------------------|
| GIBS API Base URL | `https://wvs.earthdata.nasa.gov/api/v1/snapshot` | `NasaGibs__BaseUrl` |
| Default layer | `MODIS_Terra_CorrectedReflectance_TrueColor` | `NasaGibs__DefaultLayer` |
| Ohio bbox south | `38.40` | `NasaGibs__BboxSouth` |
| Ohio bbox west | `-84.82` | `NasaGibs__BboxWest` |
| Ohio bbox north | `42.32` | `NasaGibs__BboxNorth` |
| Ohio bbox east | `-80.52` | `NasaGibs__BboxEast` |
| Image width | `1024` | `NasaGibs__ImageWidth` |
| Image height | `768` | `NasaGibs__ImageHeight` |

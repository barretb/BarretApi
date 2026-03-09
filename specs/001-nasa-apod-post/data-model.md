# Data Model: NASA APOD Social Posting

**Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md) | **Research**: [research.md](research.md)

## Entity Overview

```text
                       ┌──────────────────┐
                       │  NasaApodOptions  │ (Configuration)
                       └────────┬─────────┘
                                │ api_key
                ┌───────────────▼───────────────┐
                │       NASA APOD API           │
                │  GET /planetary/apod           │
                └───────────────┬───────────────┘
                                │ JSON response
                       ┌────────▼─────────┐
                       │    ApodEntry      │ (Core Model)
                       └────────┬─────────┘
                                │
              ┌─────────────────┼─────────────────┐
              │                 │                  │
     ┌────────▼────────┐  ┌────▼─────┐  ┌────────▼────────┐
     │   SocialPost     │  │ ImageUrl │  │  ImageData      │
     │  (existing)      │  │(existing)│  │  (existing)     │
     └────────┬────────┘  └──────────┘  └────────┬────────┘
              │                                   │ resize if needed
              │                          ┌────────▼────────┐
              │                          │  IImageResizer   │
              │                          │  (new interface) │
              │                          └─────────────────┘
              │
     ┌────────▼────────┐
     │ SocialPostService│ (existing — fan-out to platforms)
     └────────┬────────┘
              │
     ┌────────▼────────┐
     │ ApodPostResult   │ (Core Model)
     └─────────────────┘
```

## New Entities

### ApodEntry

**Project**: `BarretApi.Core` | **Namespace**: `BarretApi.Core.Models`

Represents a single NASA Astronomy Picture of the Day as returned by the API. This is a domain model used within the Core layer — not the raw API response DTO.

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `Title` | `string` | Yes | APOD title |
| `Date` | `DateOnly` | Yes | APOD date (YYYY-MM-DD) |
| `Explanation` | `string` | Yes | Full-text description (used as image alt text) |
| `Url` | `string` | Yes | Standard-resolution image URL or video embed URL |
| `HdUrl` | `string?` | No | High-definition image URL (null for videos, some older entries) |
| `MediaType` | `ApodMediaType` | Yes | `Image` or `Video` |
| `Copyright` | `string?` | No | Copyright holder (null = public domain / NASA) |
| `ThumbnailUrl` | `string?` | No | Video thumbnail URL (only for video APODs with thumbs requested) |

**Validation Rules**:

- `Title` must not be null or empty.
- `Date` must be between 1995-06-16 and today (inclusive).
- `Url` must be a valid absolute HTTP(S) URL.
- `MediaType` must be a recognized value.

**State Transitions**: None — this is an immutable value object.

### ApodMediaType

**Project**: `BarretApi.Core` | **Namespace**: `BarretApi.Core.Models`

Enum representing the APOD media type.

| Value | Description |
|-------|-------------|
| `Image` | Static image (JPEG, PNG, etc.) |
| `Video` | Video embed (typically YouTube) |

### ApodPostResult

**Project**: `BarretApi.Core` | **Namespace**: `BarretApi.Core.Models`

Result of posting an APOD to social platforms.

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `ApodEntry` | `ApodEntry` | Yes | The APOD that was posted |
| `PlatformResults` | `IReadOnlyList<PlatformPostResult>` | Yes | Per-platform results (existing model) |
| `ImageAttached` | `bool` | Yes | Whether an image was attached to the post |
| `ImageResized` | `bool` | Yes | Whether the image was resized before posting |

### NasaApodOptions

**Project**: `BarretApi.Core` | **Namespace**: `BarretApi.Core.Configuration`

Configuration for the NASA APOD API client.

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `ApiKey` | `string` | Yes | NASA API key (from Aspire AppHost secrets) |
| `BaseUrl` | `string` | No | API base URL (default: `https://api.nasa.gov/planetary/apod`) |

## New Interfaces

### INasaApodClient

**Project**: `BarretApi.Core` | **Namespace**: `BarretApi.Core.Interfaces`

Abstracts communication with the NASA APOD API. Implementation lives in `BarretApi.Infrastructure`.

| Method | Signature | Description |
|--------|-----------|-------------|
| `GetApodAsync` | `Task<ApodEntry> GetApodAsync(DateOnly? date, CancellationToken ct)` | Fetches the APOD for the given date (or today if null). Sends `thumbs=True` always. |

### IImageResizer

**Project**: `BarretApi.Core` | **Namespace**: `BarretApi.Core.Interfaces`

Abstracts image resizing. Implementation (SkiaSharp-based) lives in `BarretApi.Infrastructure`.

| Method | Signature | Description |
|--------|-----------|-------------|
| `ResizeToFit` | `byte[] ResizeToFit(byte[] imageBytes, long maxBytes)` | Resizes image to fit within byte limit. Returns original if already within limit. Output is always JPEG. |

## New Services

### NasaApodPostService

**Project**: `BarretApi.Core` | **Namespace**: `BarretApi.Core.Services`

Orchestrates fetching an APOD and posting it to social platforms. Follows the same pattern as `RssRandomPostService`.

**Dependencies** (via primary constructor):

- `INasaApodClient` — fetches APOD data from NASA API
- `SocialPostService` — handles posting to platforms (existing)
- `ILogger<NasaApodPostService>` — structured logging

**Primary Method**:

```
PostAsync(DateOnly? date, IReadOnlyList<string> platforms, CancellationToken ct)
  → ApodPostResult
```

**Flow**:

1. Call `INasaApodClient.GetApodAsync(date, ct)` to fetch APOD
2. Determine image URL: if `MediaType == Image`, use `Url`; if `Video`, use `ThumbnailUrl` (may be null)
3. Build post text: `{Title}\n{HdUrl ?? Url}` + optional `\nCredit: {Copyright}`
4. Build `SocialPost` with text, image URL (with explanation as alt text), and target platforms
5. Call `SocialPostService.PostAsync(socialPost, ct)`
6. Return `ApodPostResult`

## Existing Entities (Reused As-Is)

| Entity | Project | Relationship |
|--------|---------|-------------|
| `SocialPost` | Core/Models | Constructed by `NasaApodPostService`, consumed by `SocialPostService` |
| `ImageUrl` | Core/Models | Used for the APOD image URL + alt text |
| `ImageData` | Core/Models | Downloaded image bytes (produced by `ImageDownloadService`) |
| `PlatformPostResult` | Core/Models | Per-platform result from `SocialPostService` |
| `PlatformConfiguration` | Core/Models | Platform limits (MaxImageSizeBytes, MaxAltTextLength) |
| `UploadedImage` | Core/Models | Image after platform upload |

## API Layer Models

### NasaApodPostRequest

**Project**: `BarretApi.Api` | **Namespace**: `BarretApi.Api.Features.NasaApod`

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `Date` | `string?` | No | APOD date in YYYY-MM-DD format (defaults to today) |
| `Platforms` | `List<string>?` | No | Target platforms (defaults to all configured) |

### NasaApodPostResponse

**Project**: `BarretApi.Api` | **Namespace**: `BarretApi.Api.Features.NasaApod`

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `Title` | `string` | Yes | APOD title |
| `Date` | `string` | Yes | APOD date |
| `MediaType` | `string` | Yes | "image" or "video" |
| `ImageUrl` | `string` | Yes | URL of the image used |
| `HdImageUrl` | `string?` | No | HD image URL (if available) |
| `Copyright` | `string?` | No | Copyright holder (if any) |
| `ImageAttached` | `bool` | Yes | Whether image was attached |
| `ImageResized` | `bool` | Yes | Whether image was resized |
| `Results` | `List<PlatformResult>` | Yes | Per-platform posting results |
| `PostedAt` | `DateTimeOffset` | Yes | Timestamp of the operation |

## Integration Points: Image Resizing

The image resize path integrates into the existing post flow at the `SocialPostService.PostToPlatformAsync` level. The current flow is:

```text
ImageDownloadService.DownloadAsync → ImageData (byte[])
  → ISocialPlatformClient.UploadImageAsync(ImageData)
    → if image > MaxImageSizeBytes → ❌ CURRENTLY FAILS
```

With the new `IImageResizer`, the `SocialPostService` (or the APOD-specific service) will:

```text
ImageDownloadService.DownloadAsync → ImageData (byte[])
  → IImageResizer.ResizeToFit(imageData.Content, config.MaxImageSizeBytes)
    → resized byte[] (JPEG)
  → ISocialPlatformClient.UploadImageAsync(resizedImageData)
    → ✅ FITS WITHIN LIMIT
```

The resize call happens **per platform** since each has a different size limit. The resizer is a no-op when the image is already under the limit (common case).

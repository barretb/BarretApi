# Data Model: NASA GIBS Ohio Satellite Image Social Posting

**Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md) | **Research**: [research.md](research.md)

## Entity Overview

```text
                       ┌──────────────────┐
                       │  NasaGibsOptions  │ (Configuration)
                       └────────┬─────────┘
                                │ base_url, default_layer,
                                │ supported_layers, ohio_bbox,
                                │ image_width, image_height
                ┌───────────────▼───────────────┐
                │   NASA GIBS Snapshot API       │
                │   GET /api/v1/snapshot         │
                │   (GetSnapshot + WMS params)   │
                └───────────────┬───────────────┘
                                │ raw JPEG bytes
                       ┌────────▼──────────┐
                       │ GibsSnapshotEntry  │ (Core Model)
                       └────────┬──────────┘
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
              │                          │  (existing)      │
              │                          └─────────────────┘
              │
     ┌────────▼────────┐
     │ SocialPostService│ (existing — fan-out to platforms)
     └────────┬────────┘
              │
     ┌────────▼─────────────────┐
     │ OhioSatellitePostResult   │ (Core Model)
     └──────────────────────────┘
```

## New Entities

### GibsSnapshotEntry

**Project**: `BarretApi.Core` | **Namespace**: `BarretApi.Core.Models`

Represents a snapshot image retrieved from the NASA GIBS Worldview Snapshot API. This is a domain model holding the image bytes and associated metadata — not a raw API response DTO.

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `ImageBytes` | `byte[]` | Yes | Raw image content (JPEG) returned by the snapshot API |
| `Date` | `DateOnly` | Yes | Date of the satellite imagery |
| `Layer` | `string` | Yes | GIBS layer identifier used (e.g., `MODIS_Terra_CorrectedReflectance_TrueColor`) |
| `Width` | `int` | Yes | Requested image width in pixels |
| `Height` | `int` | Yes | Requested image height in pixels |
| `ContentType` | `string` | Yes | MIME type of the image (e.g., `image/jpeg`) |

**Validation Rules**:

- `ImageBytes` must not be null or empty.
- `Date` must not be in the future and not before the layer's earliest available date.
- `Layer` must be one of the supported layer identifiers.
- `Width` and `Height` must be positive integers.

**State Transitions**: None — this is an immutable value object.

**Implementation**: Sealed `record` with positional properties.

```csharp
public sealed record GibsSnapshotEntry(
    byte[] ImageBytes,
    DateOnly Date,
    string Layer,
    int Width,
    int Height,
    string ContentType);
```

### OhioSatellitePostResult

**Project**: `BarretApi.Core` | **Namespace**: `BarretApi.Core.Models`

Result of posting an Ohio satellite image to social platforms.

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `Date` | `DateOnly` | Yes | Date of the satellite imagery that was posted |
| `Layer` | `string` | Yes | GIBS layer identifier used |
| `WorldviewUrl` | `string` | Yes | NASA Worldview interactive link for the same view |
| `ImageWidth` | `int` | Yes | Width of the snapshot image in pixels |
| `ImageHeight` | `int` | Yes | Height of the snapshot image in pixels |
| `ImageAttached` | `bool` | Yes | Whether an image was attached to the posts |
| `ImageResized` | `bool` | Yes | Whether the image was resized before posting |
| `PlatformResults` | `IReadOnlyList<PlatformPostResult>` | Yes | Per-platform posting results (existing model) |

**Implementation**: Sealed `record`.

```csharp
public sealed record OhioSatellitePostResult(
    DateOnly Date,
    string Layer,
    string WorldviewUrl,
    int ImageWidth,
    int ImageHeight,
    bool ImageAttached,
    bool ImageResized,
    IReadOnlyList<PlatformPostResult> PlatformResults);
```

### NasaGibsOptions

**Project**: `BarretApi.Core` | **Namespace**: `BarretApi.Core.Configuration`

Strongly-typed configuration for the NASA GIBS Snapshot API client. Populated from Aspire AppHost environment variables.

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `BaseUrl` | `string` | No | `https://wvs.earthdata.nasa.gov/api/v1/snapshot` | GIBS Snapshot API base URL |
| `DefaultLayer` | `string` | No | `MODIS_Terra_CorrectedReflectance_TrueColor` | Default imagery layer |
| `SupportedLayers` | `string[]` | No | *(see below)* | Allowlist of supported layer identifiers |
| `BboxSouth` | `double` | No | `38.40` | Southern boundary latitude |
| `BboxWest` | `double` | No | `-84.82` | Western boundary longitude |
| `BboxNorth` | `double` | No | `42.32` | Northern boundary latitude |
| `BboxEast` | `double` | No | `-80.52` | Eastern boundary longitude |
| `ImageWidth` | `int` | No | `1024` | Default snapshot image width in pixels |
| `ImageHeight` | `int` | No | `768` | Default snapshot image height in pixels |

**Default Supported Layers**:

```csharp
[
    "MODIS_Terra_CorrectedReflectance_TrueColor",
    "MODIS_Aqua_CorrectedReflectance_TrueColor",
    "VIIRS_SNPP_CorrectedReflectance_TrueColor",
    "VIIRS_NOAA20_CorrectedReflectance_TrueColor",
    "VIIRS_NOAA21_CorrectedReflectance_TrueColor"
]
```

**Implementation**: Standard options class with property defaults.

```csharp
public sealed class NasaGibsOptions
{
    public string BaseUrl { get; set; } = "https://wvs.earthdata.nasa.gov/api/v1/snapshot";
    public string DefaultLayer { get; set; } = "MODIS_Terra_CorrectedReflectance_TrueColor";
    public string[] SupportedLayers { get; set; } =
    [
        "MODIS_Terra_CorrectedReflectance_TrueColor",
        "MODIS_Aqua_CorrectedReflectance_TrueColor",
        "VIIRS_SNPP_CorrectedReflectance_TrueColor",
        "VIIRS_NOAA20_CorrectedReflectance_TrueColor",
        "VIIRS_NOAA21_CorrectedReflectance_TrueColor"
    ];
    public double BboxSouth { get; set; } = 38.40;
    public double BboxWest { get; set; } = -84.82;
    public double BboxNorth { get; set; } = 42.32;
    public double BboxEast { get; set; } = -80.52;
    public int ImageWidth { get; set; } = 1024;
    public int ImageHeight { get; set; } = 768;
}
```

### Layer Start Dates (Validation Constants)

Used by the validator and service to reject dates before a layer's earliest imagery:

| Layer | Earliest Date |
|-------|--------------|
| `MODIS_Terra_CorrectedReflectance_TrueColor` | 2000-02-24 |
| `MODIS_Aqua_CorrectedReflectance_TrueColor` | 2002-07-04 |
| `VIIRS_SNPP_CorrectedReflectance_TrueColor` | 2015-11-24 |
| `VIIRS_NOAA20_CorrectedReflectance_TrueColor` | 2017-12-01 |
| `VIIRS_NOAA21_CorrectedReflectance_TrueColor` | 2024-01-17 |

These will be defined as a static `Dictionary<string, DateOnly>` within the `NasaGibsOptions` class or a companion constants class.

## New Interfaces

### INasaGibsClient

**Project**: `BarretApi.Core` | **Namespace**: `BarretApi.Core.Interfaces`

Abstracts communication with the NASA GIBS Worldview Snapshot API. Implementation lives in `BarretApi.Infrastructure`.

| Method | Signature | Description |
|--------|-----------|-------------|
| `GetSnapshotAsync` | `Task<GibsSnapshotEntry> GetSnapshotAsync(string layer, DateOnly date, CancellationToken ct)` | Fetches a snapshot image of Ohio for the given layer and date. Uses configured bounding box and image dimensions. Throws if the API returns an XML error response. |

**Key Behaviours**:

- Constructs the Snapshot API URL with `REQUEST=GetSnapshot`, `SERVICE=WMS`, `CRS=EPSG:4326`, `FORMAT=image/jpeg`, and the configured BBOX/dimensions.
- Checks response `Content-Type`: if it contains `xml` or `text`, parses the error message and throws `InvalidOperationException`.
- Returns a `GibsSnapshotEntry` with the raw JPEG bytes and metadata.
- Does **not** inspect image content for blank/empty images (per research decision — accept whatever GIBS returns).

## New Services

### NasaGibsPostService

**Project**: `BarretApi.Core` | **Namespace**: `BarretApi.Core.Services`

Orchestrates fetching a GIBS satellite snapshot of Ohio and posting it to social media platforms. Follows the same pattern as `NasaApodPostService`.

**Dependencies** (via primary constructor → readonly fields):

- `INasaGibsClient _gibsClient` — fetches snapshot from GIBS
- `SocialPostService _socialPostService` — handles posting to platforms (existing)
- `IOptions<NasaGibsOptions> _options` — GIBS configuration
- `ILogger<NasaGibsPostService> _logger` — structured logging

**Primary Method**:

```
virtual async Task<OhioSatellitePostResult> PostAsync(
    DateOnly? date,
    string? layer,
    IReadOnlyList<string> platforms,
    CancellationToken ct)
  → OhioSatellitePostResult
```

**Flow**:

1. Resolve `date` — default to yesterday UTC if null: `DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1))`
2. Resolve `layer` — default to `_options.Value.DefaultLayer` if null
3. Call `_gibsClient.GetSnapshotAsync(layer, date, ct)` to fetch the snapshot
4. Build Worldview URL: `https://worldview.earthdata.nasa.gov/?v={west},{south},{east},{north}&l={layer}&t={date:yyyy-MM-dd}`
5. Build post text:
   ```
   Satellite view of Ohio — {date:MMMM d, yyyy}
   Imagery: {layer} via NASA GIBS
   Explore: {worldviewUrl}

   Imagery: NASA GIBS #Ohio #satellite #NASA #EarthObservation
   ```
6. Build alt text: `"Satellite image of Ohio captured on {date:yyyy-MM-dd} by {instrumentName}"`
7. Build `SocialPost` with text, inline image data (bytes from `GibsSnapshotEntry`), alt text, and target platforms
8. Call `_socialPostService.PostAsync(socialPost, ct)`
9. Return `OhioSatellitePostResult` with metadata and platform results

## Existing Entities (Reused As-Is)

| Entity | Project | Relationship |
|--------|---------|-------------|
| `SocialPost` | Core/Models | Constructed by `NasaGibsPostService`, consumed by `SocialPostService` |
| `ImageUrl` | Core/Models | Not used directly — GIBS returns raw bytes, not a URL to download |
| `ImageData` | Core/Models | Used to wrap the GIBS snapshot bytes for platform upload |
| `PlatformPostResult` | Core/Models | Per-platform result from `SocialPostService` |
| `PlatformConfiguration` | Core/Models | Platform limits (MaxImageSizeBytes, MaxAltTextLength) |
| `UploadedImage` | Core/Models | Image after platform upload |
| `SocialPostService` | Core/Services | Existing fan-out orchestrator for multi-platform posting |
| `IImageResizer` | Core/Interfaces | Existing — resizes images exceeding platform limits |
| `SkiaImageResizer` | Infrastructure/Services | Existing SkiaSharp implementation of `IImageResizer` |

## API Layer Models

### OhioSatellitePostRequest

**Project**: `BarretApi.Api` | **Namespace**: `BarretApi.Api.Features.Nasa`

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `Date` | `string?` | No | Imagery date in YYYY-MM-DD format (defaults to yesterday) |
| `Layer` | `string?` | No | GIBS layer identifier (defaults to configured default layer) |
| `Platforms` | `List<string>?` | No | Target platforms (defaults to all configured) |

### OhioSatellitePostResponse

**Project**: `BarretApi.Api` | **Namespace**: `BarretApi.Api.Features.Nasa`

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `Date` | `string` | Yes | Imagery date (YYYY-MM-DD) |
| `Layer` | `string` | Yes | GIBS layer identifier used |
| `WorldviewUrl` | `string` | Yes | NASA Worldview interactive link |
| `ImageWidth` | `int` | Yes | Snapshot image width in pixels |
| `ImageHeight` | `int` | Yes | Snapshot image height in pixels |
| `ImageAttached` | `bool` | Yes | Whether image was attached to posts |
| `ImageResized` | `bool` | Yes | Whether image was resized for platform limits |
| `Results` | `List<PlatformResult>` | Yes | Per-platform posting results |
| `PostedAt` | `DateTimeOffset` | Yes | Timestamp of the posting operation |

### OhioSatellitePostValidator

**Project**: `BarretApi.Api` | **Namespace**: `BarretApi.Api.Features.Nasa`

Validation rules for `OhioSatellitePostRequest`:

| Rule | Field | Condition |
|------|-------|-----------|
| Date not in the future | `Date` | If provided, must parse to `DateOnly` and not be after today |
| Date not before layer start | `Date` | If provided with a layer, must not be before the layer's earliest imagery date |
| Layer in supported list | `Layer` | If provided, must be one of the configured `SupportedLayers` |
| Platforms are valid | `Platforms` | Each item must be one of: `bluesky`, `mastodon`, `linkedin` |

## Integration Points

### Image Handling

Unlike the APOD feature (which downloads an image from a URL), the GIBS feature receives raw image bytes directly from the Snapshot API response. The flow for GIBS is:

```text
INasaGibsClient.GetSnapshotAsync → GibsSnapshotEntry (byte[])
  → Wrap as ImageData
  → ISocialPlatformClient.UploadImageAsync(ImageData)
    → if image > MaxImageSizeBytes → IImageResizer.ResizeToFit()
  → ✅ POST TO PLATFORM
```

The resizer is typically a no-op for GIBS images — JPEG snapshots at 1024×768 are usually 80–400 KB, well within all platform limits (Bluesky: 1 MB, Mastodon: 16 MB, LinkedIn: 20 MB).

### Worldview URL Construction

The Worldview link uses **longitude,latitude** order (opposite of the WMS BBOX parameter order):

```text
WMS BBOX (EPSG:4326):  minLat,minLon,maxLat,maxLon  → 38.40,-84.82,42.32,-80.52
Worldview ?v= param:   minLon,minLat,maxLon,maxLat   → -84.82,38.40,-80.52,42.32
```

This difference is handled in the `NasaGibsPostService` when constructing the URL.

### Configuration Mapping (Aspire AppHost)

Environment variables mapped from AppHost parameters to `NasaGibsOptions`:

| AppHost Parameter | Environment Variable | Options Property |
|-------------------|---------------------|-----------------|
| `gibs-base-url` | `NasaGibs__BaseUrl` | `BaseUrl` |
| `gibs-default-layer` | `NasaGibs__DefaultLayer` | `DefaultLayer` |
| `gibs-bbox-south` | `NasaGibs__BboxSouth` | `BboxSouth` |
| `gibs-bbox-west` | `NasaGibs__BboxWest` | `BboxWest` |
| `gibs-bbox-north` | `NasaGibs__BboxNorth` | `BboxNorth` |
| `gibs-bbox-east` | `NasaGibs__BboxEast` | `BboxEast` |
| `gibs-image-width` | `NasaGibs__ImageWidth` | `ImageWidth` |
| `gibs-image-height` | `NasaGibs__ImageHeight` | `ImageHeight` |

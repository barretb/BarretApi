# Research: NASA GIBS Ohio Satellite Image Social Posting

**Date**: 2026-03-09
**Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

---

## Topic 1: NASA GIBS Worldview Snapshot API — Endpoint & Parameters

### Snapshot API Base URL

```
https://wvs.earthdata.nasa.gov/api/v1/snapshot
```

- **Protocol**: HTTPS required
- **Authentication**: **None required** — the Worldview Snapshot service is public and does not require an API key or NASA Earthdata login
- **Rate Limiting**: No documented rate limit, but the service is intended for reasonable use (not bulk download). For bulk imagery, NASA recommends using WMTS/GDAL instead.
- **Service Type**: The Worldview Snapshot API is a specialized WMS-like service that composites GIBS tiles into a single image for a given bounding box, layer, date, and dimensions. It is **not** a full OGC WMS implementation.

### Supported Request Type

The Snapshot API supports **only** `REQUEST=GetSnapshot`. It does **not** support `GetCapabilities` or `GetMap`.

Tested: Sending `REQUEST=GetCapabilities` returns:

```xml
<ServiceExceptionReport>
  <ServiceException code="OperationNotSupported">
    REQUEST type not supported
  </ServiceException>
</ServiceExceptionReport>
```

### Complete Parameter Reference

| Parameter | Required | Value | Description |
|-----------|----------|-------|-------------|
| `REQUEST` | Yes | `GetSnapshot` | The only supported request type |
| `SERVICE` | Yes | `WMS` | Must be `WMS` (the snapshot service wraps WMS conventions) |
| `LAYERS` | Yes | Layer identifier | e.g., `MODIS_Terra_CorrectedReflectance_TrueColor` |
| `CRS` | Yes | EPSG code | e.g., `EPSG:4326` (Geographic), `EPSG:3857` (Web Mercator) |
| `BBOX` | Yes | `minLat,minLon,maxLat,maxLon` | Bounding box in CRS units. For EPSG:4326: `lat_south,lon_west,lat_north,lon_east` |
| `FORMAT` | Yes | MIME type | `image/jpeg` or `image/png` |
| `WIDTH` | Yes | Integer pixels | Width of the output image |
| `HEIGHT` | Yes | Integer pixels | Height of the output image |
| `TIME` | No | `YYYY-MM-DD` | Date for daily layers. Omit for non-temporal layers. |
| `TRANSPARENT` | No | `TRUE`/`FALSE` | Transparency support (only meaningful for PNG format) |
| `VERSION` | No | `1.3.0` | WMS version (defaults to 1.3.0) |
| `STYLE` | No | `default` | Layer style |

### Example Request URL

```
https://wvs.earthdata.nasa.gov/api/v1/snapshot?REQUEST=GetSnapshot&SERVICE=WMS&LAYERS=MODIS_Terra_CorrectedReflectance_TrueColor&CRS=EPSG:4326&BBOX=38.40,-84.82,42.32,-80.52&FORMAT=image/jpeg&WIDTH=1024&HEIGHT=768&TIME=2026-03-07
```

### Supported Projections

| Code | Name | BBOX Order |
|------|------|------------|
| `EPSG:4326` | Geographic (lat/lon) | `minLat,minLon,maxLat,maxLon` |
| `EPSG:3857` | Web Mercator | `minX,minY,maxX,maxY` (meters) |
| `EPSG:3413` | Arctic Polar Stereographic | `minX,minY,maxX,maxY` (meters) |
| `EPSG:3031` | Antarctic Polar Stereographic | `minX,minY,maxX,maxY` (meters) |

**Recommendation**: Use `EPSG:4326` for Ohio imagery — simplest to configure with lat/lon bounding box coordinates.

### Supported Output Formats

| Format | MIME Type | Notes |
|--------|-----------|-------|
| JPEG | `image/jpeg` | Smaller file size, no transparency, best for social media |
| PNG | `image/png` | Larger file size, supports transparency |

**Tested**: Both `image/jpeg` and `image/png` return valid images from the snapshot API.

**Recommendation**: Use `image/jpeg` per spec FR-020 for optimal file size and social media compatibility.

### Response Behavior

| Scenario | Response |
|----------|----------|
| Valid request | Returns raw image bytes with appropriate `Content-Type` header |
| Invalid layer name | Returns XML `ServiceExceptionReport` with `code="LayerNotDefined"` |
| Future date (no data) | Returns an image (not an error) — the image is blank/black for JPEG or transparent for PNG |
| Unsupported `REQUEST` type | Returns XML `ServiceExceptionReport` with `code="OperationNotSupported"` |
| Missing required parameter | Returns XML `ServiceExceptionReport` |

**Critical finding**: The API does **not** return an HTTP error status for dates with no data. It returns a 200 OK with a blank image. This means the caller cannot rely on HTTP status codes to detect missing imagery — the image content itself must be inspected (or the date must be validated before the request).

### Error Response Format

```xml
<?xml version='1.0' encoding="UTF-8"?>
<ServiceExceptionReport version="1.3.0">
  <ServiceException code="LayerNotDefined">
    msWMSLoadGetMapParams(): WMS server error. Invalid layer(s) given in the LAYERS parameter.
  </ServiceException>
</ServiceExceptionReport>
```

Error responses have `Content-Type: text/xml` or `application/vnd.ogc.se_xml`, allowing differentiation from successful image responses by checking the response content type.

### .NET HttpClient Integration Pattern

```csharp
public sealed class GibsSnapshotClient(HttpClient httpClient)
{
    private readonly HttpClient _httpClient = httpClient;

    public async Task<byte[]> GetSnapshotAsync(
        string layer,
        string bbox,
        DateOnly date,
        int width = 1024,
        int height = 768,
        string format = "image/jpeg",
        CancellationToken cancellationToken = default)
    {
        var url = $"?REQUEST=GetSnapshot&SERVICE=WMS" +
                  $"&LAYERS={Uri.EscapeDataString(layer)}" +
                  $"&CRS=EPSG:4326" +
                  $"&BBOX={bbox}" +
                  $"&FORMAT={Uri.EscapeDataString(format)}" +
                  $"&WIDTH={width}" +
                  $"&HEIGHT={height}" +
                  $"&TIME={date:yyyy-MM-dd}";

        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        // Check if the response is an error XML instead of an image
        var contentType = response.Content.Headers.ContentType?.MediaType;
        if (contentType is not null &&
            (contentType.Contains("xml") || contentType.Contains("text")))
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"GIBS snapshot returned an error: {errorBody}");
        }

        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }
}
```

Register with `IHttpClientFactory`:

```csharp
services.AddHttpClient<GibsSnapshotClient>(client =>
{
    client.BaseAddress = new Uri("https://wvs.earthdata.nasa.gov/api/v1/snapshot");
    client.Timeout = TimeSpan.FromSeconds(30);
});
```

---

## Topic 2: Ohio Bounding Box (EPSG:4326)

### Bounding Box Coordinates

| Boundary | Value | Description |
|----------|-------|-------------|
| South (min latitude) | **38.40** | Southern border of Ohio |
| West (min longitude) | **-84.82** | Western border of Ohio |
| North (max latitude) | **42.32** | Northern border of Ohio (Lake Erie shoreline) |
| East (max longitude) | **-80.52** | Eastern border of Ohio |

### BBOX Parameter String

```
38.40,-84.82,42.32,-80.52
```

This follows the EPSG:4326 axis order for WMS 1.3.0: `minLat,minLon,maxLat,maxLon`.

### Verification

This bounding box has been tested with the Worldview Snapshot API and confirmed to produce images showing the full state of Ohio with minimal surrounding area. All five tested layers return valid images with this BBOX.

### Approximate Dimensions

- Latitude span: ~3.92° (≈435 km / 270 mi north-south)
- Longitude span: ~4.30° (≈370 km / 230 mi east-west at Ohio's latitude)
- Aspect ratio: approximately 1:1.1 (slightly wider than tall)

### Recommended Image Dimensions

Given the approximately square aspect ratio of Ohio's bounding box:

| Dimension | Width | Height | Aspect Ratio | Notes |
|-----------|-------|--------|--------------|-------|
| Default | **1024** | **768** | 4:3 | Good for social media, per spec |
| Square | **1024** | **1024** | 1:1 | Closer to Ohio's actual shape |
| Widescreen | **1280** | **720** | 16:9 | Better for preview cards |

**Recommendation**: Use 1024×768 as the default per spec, but note that Ohio's shape would be slightly stretched horizontally. A 1024×900 or 1024×1024 dimension would be more geographically accurate. This is a design choice — social media platforms generally display 4:3 or 1:1 better.

### Configuration

Per the project's AGENTS.md conventions, the Ohio bounding box should be stored as configuration in the Aspire AppHost (not hardcoded), allowing future extension to other regions:

```json
{
  "Gibs": {
    "BoundingBox": {
      "South": 38.40,
      "West": -84.82,
      "North": 42.32,
      "East": -80.52
    }
  }
}
```

---

## Topic 3: Available True-Color Satellite Imagery Layers

### Confirmed Working Layers

All five layers below have been tested with the Worldview Snapshot API using Ohio's bounding box and confirmed to return valid imagery:

| Layer Identifier | Instrument | Platform | Resolution | Data Start | Status |
|-----------------|------------|----------|------------|------------|--------|
| `MODIS_Terra_CorrectedReflectance_TrueColor` | MODIS | Terra | 250m | 2000-02-24 | **Active** |
| `MODIS_Aqua_CorrectedReflectance_TrueColor` | MODIS | Aqua | 250m | 2002-07-04 | **Active** |
| `VIIRS_SNPP_CorrectedReflectance_TrueColor` | VIIRS | Suomi NPP | 250m | 2015-11-24 | **Active** |
| `VIIRS_NOAA20_CorrectedReflectance_TrueColor` | VIIRS | NOAA-20 (JPSS-1) | 250m | 2017-12-01 | **Active** |
| `VIIRS_NOAA21_CorrectedReflectance_TrueColor` | VIIRS | NOAA-21 (JPSS-2) | 250m | 2024-01-17 | **Active** |

### Layer Naming Convention

GIBS layer names follow the pattern:

```
{Instrument}_{Platform}_{ScienceParameter}_{ProcessingLevel}_{DataPeriod}_{DataVersion}_{DataLatency}
```

For corrected reflectance true-color layers:
- **Instrument**: `MODIS` or `VIIRS`
- **Platform**: `Terra`, `Aqua`, `SNPP`, `NOAA20`, or `NOAA21`
- **Science Parameter**: `CorrectedReflectance_TrueColor`
- Daily data period is implied (not in the name)

### "Best Available" Layers

GIBS provides "Best Available" composite layers that automatically select the most appropriate version and latency for a given date. The priority order is:

1. Latest Version Standard (STD)
2. Latest Version Near Real-Time (NRT)
3. Previous Version Standard (STD)
4. Previous Version Near Real-Time (NRT)

For access, GIBS recommends the `/best/` endpoint paths:

```
https://gibs.earthdata.nasa.gov/wms/epsg4326/best/wms.cgi
https://gibs.earthdata.nasa.gov/wmts/epsg4326/best/wmts.cgi
```

The Worldview Snapshot API at `wvs.earthdata.nasa.gov` already uses Best Available logic internally.

### Instrument Comparison

| Feature | MODIS (Terra/Aqua) | VIIRS (SNPP/NOAA-20/21) |
|---------|--------------------|------------------------|
| Spatial Resolution | 250m (bands 1-2), 500m (bands 3-7) | 375m (I-bands), 750m (M-bands) |
| Swath Width | 2330 km | 3000 km |
| True-Color Resolution in GIBS | 250m | 250m |
| Overpass Time (ascending) | Terra: ~10:30 AM, Aqua: ~1:30 PM local | SNPP: ~1:30 PM, NOAA-20: ~1:30 PM local |
| Data Availability | Since 2000 (Terra), 2002 (Aqua) | Since 2012+ (SNPP), 2017+ (NOAA-20) |
| Mission Status | Aging (Terra launched 1999, Aqua 2002) | Current primary missions |

**Recommendation**: Default to `MODIS_Terra_CorrectedReflectance_TrueColor` per spec FR-009 — it has the longest data record (since 2000) and is the most widely recognized. VIIRS layers offer good alternatives, especially as MODIS instruments age.

### Default Layer Configuration

The list of supported layers should be a curated, configured set (not dynamically queried from GIBS capabilities):

```csharp
public static readonly string[] SupportedLayers =
[
    "MODIS_Terra_CorrectedReflectance_TrueColor",
    "MODIS_Aqua_CorrectedReflectance_TrueColor",
    "VIIRS_SNPP_CorrectedReflectance_TrueColor",
    "VIIRS_NOAA20_CorrectedReflectance_TrueColor",
    "VIIRS_NOAA21_CorrectedReflectance_TrueColor"
];
```

---

## Topic 4: NASA Worldview Link Format

### Worldview Application Base URL

```
https://worldview.earthdata.nasa.gov/
```

### URL Parameters

The Worldview application uses URL query parameters to encode the map state. Key parameters:

| Parameter | Description | Format | Example |
|-----------|-------------|--------|---------|
| `v` | Viewport extent (visible area) | `minLon,minLat,maxLon,maxLat` | `-84.82,38.40,-80.52,42.32` |
| `l` | Layers (comma-separated) | Layer IDs with optional settings | `MODIS_Terra_CorrectedReflectance_TrueColor` |
| `t` | Date/time | `YYYY-MM-DD` or `YYYY-MM-DDTHH:mm:ssZ` | `2026-03-07` |
| `p` | Projection | `geographic`, `arctic`, `antarctic` | `geographic` |
| `lg` | Show layer list panel | `true`/`false` | `true` |

**Important**: The `v` parameter uses **longitude,latitude** order (not latitude,longitude like the WMS BBOX), and uses the order `minLon,minLat,maxLon,maxLat`.

### Constructing a Worldview Link for Ohio

Template:

```
https://worldview.earthdata.nasa.gov/?v={minLon},{minLat},{maxLon},{maxLat}&l={layer}&t={date}
```

Example for Ohio on 2026-03-07 with MODIS Terra true color:

```
https://worldview.earthdata.nasa.gov/?v=-84.82,38.40,-80.52,42.32&l=MODIS_Terra_CorrectedReflectance_TrueColor&t=2026-03-07
```

### C# Helper Method

```csharp
public static string BuildWorldviewUrl(
    string layer,
    DateOnly date,
    double south,
    double west,
    double north,
    double east)
{
    return $"https://worldview.earthdata.nasa.gov/" +
           $"?v={west},{south},{east},{north}" +
           $"&l={Uri.EscapeDataString(layer)}" +
           $"&t={date:yyyy-MM-dd}";
}
```

### Multiple Layers in Worldview URL

When specifying multiple layers, separate them with commas. Layers are rendered in order (first layer on top):

```
&l=MODIS_Terra_CorrectedReflectance_TrueColor,Reference_Labels_15m,Reference_Features_15m,Coastlines_15m
```

For the social post, including just the primary imagery layer is sufficient. Adding reference layers (labels, coastlines) would enhance the interactive experience when viewers click the link.

---

## Topic 5: Data Availability & Limitations

### Data Latency

| Latency Type | Abbreviation | Availability | Description |
|--------------|-------------|--------------|-------------|
| Near Real-Time | NRT | Within ~3.5 hours of observation | Preliminary processing; may have minor artifacts |
| Standard | STD | Within ~24 hours of observation | Full quality-controlled processing |

**Key implication for the API**: When requesting imagery for "today," much of the image may be empty or missing because:

1. The satellite may not have completed its overpass of Ohio yet
2. NRT data for recent overpasses may still be processing
3. Standard-quality data requires ~24 hours

**Recommendation**: Default to **yesterday's date** (`DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1))`) per spec FR-004, as satellite imagery typically has a 1–2 day processing delay for complete coverage.

### Missing Data Behavior

| Scenario | API Behavior | Image Content |
|----------|-------------|---------------|
| Yesterday's date | Returns image | Usually complete coverage |
| Today's date | Returns image (200 OK) | Partially filled — empty/black regions where data hasn't arrived |
| Far future date (e.g., 2030) | Returns image (200 OK) | Completely blank/black (JPEG) or transparent (PNG) |
| Date before layer's start date | Returns image (200 OK) | Blank/empty |
| Invalid layer name | Returns XML error | N/A — not an image |

**Critical design consideration**: The snapshot API never returns an HTTP error for valid requests with dates that have no data. The response is always a 200 OK with an image. To detect "no data" conditions programmatically, options include:

1. **Content-type checking**: Verify the response is `image/jpeg` or `image/png` (not `text/xml`)
2. **Image size heuristic**: Blank JPEG images tend to be very small (< 10 KB); real imagery is typically 50+ KB
3. **Date validation**: Reject dates before the layer's known start date and future dates before making the request
4. **Accept blank images**: Since cloudy/overcast days can also look mostly white, distinguishing "no data" from "cloudy" may not be worth the complexity

**Recommendation**: Validate the date range before making the request (not in the future, not before layer start date). Accept whatever image GIBS returns without content inspection — the API's purpose is posting satellite imagery, and even partially empty images convey valid information about data coverage.

### Satellite Overpass Times for Ohio

| Satellite | Approximate Equatorial Crossing Time | Ohio Overpass (approximate) |
|-----------|--------------------------------------|----------------------------|
| Terra | 10:30 AM local (descending) | ~10:30 AM – 11:00 AM ET |
| Aqua | 1:30 PM local (ascending) | ~1:30 PM – 2:00 PM ET |
| Suomi NPP | 1:30 PM local (ascending) | ~1:30 PM – 2:00 PM ET |
| NOAA-20 | ~50 min before Suomi NPP | ~12:40 PM – 1:10 PM ET |
| NOAA-21 | ~50 min after Suomi NPP | ~2:20 PM – 2:50 PM ET |

All satellites are in sun-synchronous polar orbits with ~14 daily orbits. Each satellite provides near-complete global coverage every 1-2 days. Most Ohio overpasses occur around midday local time.

### Cloud Cover Considerations

- Ohio's average cloud cover is ~65–70% annually (one of the cloudiest US states)
- True-color imagery will frequently show clouds obscuring the ground
- No programmatic way to filter by cloud cover using the Snapshot API alone
- Per spec edge cases: "Cloud cover is a natural part of satellite imagery and is noted in the post caption"

### image Size Estimates

Based on testing with the Ohio bounding box at 1024×768:

| Format | Typical Size (with data) | Blank/No Data |
|--------|--------------------------|---------------|
| JPEG | 80–400 KB | < 10 KB |
| PNG | 300 KB – 1.5 MB | < 5 KB |

These sizes are well within all social platform limits (Bluesky: 1 MB, Mastodon: 16 MB, LinkedIn: 20 MB). Resizing via SkiaSharp will rarely be needed for JPEG snapshots at this resolution. PNG snapshots may occasionally need resizing for Bluesky.

### GIBS Data Use & Acknowledgement

Per NASA's data use policy:
- GIBS visualizations are freely available for any use
- NASA requests attribution: **"Imagery provided by NASA's Global Imagery Browse Services (GIBS)"**
- This aligns with spec FR-019

GIBS documentation states: "We kindly request that you include a NASA Worldview acknowledgement when sharing GIBS imagery."

---

## Topic 6: Test URL Verification

### Test URL

```
https://wvs.earthdata.nasa.gov/api/v1/snapshot?REQUEST=GetSnapshot&SERVICE=WMS&LAYERS=MODIS_Terra_CorrectedReflectance_TrueColor&CRS=EPSG:4326&BBOX=38.40,-84.82,42.32,-80.52&FORMAT=image/jpeg&WIDTH=1024&HEIGHT=768&TIME=2026-03-07
```

### Test Results

| Test | URL Variation | Result |
|------|--------------|--------|
| **Base URL** (MODIS Terra, JPEG, 2026-03-07) | As above | **Returns valid JPEG image** |
| **VIIRS SNPP** | `LAYERS=VIIRS_SNPP_CorrectedReflectance_TrueColor` | **Returns valid JPEG image** |
| **VIIRS NOAA-20** | `LAYERS=VIIRS_NOAA20_CorrectedReflectance_TrueColor` | **Returns valid JPEG image** |
| **VIIRS NOAA-21** | `LAYERS=VIIRS_NOAA21_CorrectedReflectance_TrueColor` | **Returns valid JPEG image** |
| **MODIS Aqua** | `LAYERS=MODIS_Aqua_CorrectedReflectance_TrueColor` | **Returns valid JPEG image** |
| **PNG format** | `FORMAT=image/png` | **Returns valid PNG image** |
| **Invalid layer** | `LAYERS=INVALID_LAYER_NAME` | **Returns XML error**: `ServiceException code="LayerNotDefined"` |
| **Far future date** | `TIME=2030-01-01` | **Returns image** (blank/black — no error) |

### Error Response Detail (Invalid Layer)

```xml
<?xml version='1.0' encoding="UTF-8"?>
<ServiceExceptionReport version="1.3.0"
  xmlns="http://www.opengis.net/ogc"
  xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
  xsi:schemaLocation="http://www.opengis.net/ogc
    http://schemas.opengis.net/wms/1.3.0/exceptions_1_3_0.xsd">
  <ServiceException code="LayerNotDefined">
    msWMSLoadGetMapParams(): WMS server error. Invalid layer(s) given in the LAYERS parameter.
  </ServiceException>
</ServiceExceptionReport>
```

---

## Summary of Key Decisions

| Decision | Recommendation | Rationale |
|----------|---------------|-----------|
| Snapshot API endpoint | `https://wvs.earthdata.nasa.gov/api/v1/snapshot` | Confirmed working; returns composited imagery |
| Authentication | None required | GIBS services are public |
| Projection | `EPSG:4326` | Simplest for lat/lon bounding box |
| Ohio BBOX (EPSG:4326) | `38.40,-84.82,42.32,-80.52` | Tested and confirmed |
| Output format | `image/jpeg` | Per spec FR-020; smaller files |
| Default dimensions | 1024×768 | Per spec; good social media size |
| Default layer | `MODIS_Terra_CorrectedReflectance_TrueColor` | Per spec FR-009; longest data record |
| Default date | Yesterday (UTC) | Satellite data has 1–2 day processing delay |
| Supported layers | 5 true-color CorrectedReflectance layers | All tested and confirmed working |
| Date validation | Reject future dates and pre-layer-start dates | API returns blank images, not errors |
| Error detection | Check response `Content-Type` for XML vs image | Differentiates real errors from blank imagery |
| Worldview link format | `?v=minLon,minLat,maxLon,maxLat&l=layer&t=date` | Note: lon/lat order differs from WMS BBOX |
| NASA acknowledgement | "Imagery: NASA GIBS" in post text | Per NASA data use guidelines |

## Documentation References

| Resource | URL |
|----------|-----|
| GIBS API Documentation (main) | <https://nasa-gibs.github.io/gibs-api-docs/> |
| GIBS Access Basics | <https://nasa-gibs.github.io/gibs-api-docs/access-basics/> |
| GIBS Access Advanced Topics | <https://nasa-gibs.github.io/gibs-api-docs/access-advanced-topics/> |
| GIBS Available Visualizations | <https://nasa-gibs.github.io/gibs-api-docs/available-visualizations/> |
| GIBS Python Usage Examples | <https://nasa-gibs.github.io/gibs-api-docs/python-usage/> |
| NASA Worldview Application | <https://worldview.earthdata.nasa.gov/> |
| Worldview Snapshot API | <https://wvs.earthdata.nasa.gov/api/v1/snapshot> |

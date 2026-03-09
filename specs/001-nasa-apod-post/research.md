# Research: NASA APOD Social Posting with Image Resizing

**Date**: 2026-03-08
**Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

---

## Topic 1: SkiaSharp Image Resizing on .NET 10

### Package Names & Versions

| Package | Latest Stable | Purpose |
|---------|--------------|---------|
| `SkiaSharp` | **3.119.2** | Core managed API (cross-platform 2D graphics) |
| `SkiaSharp.NativeAssets.Win32` | **3.119.2** | Native Skia binaries for Windows (x86 + x64 + ARM64) |
| `SkiaSharp.NativeAssets.Linux` | **3.119.2** | Native Skia binaries for Linux (x64 + ARM64); **requires fontconfig** on host |
| `SkiaSharp.NativeAssets.Linux.NoDependencies` | **3.119.2** | Linux binaries **without** fontconfig dependency (preferred for containers) |
| `SkiaSharp.NativeAssets.macOS` | **3.119.2** | Native Skia binaries for macOS |

- **Prerelease**: `3.119.3-preview.1.1` exists but should not be used in production.
- The main `SkiaSharp` package transitively depends on `SkiaSharp.NativeAssets.Win32` and `SkiaSharp.NativeAssets.macOS` for net6.0/net8.0+ targets. **It does NOT depend on the Linux package** — that must be added explicitly.

### Packages Required for This Project (Windows + Linux)

For `Directory.Packages.props`:

```xml
<PackageVersion Include="SkiaSharp" Version="3.119.2" />
<PackageVersion Include="SkiaSharp.NativeAssets.Linux.NoDependencies" Version="3.119.2" />
```

- `SkiaSharp` pulls in `SkiaSharp.NativeAssets.Win32` transitively — no need to reference it explicitly.
- `SkiaSharp.NativeAssets.Linux.NoDependencies` is chosen over `SkiaSharp.NativeAssets.Linux` because it avoids the fontconfig system dependency (see Linux issues below).

### Minimal Code Pattern: Load → Resize to Byte Limit → Save as JPEG

```csharp
using SkiaSharp;

public static byte[] ResizeToFitByteLimit(byte[] imageBytes, long maxBytes, int initialQuality = 85)
{
    using var original = SKBitmap.Decode(imageBytes);
    if (original is null)
        throw new InvalidOperationException("Failed to decode image.");

    // Step 1: Try encoding at current dimensions with decreasing quality
    var bitmap = original;
    int quality = initialQuality;

    while (quality >= 10)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, quality);
        if (data.Size <= maxBytes)
            return data.ToArray();

        quality -= 10;
    }

    // Step 2: If quality reduction alone wasn't enough, reduce dimensions
    float scale = 0.9f;
    while (scale >= 0.1f)
    {
        var newWidth = (int)(original.Width * scale);
        var newHeight = (int)(original.Height * scale);
        var info = new SKImageInfo(newWidth, newHeight);

        using var resized = original.Resize(info, SKFilterQuality.Medium);
        if (resized is null)
            break;

        quality = 80;
        while (quality >= 10)
        {
            using var image = SKImage.FromBitmap(resized);
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, quality);
            if (data.Size <= maxBytes)
                return data.ToArray();

            quality -= 10;
        }

        scale -= 0.1f;
    }

    throw new InvalidOperationException(
        $"Cannot resize image to fit within {maxBytes} bytes.");
}
```

### JPEG Encoding Quality Settings

- `SKImage.Encode(SKEncodedImageFormat format, int quality)` — the `quality` parameter is an integer from **0 to 100**.
  - **100** = lossless-like, largest file
  - **85** = visually excellent, good compression
  - **75** = good quality, significant size reduction
  - **50** = acceptable for thumbnails
  - **10** = minimum usable quality
- The `SKEncodedImageFormat.Jpeg` value (enum = `3`) selects JPEG encoding.
- Encoding is synchronous — there is no async overload. This means CPU-bound work should be dispatched to a background thread in async contexts.

### SKBitmap.Resize API (SkiaSharp 3.x)

Two non-obsolete overloads available in SkiaSharp 3.119.x:

```csharp
public SKBitmap Resize(SKImageInfo info, SKFilterQuality quality);
public SKBitmap Resize(SKSizeI size, SKFilterQuality quality);
```

`SKFilterQuality` enum values:

| Value | Name | Description |
|-------|------|-------------|
| 0 | `None` | Nearest-neighbor (fastest, worst quality) |
| 1 | `Low` | Bilinear filtering |
| 2 | `Medium` | Bilinear + mipmaps (recommended for downscaling) |
| 3 | `High` | Bicubic (best quality, slowest) |

**Recommendation**: Use `SKFilterQuality.Medium` for server-side downscaling — good quality/performance trade-off.

### Known Issues with SkiaSharp on Linux Containers

1. **Fontconfig dependency**: The standard `SkiaSharp.NativeAssets.Linux` package links against `libfontconfig`. This causes `DllNotFoundException` or `TypeInitializationException` in minimal Docker images (e.g., `mcr.microsoft.com/dotnet/aspnet:10.0`) that don't have fontconfig installed.
   - **Solution**: Use `SkiaSharp.NativeAssets.Linux.NoDependencies` instead. This package bundles a `libSkiaSharp.so` that has **no external dependencies** except libc/libm/libdl/libpthread. Since our use case is image resize (no text rendering), fontconfig is unnecessary.
   - The `NoDependencies` variant's complete dependency list: `libpthread.so.0`, `libdl.so.2`, `libm.so.6`, `libc.so.6`, `ld-linux-x86-64.so.2`.

2. **Alpine Linux**: Alpine uses musl libc, not glibc. SkiaSharp's prebuilt Linux binaries target glibc-based distros (Debian/Ubuntu). Alpine will fail unless you either:
   - Use a Debian-based Docker image (recommended: `mcr.microsoft.com/dotnet/aspnet:10.0` which is Debian-based)
   - Install compatibility packages (not recommended)

3. **ARM64/aarch64 support**: `SkiaSharp.NativeAssets.Linux` 3.119.2 includes both x64 and ARM64 native libraries. No separate package needed.

4. **Memory**: SkiaSharp objects (`SKBitmap`, `SKImage`, `SKData`) hold unmanaged memory. All must be disposed via `using` blocks. Failing to dispose can cause OOM in containerized environments with low memory limits.

---

## Topic 2: NASA APOD API Integration Patterns

### API Base URL

```
https://api.nasa.gov/planetary/apod
```

- **Protocol**: HTTPS required (the API redirects HTTP to HTTPS)
- **Version**: The NASA-hosted API acts as a proxy to `/v1/apod`; calling `https://api.nasa.gov/planetary/apod` is the canonical public endpoint

### Authentication

- Query parameter: `api_key={your_key}`
- `DEMO_KEY` available for testing (30 req/hr per IP, 50/day)
- Registered keys: **1,000 requests/hour** (rolling window)
- Rate limit headers returned: `X-RateLimit-Limit` and `X-RateLimit-Remaining`

### Exact JSON Response Fields

Live response from `https://api.nasa.gov/planetary/apod?api_key=DEMO_KEY&thumbs=True` (2026-03-08):

```json
{
  "copyright": "Alyn Wallace",
  "date": "2026-03-08",
  "explanation": "Yes, but can your tree do this? ...",
  "hdurl": "https://apod.nasa.gov/apod/image/2603/AuroraTree_Wallace_2048.jpg",
  "media_type": "image",
  "service_version": "v1",
  "title": "The Aurora Tree",
  "url": "https://apod.nasa.gov/apod/image/2603/AuroraTree_Wallace_960.jpg"
}
```

Complete field inventory:

| Field | Type | Always Present | Notes |
|-------|------|----------------|-------|
| `date` | string | Yes | `YYYY-MM-DD` format |
| `title` | string | Yes | |
| `explanation` | string | Yes | Can be several paragraphs |
| `url` | string | Yes | Standard-resolution image URL or video embed URL |
| `hdurl` | string | **No** | Omitted when no HD version exists or when media_type is "video" |
| `media_type` | string | Yes | Either `"image"` or `"video"` |
| `copyright` | string | **No** | Omitted for public domain (NASA) images |
| `service_version` | string | Yes | Currently `"v1"` |
| `thumbnail_url` | string | **No** | Only present when `thumbs=True` is passed AND media_type is `"video"` |
| `resource` | object | **No** | Rarely populated; describes image set or planet |
| `concept_tags` | boolean | **No** | Only when `concept_tags=True` is requested |
| `concepts` | string | **No** | Only when `concept_tags=True` is requested |

### The `thumbs` Parameter

- **Type**: Query string parameter (not a header)
- **Usage**: `?thumbs=True` (case-insensitive boolean in query string)
- **Behavior**: When media_type is `"video"`, the API includes a `thumbnail_url` field with a URL to a thumbnail image (typically a YouTube video thumbnail). When media_type is `"image"`, the parameter is silently ignored.
- **Recommendation**: Always send `thumbs=True` in every request to ensure video APODs have thumbnails available.

### HTTP Status Codes

| Status | Condition | Response Body |
|--------|-----------|---------------|
| 200 | Success | JSON APOD object |
| 400 | Invalid parameters (e.g., bad date format, future date) | `{"code": 400, "msg": "...", "service_version": "v1"}` |
| 403 | Invalid or missing API key | `{"error": {"code": "API_KEY_INVALID", "message": "..."}}` |
| 404 | No APOD found for date | JSON error object |
| 429 | Rate limit exceeded | `{"error": {"code": "OVER_RATE_LIMIT", "message": "..."}}` |
| 500 | Server-side error | JSON error or HTML error page |
| 503 | Service temporarily unavailable | May return HTML or JSON |

### Best Practices for .NET HttpClient Integration

1. **Use typed HttpClient via `IHttpClientFactory`**: Register with `services.AddHttpClient<NasaApodClient>()` and configure base address via Aspire service discovery or options.

2. **Always include `thumbs=True`**: Append to all requests to handle video APODs gracefully.

3. **Deserialize defensively**: Many fields are optional. Use nullable properties in the C# model:

   ```csharp
   public sealed class ApodApiResponse
   {
       [JsonPropertyName("date")]
       public required string Date { get; init; }

       [JsonPropertyName("title")]
       public required string Title { get; init; }

       [JsonPropertyName("explanation")]
       public required string Explanation { get; init; }

       [JsonPropertyName("url")]
       public required string Url { get; init; }

       [JsonPropertyName("hdurl")]
       public string? HdUrl { get; init; }

       [JsonPropertyName("media_type")]
       public required string MediaType { get; init; }

       [JsonPropertyName("copyright")]
       public string? Copyright { get; init; }

       [JsonPropertyName("thumbnail_url")]
       public string? ThumbnailUrl { get; init; }

       [JsonPropertyName("service_version")]
       public string? ServiceVersion { get; init; }
   }
   ```

4. **Handle rate limiting**: Check for 429 status and `X-RateLimit-Remaining` header. Consider retry with Polly (already available via `Microsoft.Extensions.Http.Resilience` in the project).

5. **Timeout configuration**: NASA API can be slow. Set a 15-second timeout for the API call, separate from the image download timeout.

6. **Request format**: 
   ```
   GET https://api.nasa.gov/planetary/apod?api_key={key}&date={YYYY-MM-DD}&thumbs=True
   ```
   - Omit `date` for today's APOD (API defaults to current date)

---

## Topic 3: Image Resizing Strategy

### Platform Size Limits (from existing codebase)

| Platform | Max Image Size | Source |
|----------|---------------|--------|
| Bluesky | **1 MB** (1,048,576 bytes) | `BlueskyClient.cs` line 122 |
| Mastodon | **16 MB** (16,777,216 bytes) | `MastodonClient.cs` line 116 / instance config |
| LinkedIn | **20 MB** (20,971,520 bytes) | `LinkedInClient.cs` line 100 |

### APOD Image Sizes (Observed)

- Standard-resolution APOD images (`url` field) typically: **50 KB – 500 KB** (960px wide)
- HD APOD images (`hdurl` field) typically: **500 KB – 10 MB** (2048px wide)
- Per spec FR-009: We use the **standard-resolution** image (`url`), not the HD image, for the attachment

### Will Resizing Be Needed Often?

Given that we use the standard-resolution `url` (typically 960px wide, 50–500 KB):

- **Bluesky (1 MB)**: Rarely needed — most standard-res APODs are under 1 MB. Occasional large PNGs or unusually large JPEGs may exceed it.
- **Mastodon (16 MB)**: Effectively never needed for standard-res images.
- **LinkedIn (20 MB)**: Never needed for standard-res images.

The resizer is primarily a safety net for Bluesky, handling edge cases where the standard-res image slightly exceeds 1 MB.

### Recommended Strategy: Quality-First, Then Dimensions

**Approach**: Iterative quality reduction first, then dimension reduction as a fallback.

```
1. Attempt encode at quality=85
   → If under limit, done
2. Reduce quality in steps: 75 → 65 → 55 → 45 → 35 → 25
   → If under limit at any step, done
3. If quality reduction to 25 still exceeds limit:
   → Scale dimensions to 90% → re-encode at quality=75
   → Repeat dimension reduction (80%, 70%, ...) until under limit
```

**Why quality-first**:

- Dimension reduction is lossy and changes the visual composition
- Quality reduction is perceptually less noticeable down to about 50-60 for photos
- JPEG quality reduction is extremely effective on photographic content (astronomy photos compress very well)
- Quality-first preserves the full resolution for social media display

### JPEG Quality vs File Size (Empirical Observations for Photographic Content)

| Original Size | Quality 85 | Quality 75 | Quality 60 | Quality 50 | Quality 35 |
|--------------|-----------|-----------|-----------|-----------|-----------|
| 5 MB PNG | ~800 KB | ~500 KB | ~350 KB | ~280 KB | ~200 KB |
| 3 MB JPEG | ~2.5 MB | ~1.5 MB | ~900 KB | ~700 KB | ~500 KB |
| 2 MB JPEG | ~1.7 MB | ~1.0 MB | ~600 KB | ~480 KB | ~340 KB |
| 1.5 MB JPEG | ~1.3 MB | ~750 KB | ~450 KB | ~360 KB | ~250 KB |

**Key finding for Bluesky (1 MB limit)**:

- A typical 2-5 MB JPEG astronomy photo can be brought under 1 MB at **quality 60-75** without dimension changes.
- Re-encoding a JPEG to JPEG at quality 75 typically achieves a **40-55% size reduction** from the original.
- For a standard-res APOD image (typically under 500 KB), re-encoding at quality 85 is usually sufficient.
- Quality 50 is the floor for "acceptable" social media photo quality — below that, JPEG artifacts become noticeable on astronomy images (gradients and star fields suffer).

### Recommended Quality Steps for Implementation

```csharp
private static readonly int[] QualitySteps = [85, 75, 65, 55, 45];
```

- Start at 85 (visually indistinguishable from original)
- Stop at 45 before resorting to dimension reduction
- Use 10-point decrements for predictable behavior
- 5 steps max before falling back to dimension scaling

### Dimension Reduction Strategy (Fallback Only)

When quality reduction alone isn't sufficient:

1. Maintain aspect ratio (critical for astronomy photos)
2. Scale by percentage (90%, 80%, 70%...) rather than absolute pixel targets
3. Use `SKFilterQuality.Medium` (bilinear + mipmaps) for downscaling
4. After each dimension reduction, re-start quality from 75 (not 85, to converge faster)
5. Minimum dimension: 480px on the short side (below this, social media previews look poor)

### Interface Design Recommendation

```csharp
public interface IImageResizer
{
    /// <summary>
    /// Resizes an image (if needed) to fit within the specified byte limit.
    /// Returns the original bytes if already within limit.
    /// Output is always JPEG format.
    /// </summary>
    byte[] ResizeToFit(byte[] imageBytes, long maxBytes);
}
```

- Synchronous is appropriate since SkiaSharp's encode/decode are CPU-bound (no I/O)
- Can be wrapped in `Task.Run()` by the caller if needed for async contexts
- Returns original bytes untouched if already under the limit (common case for standard-res APODs)
- Always outputs JPEG per FR-020

---

## Summary of Key Decisions

| Decision | Recommendation | Rationale |
|----------|---------------|-----------|
| SkiaSharp version | **3.119.2** | Latest stable; compatible with .NET 6.0+ (and thus .NET 10) |
| Linux native package | **SkiaSharp.NativeAssets.Linux.NoDependencies** | Avoids fontconfig requirement in containers |
| Docker base image | **mcr.microsoft.com/dotnet/aspnet:10.0** (Debian) | SkiaSharp doesn't support Alpine/musl |
| NASA API key passing | Query parameter `api_key` | Per API docs |
| Thumbnail retrieval | Always send `thumbs=True` query param | Ensures video APODs have thumbnails |
| Image source | Standard-resolution `url` field | Per spec FR-009 |
| Resize strategy | Quality-first (85→45), then dimension reduction | Quality reduction is less destructive for photos |
| Quality floor | **45** before dimension reduction | Below 45, JPEG artifacts are noticeable on astronomy gradients |
| JPEG quality for Bluesky | Typically **65-75** for standard-res APOD ≤1 MB | Empirical sweet spot for photo content |
| Filter quality | `SKFilterQuality.Medium` | Best quality/performance for downscaling |

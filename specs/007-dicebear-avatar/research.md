# Research: DiceBear Random Avatar

**Feature**: 007-dicebear-avatar
**Date**: 2026-03-15

## R1: DiceBear HTTP API Integration Pattern

**Decision**: Use the DiceBear HTTP API (v9.x) as a passthrough proxy — our endpoint constructs a URL and fetches the image bytes from `https://api.dicebear.com/9.x/{style}/{format}?seed={seed}`, then returns them to the caller with the correct content type.

**Rationale**: The DiceBear HTTP API is free, requires no authentication, and supports all needed features (styles, formats, seeds) via simple URL query parameters. No client library or SDK is needed — a typed `HttpClient` with URL construction is sufficient, following the same pattern used by `NasaApodClient` and `NasaGibsClient` in the existing codebase.

**Alternatives considered**:

- **DiceBear JS/Node library**: Would require a Node.js sidecar or WASM integration. Far more complex than needed for a simple HTTP proxy.
- **Self-hosted DiceBear API**: Would provide rate limit control and guaranteed availability but adds significant infrastructure complexity. Not warranted for initial implementation; can be revisited if rate limits become an issue.
- **Client-side redirect**: Could redirect the caller to the DiceBear URL directly (302 redirect). Rejected because it doesn't allow the API to control error handling, logging, or validation; also exposes the upstream dependency to callers.

## R2: Upstream URL Structure & Parameters

**Decision**: Construct upstream URLs using the pattern `https://api.dicebear.com/9.x/{styleName}/{format}?seed={seed}`.

**Rationale**: This is the documented DiceBear HTTP API URL format. Version `9.x` is the latest supported version. All core options (seed, flip, rotate, scale, radius, backgroundColor) are available as query parameters, but for this feature only `seed` is needed.

**Key API details**:

- Base URL: `https://api.dicebear.com/9.x`
- Style names use kebab-case in URL path (e.g., `pixel-art`, `big-ears-neutral`)
- Format is the file extension in the URL path: `svg`, `png`, `jpg`, `webp`, `avif`, `json`
- Seed is passed as `?seed={value}` query parameter
- No authentication headers needed
- Rate limits: 50 req/s for SVG, 10 req/s for PNG/JPG/WebP/AVIF
- Raster formats: max 256×256px
- HTTP 429 returned when rate-limited

## R3: Avatar Style Enumeration

**Decision**: Define the list of valid styles as a static readonly collection in `AvatarStyle` class in Core, using string constants matching the DiceBear kebab-case names.

**Rationale**: The style list is relatively stable (DiceBear adds styles infrequently across major versions). A static list enables compile-time reference and validation without external API calls. The list can be updated when bumping the DiceBear API version.

**Alternatives considered**:

- **Dynamic discovery via JSON endpoint**: DiceBear doesn't expose a styles listing API endpoint. Would require scraping or hardcoding anyway.
- **Enum type**: Enums don't naturally support kebab-case values and would require mapping attributes. String constants are simpler and match the URL path directly.

**Supported styles (v9.x)**: adventurer, adventurer-neutral, avataaars, avataaars-neutral, big-ears, big-ears-neutral, big-smile, bottts, bottts-neutral, croodles, croodles-neutral, dylan, fun-emoji, glass, icons, identicon, initials, lorelei, lorelei-neutral, micah, miniavs, notionists, notionists-neutral, open-peeps, personas, pixel-art, pixel-art-neutral, rings, shapes, thumbs, toon-head.

## R4: Output Format Handling

**Decision**: Use a string-based format parameter validated against an allowlist of supported values: `svg`, `png`, `jpg`, `webp`, `avif`. Map each format to its MIME content type for the response header.

**Rationale**: The format maps directly to the URL path extension in the DiceBear API, so string values are the most natural representation. Content type mapping is straightforward:

| Format | DiceBear URL Extension | Content Type |
|--------|----------------------|--------------|
| svg | `/svg` | `image/svg+xml` |
| png | `/png` | `image/png` |
| jpg | `/jpg` | `image/jpeg` |
| webp | `/webp` | `image/webp` |
| avif | `/avif` | `image/avif` |

**Alternatives considered**:

- **Enum with `[Description]` attribute**: More type-safe but adds mapping overhead. The string allowlist is simpler and aligns with the URL path structure.
- **Accept header content negotiation**: More RESTful but adds complexity for a simple proxy; explicit format parameter is clearer for API consumers.

## R5: Error Handling & Resilience

**Decision**: Use `Microsoft.Extensions.Http.Resilience` (already in the project) for standard HTTP resilience policies on the typed HttpClient. Catch upstream errors and return appropriate HTTP status codes from our endpoint.

**Rationale**: The project already uses `Microsoft.Extensions.Http.Resilience` via Aspire service defaults. The DiceBear API is a third-party dependency that may be temporarily unavailable, rate-limited, or slow. Standard resilience policies (retry, circuit breaker, timeout) protect the API from cascading failures.

**Error mapping**:

| Upstream Condition | Our Response |
|---|---|
| DiceBear returns 200 OK | Forward image bytes with correct content type |
| DiceBear returns 429 Too Many Requests | Return 502 Bad Gateway with message about rate limiting |
| DiceBear returns 4xx (unexpected) | Return 502 Bad Gateway with upstream error details |
| DiceBear returns 5xx | Return 502 Bad Gateway with service unavailability message |
| Network timeout / connection failure | Return 502 Bad Gateway with service unavailability message |
| Invalid style parameter | Return 400 Bad Request via FluentValidation (before upstream call) |
| Invalid format parameter | Return 400 Bad Request via FluentValidation (before upstream call) |
| Seed too long | Return 400 Bad Request via FluentValidation (before upstream call) |

## R6: Endpoint Return Type Pattern

**Decision**: The endpoint returns raw image bytes with the correct content type, not a JSON response. Use `SendBytesAsync()` from FastEndpoints to stream the image content directly.

**Rationale**: Avatar images should be served as actual images, not wrapped in JSON. This allows direct use in `<img>` tags, CSS `url()`, or as download targets. FastEndpoints supports this via `SendBytesAsync()` or by writing directly to `HttpContext.Response`.

**Alternatives considered**:

- **Base64-encoded JSON response**: Would increase payload size by ~33% and require client-side decoding. Not practical for image delivery.
- **Redirect to DiceBear URL**: Exposes upstream dependency and prevents error handling, logging, and validation.

## R7: Configuration Requirements

**Decision**: No configuration parameters needed for the initial implementation. The DiceBear base URL (`https://api.dicebear.com/9.x`) can be hardcoded as a constant since it's a public, well-known API with versioned URLs.

**Rationale**: DiceBear requires no API key, no authentication, and the base URL is stable per version. Adding a configurable base URL would comply with YAGNI violation — it adds complexity for a scenario (self-hosted DiceBear) that isn't currently needed.

**Future consideration**: If the project later moves to a self-hosted DiceBear instance, a `DiceBearOptions` configuration class with a `BaseUrl` property can be added via Aspire parameter, following the same pattern as `NasaApodOptions`.

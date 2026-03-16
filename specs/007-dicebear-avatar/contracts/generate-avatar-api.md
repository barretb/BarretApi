# API Contract: Generate Avatar

**Feature**: 007-dicebear-avatar
**Date**: 2026-03-15

## Endpoint

```
GET /api/avatars/random
```

## Request

### Query Parameters

| Parameter | Type | Required | Default | Constraints | Description |
|-----------|------|----------|---------|-------------|-------------|
| style | string | No | Random selection from all styles | Must be a valid style name (see below) | Avatar visual style |
| format | string | No | `svg` | One of: `svg`, `png`, `jpg`, `webp`, `avif` | Output image format |
| seed | string | No | Random GUID | Max 256 characters | Deterministic generation seed |

### Example Requests

```
GET /api/avatars/random
GET /api/avatars/random?style=pixel-art
GET /api/avatars/random?style=adventurer&format=png
GET /api/avatars/random?style=bottts&seed=john-doe&format=svg
GET /api/avatars/random?seed=my-user-id
```

## Response

### Success (200 OK)

Returns the raw image bytes with the appropriate content type header.

**Response Headers**:

| Header | Value |
|--------|-------|
| Content-Type | Depends on format: `image/svg+xml`, `image/png`, `image/jpeg`, `image/webp`, or `image/avif` |

**Response Body**: Raw image bytes (binary content, not JSON)

### Validation Error (400 Bad Request)

Returned when request parameters fail validation.

**Response Body** (JSON):

```json
{
  "statusCode": 400,
  "message": "One or more validation errors occurred.",
  "errors": {
    "style": ["'style' must be one of the supported styles: adventurer, adventurer-neutral, avataaars, ..."],
    "format": ["'format' must be one of: svg, png, jpg, webp, avif"],
    "seed": ["'seed' must not exceed 256 characters"]
  }
}
```

### Upstream Service Error (502 Bad Gateway)

Returned when the DiceBear API is unreachable, rate-limited, or returns an error.

**Response Body** (JSON):

```json
{
  "statusCode": 502,
  "message": "The avatar generation service is temporarily unavailable. Please try again later."
}
```

## Valid Avatar Styles

The following style names are accepted (case-insensitive):

### Minimalist

- `glass`
- `icons`
- `identicon`
- `initials`
- `rings`
- `shapes`
- `thumbs`

### Character

- `adventurer`
- `adventurer-neutral`
- `avataaars`
- `avataaars-neutral`
- `big-ears`
- `big-ears-neutral`
- `big-smile`
- `bottts`
- `bottts-neutral`
- `croodles`
- `croodles-neutral`
- `dylan`
- `fun-emoji`
- `lorelei`
- `lorelei-neutral`
- `micah`
- `miniavs`
- `notionists`
- `notionists-neutral`
- `open-peeps`
- `personas`
- `pixel-art`
- `pixel-art-neutral`
- `toon-head`

## Authentication

This endpoint follows the same authentication scheme as the rest of the API (API key authentication via `ApiKeyAuthHandler`).

## Rate Limiting

No application-level rate limiting is implemented. The upstream DiceBear API enforces its own rate limits:

- SVG: 50 requests/second
- PNG, JPG, WebP, AVIF: 10 requests/second

If the upstream rate limit is exceeded, a 502 response is returned to the caller.

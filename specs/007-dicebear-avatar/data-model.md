# Data Model: DiceBear Random Avatar

**Feature**: 007-dicebear-avatar
**Date**: 2026-03-15

## Entities

### AvatarStyle (Static Reference Data)

Represents the set of valid DiceBear avatar style identifiers. Not a persisted entity — defined as compile-time constants.

| Field | Type | Description |
|-------|------|-------------|
| Name | string | Kebab-case style identifier (e.g., "pixel-art", "adventurer") |
| Category | string | "minimalist" or "character" (informational only) |

**Validation Rules**:

- Style name MUST be one of the 32 supported values (see list below)
- Style name comparison MUST be case-insensitive

**Supported Styles**:

| Style Name | Category |
|------------|----------|
| adventurer | character |
| adventurer-neutral | character |
| avataaars | character |
| avataaars-neutral | character |
| big-ears | character |
| big-ears-neutral | character |
| big-smile | character |
| bottts | character |
| bottts-neutral | character |
| croodles | character |
| croodles-neutral | character |
| dylan | character |
| fun-emoji | character |
| glass | minimalist |
| icons | minimalist |
| identicon | minimalist |
| initials | minimalist |
| lorelei | character |
| lorelei-neutral | character |
| micah | character |
| miniavs | character |
| notionists | character |
| notionists-neutral | character |
| open-peeps | character |
| personas | character |
| pixel-art | character |
| pixel-art-neutral | character |
| rings | minimalist |
| shapes | minimalist |
| thumbs | minimalist |
| toon-head | character |

### AvatarFormat (Enum)

Represents the supported output image formats.

| Value | URL Extension | Content Type | Max Size | Rate Limit |
|-------|--------------|--------------|----------|------------|
| Svg | svg | image/svg+xml | Unlimited | 50 req/s |
| Png | png | image/png | 256×256px | 10 req/s |
| Jpg | jpg | image/jpeg | 256×256px | 10 req/s |
| WebP | webp | image/webp | 256×256px | 10 req/s |
| Avif | avif | image/avif | 256×256px | 10 req/s |

**Validation Rules**:

- Format MUST be one of the five supported values
- Default format is `Svg` when not specified

### AvatarResult (Domain Model)

Represents the result of an avatar generation request. Returned by the infrastructure client to the API layer.

| Field | Type | Description |
|-------|------|-------------|
| ImageBytes | byte[] | Raw image content from DiceBear API |
| ContentType | string | MIME type for the response header (e.g., "image/svg+xml") |
| Style | string | The style used (for logging/metadata) |
| Seed | string | The seed used (for logging/metadata) |
| Format | string | The format used (for logging/metadata) |

**Validation Rules**:

- ImageBytes MUST NOT be empty
- ContentType MUST be a valid image MIME type

## Relationships

```text
GenerateAvatarRequest ──→ IDiceBearAvatarClient ──→ DiceBear HTTP API
         │                        │
         │                        ▼
         │                  AvatarResult
         │                  (bytes + content type)
         ▼
  GenerateAvatarEndpoint ──→ HTTP Response (image bytes)
```

- A request specifies zero or one `AvatarStyle`, zero or one `AvatarFormat`, and zero or one seed
- The client resolves defaults (random style, random seed, SVG format) before calling upstream
- The result carries raw image bytes that the endpoint streams directly to the caller

## State Transitions

No state transitions — this is a stateless, read-only operation. Each request is independent with no side effects or persistence.

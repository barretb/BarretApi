# Data Model: Hero Image Generator

**Feature Branch**: `009-hero-image-generator`
**Date**: 2026-04-10

## Entities

### HeroImageRequest (API Layer — Request DTO)

Represents the input submitted by the user to the hero image generation endpoint.

| Field | Type | Required | Constraints | Description |
|-------|------|----------|-------------|-------------|
| Title | string | Yes | Non-empty, max 200 chars | The main heading text rendered on the hero image |
| Subtitle | string | No | Max 300 chars if provided | Secondary text rendered below the title at a smaller size |
| BackgroundImage | file (IFormFile) | No | JPEG or PNG, max 10 MB | Custom background image; generic background used if omitted |

**Validation rules**:

- Title must not be null, empty, or whitespace
- Title must not exceed 200 characters
- Subtitle, if provided, must not exceed 300 characters
- BackgroundImage, if provided, must have content type `image/jpeg` or `image/png`
- BackgroundImage, if provided, must not exceed 10 MB (10,485,760 bytes)
- BackgroundImage, if provided, must be a valid decodable image (verified by attempting `SKBitmap.Decode`)

### HeroImageOptions (Core Layer — Configuration Model)

Represents the configuration for the hero image generator service, including asset file paths and layout parameters.

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| FaceImagePath | string | Yes | — | Absolute path to the face image file (barretcircle2.png) |
| LogoImagePath | string | Yes | — | Absolute path to the logo image file (barret-blake-logo-1024.png) |
| DefaultBackgroundPath | string | Yes | — | Absolute path to the generic background image (generic-background.jpg) |
| OutputWidth | int | No | 1280 | Width of the generated hero image in pixels |
| OutputHeight | int | No | 720 | Height of the generated hero image in pixels |

### HeroImageGenerationCommand (Core Layer — Use Case Input)

Represents the processed input passed from the endpoint to the generator service.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| Title | string | Yes | Validated title text |
| Subtitle | string | No | Validated subtitle text (null if not provided) |
| CustomBackgroundBytes | byte[] | No | Raw bytes of the uploaded background image (null if not provided) |

## Relationships

```
HeroImageRequest (API)
    │
    ▼ [mapped by endpoint]
HeroImageGenerationCommand (Core)
    │
    ▼ [consumed by IHeroImageGenerator]
byte[] (generated PNG image)
```

- `HeroImageRequest` is the API-layer DTO bound from the HTTP multipart form request
- The endpoint maps `HeroImageRequest` → `HeroImageGenerationCommand`, reading the `IFormFile` stream into `byte[]`
- `IHeroImageGenerator.GenerateAsync(HeroImageGenerationCommand)` returns the composed image as `byte[]`
- The endpoint writes the `byte[]` directly to the response body with `Content-Type: image/png`

## State Transitions

This feature is **stateless** — no data is persisted. Each request produces a new image independently. There are no state transitions to model.

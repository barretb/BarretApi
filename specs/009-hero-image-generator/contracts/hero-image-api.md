# API Contract: Hero Image Generator

**Feature Branch**: `009-hero-image-generator`
**Date**: 2026-04-10

## POST /api/hero-image

Generate a branded hero image with title text, optional subtitle, and optional custom background.

### Request

**Content-Type**: `multipart/form-data`

| Field | Type | Required | Constraints | Description |
|-------|------|----------|-------------|-------------|
| `title` | string | Yes | Non-empty, max 200 chars | Main heading text |
| `subtitle` | string | No | Max 300 chars | Secondary text below the title |
| `backgroundImage` | file | No | JPEG/PNG, max 10 MB | Custom background image |

### Response — 200 OK

**Content-Type**: `image/png`
**Body**: Raw PNG image bytes (1280×720 pixels)

The response is the generated hero image as a binary PNG file. No JSON wrapper.

### Response — 400 Bad Request

**Content-Type**: `application/json`

Returned when request validation fails.

```json
{
  "statusCode": 400,
  "message": "Validation failed",
  "errors": {
    "title": ["Title is required."],
    "backgroundImage": ["Background image must be JPEG or PNG format."]
  }
}
```

**Possible validation errors**:

| Field | Condition | Message |
|-------|-----------|---------|
| `title` | Missing or empty | "Title is required." |
| `title` | Exceeds 200 characters | "Title must not exceed 200 characters." |
| `subtitle` | Exceeds 300 characters | "Subtitle must not exceed 300 characters." |
| `backgroundImage` | Not JPEG or PNG | "Background image must be JPEG or PNG format." |
| `backgroundImage` | Exceeds 10 MB | "Background image must not exceed 10 MB." |

### Response — 422 Unprocessable Entity

**Content-Type**: `application/json`

Returned when the uploaded background image cannot be decoded as a valid image.

```json
{
  "statusCode": 422,
  "message": "The uploaded background image could not be decoded. Ensure it is a valid JPEG or PNG file."
}
```

### Response — 500 Internal Server Error

**Content-Type**: `application/json`

Returned when image generation fails unexpectedly.

```json
{
  "statusCode": 500,
  "message": "An unexpected error occurred while generating the hero image."
}
```

## Example Usage

### Minimal Request (title only)

```bash
curl -X POST http://localhost:5000/api/hero-image \
  -F "title=Getting Started with .NET 10" \
  -o hero-image.png
```

### Full Request (title + subtitle + custom background)

```bash
curl -X POST http://localhost:5000/api/hero-image \
  -F "title=Blazor Deep Dive" \
  -F "subtitle=Part 3: Component Lifecycle" \
  -F "backgroundImage=@my-background.jpg" \
  -o hero-image.png
```

## Swagger/OpenAPI Summary

```yaml
paths:
  /api/hero-image:
    post:
      summary: Generate branded hero image
      description: >
        Accepts a title, optional subtitle, and optional background image upload.
        Returns a 1280×720 PNG hero image with the title text, face image in the
        lower right, logo in the lower left, and a faded background.
      requestBody:
        required: true
        content:
          multipart/form-data:
            schema:
              type: object
              required: [title]
              properties:
                title:
                  type: string
                  maxLength: 200
                subtitle:
                  type: string
                  maxLength: 300
                backgroundImage:
                  type: string
                  format: binary
      responses:
        '200':
          description: Hero image generated successfully
          content:
            image/png:
              schema:
                type: string
                format: binary
        '400':
          description: Validation error
        '422':
          description: Uploaded image could not be decoded
        '500':
          description: Internal server error
```

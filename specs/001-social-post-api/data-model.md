# Data Model: Social Media Post API

**Date**: 2026-02-28 | **Spec**: [spec.md](spec.md) | **Research**: [research.md](research.md)

---

## Entity Relationship Overview

```text
CreateSocialPostRequest 1──*  ImageAttachment
CreateSocialPostRequest 1──*  Hashtag (separate list)
CreateSocialPostRequest 1──*  Platform (target selection)

SocialPostOrchestrator  1──*  ISocialPlatformClient (one per platform)

CreateSocialPostResponse 1──*  PlatformResult
```

---

## 1. API Layer (Request / Response DTOs)

### CreateSocialPostRequest

The inbound request DTO for both the JSON and multipart endpoints.

| Field | Type | Required | Validation | Notes |
|-------|------|----------|------------|-------|
| `Text` | `string?` | Conditional | Required if no images; max 10,000 chars (pre-shortening input cap) | Raw post text; may exceed platform limits (auto-shortened) |
| `Hashtags` | `List<string>?` | No | Each tag max 100 chars; no spaces; auto-prefixed with `#` | Appended to text after inline hashtags |
| `Platforms` | `List<string>?` | No | Each must be `"bluesky"` or `"mastodon"` (case-insensitive) | Defaults to all configured platforms if omitted (FR-004) |
| `Images` | `List<ImageAttachmentRequest>?` | No | Max 4 items (FR-008); each must have alt text | Images via URL (JSON endpoint) or file upload (multipart endpoint) |

### ImageAttachmentRequest (JSON endpoint — URL-referenced images)

| Field | Type | Required | Validation | Notes |
|-------|------|----------|------------|-------|
| `Url` | `string` | Yes | Must be a valid absolute HTTP(S) URL | Server downloads the image before uploading to platforms (FR-022) |
| `AltText` | `string` | Yes | Non-empty, non-whitespace; max 1,500 chars | Mandatory for accessibility (FR-009, FR-010) |

### ImageUploadRequest (Multipart endpoint — file uploads)

| Field | Type | Required | Validation | Notes |
|-------|------|----------|------------|-------|
| `File` | `IFormFile` | Yes | Max 1 MB (Bluesky constraint); JPEG/PNG/GIF/WebP only (FR-015) | Binary image data via multipart form |
| `AltText` | `string` | Yes | Non-empty, non-whitespace; max 1,500 chars | Mandatory for accessibility (FR-009, FR-010) |

### CreateSocialPostResponse

| Field | Type | Always present | Notes |
|-------|------|---------------|-------|
| `Results` | `List<PlatformResult>` | Yes | One entry per targeted platform |
| `PostedAt` | `DateTimeOffset` | Yes | Timestamp of the request processing |

### PlatformResult

| Field | Type | Always present | Notes |
|-------|------|---------------|-------|
| `Platform` | `string` | Yes | `"bluesky"` or `"mastodon"` |
| `Success` | `bool` | Yes | Whether the post was published successfully |
| `PostId` | `string?` | On success | Platform-specific identifier (Bluesky: `at://` URI; Mastodon: status ID) |
| `PostUrl` | `string?` | On success | Direct URL to the published post |
| `ShortenedText` | `string?` | On success | The actual text published (after shortening, if applied) |
| `Error` | `string?` | On failure | Human-readable error message |
| `ErrorCode` | `string?` | On failure | Machine-readable error code (e.g., `AUTH_FAILED`, `RATE_LIMITED`, `VALIDATION_FAILED`) |

---

## 2. Core Layer (Domain Models & Interfaces)

### SocialPost (Domain Model)

Internal representation after request parsing and hashtag processing.

| Field | Type | Notes |
|-------|------|-------|
| `Text` | `string` | Original text with hashtags appended (pre-shortening) |
| `Hashtags` | `IReadOnlyList<string>` | De-duplicated, `#`-prefixed hashtags (inline + separate list merged) |
| `Images` | `IReadOnlyList<ImageData>` | Processed image data ready for upload |
| `TargetPlatforms` | `IReadOnlyList<string>` | Resolved platform names |

### ImageData (Value Object)

| Field | Type | Notes |
|-------|------|-------|
| `Content` | `byte[]` | Raw image bytes (downloaded from URL or read from upload) |
| `ContentType` | `string` | MIME type (e.g., `image/jpeg`, `image/png`) |
| `AltText` | `string` | Validated, non-empty alt text |
| `FileName` | `string?` | Original filename if available |

### PlatformConfiguration (Value Object)

Cached configuration for each platform.

| Field | Type | Notes |
|-------|------|-------|
| `Name` | `string` | `"bluesky"` or `"mastodon"` |
| `MaxCharacters` | `int` | Bluesky: 300 grapheme clusters; Mastodon: from instance config |
| `MaxImages` | `int` | Both: 4 |
| `MaxImageSizeBytes` | `long` | Bluesky: 1,048,576 (1 MB); Mastodon: from instance config |
| `MaxAltTextLength` | `int` | Bluesky: ~1,000; Mastodon: 1,500 |

### ISocialPlatformClient (Interface)

Defined in Core; implemented per-platform in Infrastructure.

```csharp
public interface ISocialPlatformClient
{
    string PlatformName { get; }

    Task<PlatformPostResult> PostAsync(
        string text,
        IReadOnlyList<UploadedImage> images,
        CancellationToken cancellationToken = default);

    Task<UploadedImage> UploadImageAsync(
        ImageData image,
        CancellationToken cancellationToken = default);

    Task<PlatformConfiguration> GetConfigurationAsync(
        CancellationToken cancellationToken = default);
}
```

### ITextShorteningService (Interface)

Defined in Core; implemented in Core (pure logic, no I/O).

```csharp
public interface ITextShorteningService
{
    string Shorten(string text, IReadOnlyList<string> hashtags, int maxCharacters);
}
```

### IHashtagService (Interface)

Defined in Core; implemented in Core (pure logic).

```csharp
public interface IHashtagService
{
    (string processedText, IReadOnlyList<string> allHashtags) Process(
        string? text,
        IReadOnlyList<string>? separateHashtags);
}
```

### UploadedImage (Value Object)

Platform-specific result of an image upload.

| Field | Type | Notes |
|-------|------|-------|
| `PlatformImageId` | `string` | Mastodon: media attachment ID; Bluesky: blob ref JSON |
| `AltText` | `string` | The alt text submitted with the upload |
| `PlatformData` | `object?` | Platform-specific metadata (e.g., Bluesky blob object for embedding) |

### PlatformPostResult (Value Object)

Result of posting to a single platform (used internally by the orchestration service).

| Field | Type | Notes |
|-------|------|-------|
| `Success` | `bool` | Whether the post succeeded |
| `PostId` | `string?` | Platform identifier |
| `PostUrl` | `string?` | Direct URL to the post |
| `PublishedText` | `string?` | The text actually published (after shortening) |
| `Error` | `Exception?` | The exception if the post failed |

---

## 3. Infrastructure Layer (Platform Client Internals)

### BlueskySession (Internal)

Cached authentication session for Bluesky.

| Field | Type | Notes |
|-------|------|-------|
| `Did` | `string` | User DID (decentralized identifier) |
| `AccessJwt` | `string` | Short-lived access token |
| `RefreshJwt` | `string` | Long-lived refresh token |
| `Handle` | `string` | User handle |

### BlueskyFacet (Internal)

Rich text facet for Bluesky posts.

| Field | Type | Notes |
|-------|------|-------|
| `ByteStart` | `int` | UTF-8 byte offset start |
| `ByteEnd` | `int` | UTF-8 byte offset end |
| `Type` | `string` | `"app.bsky.richtext.facet#tag"`, `"app.bsky.richtext.facet#link"`, etc. |
| `Value` | `string` | Tag name (without `#`), URI, or DID depending on type |

### MastodonInstanceConfig (Internal)

Cached instance configuration from `GET /api/v2/instance`.

| Field | Type | Notes |
|-------|------|-------|
| `MaxCharacters` | `int` | Default 500; varies by instance |
| `MaxMediaAttachments` | `int` | Default 4 |
| `CharactersReservedPerUrl` | `int` | Default 23 |
| `ImageSizeLimit` | `long` | Default 16,777,216 (16 MB) |
| `DescriptionLimit` | `int` | Alt text max; default 1,500 |

---

## 4. Configuration (Aspire AppHost — IOptions<T>)

### BlueskyOptions

| Field | Type | Notes |
|-------|------|-------|
| `Handle` | `string` | Bluesky handle or DID |
| `AppPassword` | `string` | App password (from User Secrets) |
| `ServiceUrl` | `string` | Default: `https://bsky.social` |

### MastodonOptions

| Field | Type | Notes |
|-------|------|-------|
| `InstanceUrl` | `string` | e.g., `https://mastodon.social` |
| `AccessToken` | `string` | Bearer token (from User Secrets) |

### ApiKeyOptions

| Field | Type | Notes |
|-------|------|-------|
| `Key` | `string` | Pre-shared API key (from User Secrets) |

### RetryOptions

| Field | Type | Notes |
|-------|------|-------|
| `MaxRetryAttempts` | `int` | Default: 3 (A-009) |
| `InitialDelaySeconds` | `double` | Default: 1.0 (A-009) |
| `UseExponentialBackoff` | `bool` | Default: true |

---

## 5. Validation Rules Summary

| Rule | Source | Layer |
|------|--------|-------|
| Text required if no images | FR-018 | Validator |
| Images require alt text (non-empty, non-whitespace) | FR-009, FR-010 | Validator |
| Max 4 images | FR-008 | Validator |
| Image format must be JPEG, PNG, GIF, or WebP | FR-015 | Validator |
| Platforms must be valid names or empty (default all) | FR-004 | Validator |
| Hashtags: no spaces, max 100 chars each | Derived | Validator |
| Alt text max 1,500 chars | Research §7.5 | Validator |
| Image file size max 1 MB (smallest platform limit) | Research §6.5 | Validator (pre-upload) |
| URL images must be reachable | FR-024 | Service (runtime) |

---

## 6. State Transitions

This API is stateless (fire-and-forget, FR-021). There are no persistent state transitions. The request lifecycle is:

```text
Request Received
  → Validate (reject if invalid → 400)
  → Authenticate (reject if unauthorized → 401)
  → Process hashtags (merge, de-dup, prefix)
  → Download URL images (if any; fail → include in error response)
  → For each target platform (parallel):
      → Shorten text to platform limit
      → Upload images to platform
      → Build platform-specific metadata (Bluesky facets)
      → Create post on platform
      → Collect result (success or failure)
  → Aggregate results
  → Return response (200 / 207 / 502)
```

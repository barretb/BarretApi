# Feature Specification: Social Media Post API

**Feature Branch**: `001-social-post-api`  
**Created**: 2026-02-28  
**Status**: Draft  
**Input**: User description: "We are going to create a web api that will have various utilities for me to use. For the MVP, I want to add the ability to post to Bluesky and Mastodon social media sites on my behalf. It should include the ability to add images and hashtags to the posts, as well as automatically shortening the length of the post to fit in the allowed number of characters. It should also require that all images include alt text for accessibility"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Post a Text Message to Both Platforms (Priority: P1)

As the API owner, I want to submit a text post through a single request and have it published to both Bluesky and Mastodon simultaneously, so that I can maintain a presence on both platforms without duplicating effort.

**Why this priority**: This is the foundational capability. Without the ability to create and publish a basic text post, no other features (images, hashtags, auto-shortening) are meaningful. A working text post to both platforms is the minimum viable product.

**Independent Test**: Can be fully tested by sending a text-only post request and verifying the post appears on both Bluesky and Mastodon with the correct content.

**Acceptance Scenarios**:

1. **Given** the user provides valid post text and credentials are configured, **When** the user submits a post request targeting both platforms, **Then** the post is published to both Bluesky and Mastodon and the response confirms success for each platform.
2. **Given** the user provides valid post text, **When** the user submits a post request targeting only one platform, **Then** the post is published only to the specified platform.
3. **Given** the user provides valid post text but one platform's credentials are misconfigured, **When** the user submits a post request targeting both platforms, **Then** the post succeeds on the working platform, and the response clearly indicates which platform failed and why.
4. **Given** the user submits a post with empty text and no images, **When** the request is processed, **Then** the system rejects the request with a clear validation error.

---

### User Story 2 - Automatic Post Length Shortening (Priority: P2)

As the API owner, I want the system to automatically shorten my post text to fit within each platform's character limit, so that my posts are always accepted without me having to manually count or trim characters.

**Why this priority**: Character limits differ between platforms (Bluesky: 300 characters; Mastodon: default 500 characters). Automatic shortening removes a major friction point and prevents post failures due to length violations. This is essential before images and hashtags add complexity.

**Independent Test**: Can be fully tested by submitting posts of various lengths — below, at, and above each platform's limit — and verifying the published text is correctly shortened with an ellipsis or truncation indicator, while posts within limits are left unchanged.

**Acceptance Scenarios**:

1. **Given** the user submits post text that is within both platforms' character limits, **When** the request is processed, **Then** the text is published unmodified to both platforms.
2. **Given** the user submits post text that exceeds Bluesky's 300-character limit but is within Mastodon's 500-character limit, **When** the request is processed, **Then** Bluesky receives a shortened version (truncated at a word boundary with an ellipsis) and Mastodon receives the full text.
3. **Given** the user submits post text that exceeds both platforms' limits, **When** the request is processed, **Then** each platform receives text shortened to its respective limit, truncated at a word boundary with an ellipsis.
4. **Given** the post text contains hashtags at the end, **When** shortening is required, **Then** the system preserves as many hashtags as possible within the character limit, removing hashtags from the end first before truncating body text.

---

### User Story 3 - Attach Images with Required Alt Text (Priority: P3)

As the API owner, I want to attach images to my posts with mandatory alt text for each image, so that my social media posts are visually engaging and accessible to people using screen readers.

**Why this priority**: Image support adds significant value to posts but depends on the core posting capability (P1) being in place. Requiring alt text ensures accessibility compliance from the start.

**Independent Test**: Can be fully tested by submitting a post with one or more images (each with alt text) and verifying the images and alt text appear correctly on both platforms.

**Acceptance Scenarios**:

1. **Given** the user submits a post with one image and valid alt text, **When** the request is processed, **Then** the image is uploaded and attached to the post on both platforms with the provided alt text.
2. **Given** the user submits a post with multiple images (up to 4) each with alt text, **When** the request is processed, **Then** all images are attached to the post on both platforms with their respective alt text.
3. **Given** the user submits a post with an image but without alt text, **When** the request is processed, **Then** the system rejects the request with a validation error indicating alt text is required.
4. **Given** the user submits a post with an image that has empty or whitespace-only alt text, **When** the request is processed, **Then** the system rejects the request with a validation error indicating meaningful alt text is required.
5. **Given** the user submits a post with more images than a platform supports (Bluesky: 4, Mastodon: 4), **When** the request is processed, **Then** the system rejects the request with a validation error indicating the maximum number of images allowed.

---

### User Story 4 - Include Hashtags in Posts (Priority: P4)

As the API owner, I want to include hashtags in my posts so that my content is discoverable by others browsing those topics on each platform.

**Why this priority**: Hashtags enhance discoverability and are a standard social media feature, but posts are fully functional without them. This builds on the core posting and shortening capabilities.

**Independent Test**: Can be fully tested by submitting a post with hashtags and verifying they appear correctly formatted on both platforms and are recognized as clickable hashtags.

**Acceptance Scenarios**:

1. **Given** the user includes hashtags in the post text (e.g., "#dotnet #webapi"), **When** the request is processed, **Then** both platforms publish the post with hashtags correctly formatted and clickable.
2. **Given** the user provides hashtags as a separate list in the request, **When** the request is processed, **Then** the system appends the hashtags to the end of the post text, space-separated, and each prefixed with `#` if not already.
3. **Given** the user provides hashtags both inline in the text and as a separate list, **When** the request is processed, **Then** the system appends only the list-provided hashtags that are not already present in the text (no duplicates).
4. **Given** hashtags cause the total post length to exceed a platform's character limit, **When** the request is processed, **Then** the auto-shortening logic trims excess hashtags before truncating body text.

---

### Edge Cases

- What happens when the post text is composed entirely of hashtags and exceeds the character limit? The system should truncate hashtags from the end until within the limit; if no hashtags remain, it should reject the post as empty.
- What happens when an image upload succeeds on one platform but fails on the other? The system should report partial success: the successful platform's post stands, and the response indicates the failure with the error details.
- What happens when the user submits an image in an unsupported format? The system should reject the request with a validation error listing the supported image formats (JPEG, PNG, GIF, WebP).
- What happens when the user sends a post while a platform is experiencing an outage? The system should return a clear error for the affected platform without blocking the post to the healthy platform.
- What happens when alt text exceeds Bluesky's or Mastodon's alt text length limits? The system should truncate alt text to the platform's maximum (Bluesky: ~1,000 characters; Mastodon: ~1,500 characters) and include the truncation in the response.
- What happens when the user's authentication token for a platform has expired? The system should return a clear authentication error for that platform and suggest re-authentication.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST accept a post request containing text content, an optional list of hashtags, and optional image attachments (via multipart form upload or URL references in a JSON body).
- **FR-002**: System MUST publish posts to Bluesky via its API.
- **FR-003**: System MUST publish posts to Mastodon via its API.
- **FR-004**: System MUST allow the user to optionally specify which platforms to post to per request; if no platforms are specified, the system MUST default to all configured platforms.
- **FR-005**: System MUST automatically shorten post text to fit within each platform's character limit (Bluesky: 300 characters; Mastodon: 500 characters, configurable per instance).
- **FR-006**: System MUST truncate text at word boundaries and append an ellipsis ("…") when shortening is applied.
- **FR-007**: System MUST preserve hashtags during shortening, removing trailing hashtags first before truncating body text.
- **FR-008**: System MUST support attaching up to 4 images per post (the maximum both platforms support).
- **FR-022**: System MUST support images provided as URLs in a JSON request body; the server MUST download the image from the URL before uploading to platforms.
- **FR-023**: System MUST support images uploaded as binary file data via `multipart/form-data` requests.
- **FR-024**: System MUST validate that URL-referenced images are reachable and return a clear error if the download fails (e.g., 404, timeout, unreachable host).
- **FR-025**: System MUST retry transient platform errors (network timeouts, 5xx responses) using a configurable retry count with exponential backoff before returning a failure; retry settings (count, initial delay) MUST be configurable via Aspire AppHost configuration.
- **FR-026**: System MUST build platform-specific rich text metadata where required (e.g., Bluesky facets for hashtags, links, and mentions) so that hashtags and links are rendered as clickable elements on every platform.
- **FR-009**: System MUST require alt text for every attached image; requests with images missing alt text MUST be rejected with a validation error.
- **FR-010**: System MUST reject alt text that is empty or whitespace-only.
- **FR-011**: System MUST support hashtags provided inline in the post text.
- **FR-012**: System MUST support hashtags provided as a separate list in the request, appending them to the post text.
- **FR-013**: System MUST de-duplicate hashtags when provided both inline and as a separate list.
- **FR-014**: System MUST auto-prefix hashtags from the separate list with `#` if not already present.
- **FR-015**: System MUST validate image formats and reject unsupported types with a clear error message listing accepted formats (JPEG, PNG, GIF, WebP).
- **FR-016**: System MUST return a response indicating per-platform success or failure, including error details for any failed platform.
- **FR-017**: System MUST NOT block a successful post on one platform due to a failure on the other platform.
- **FR-018**: System MUST validate that post text is not empty when no images are attached.
- **FR-019**: System MUST accept image-only posts (no text) provided alt text is supplied for all images.
- **FR-020**: System MUST support authenticated access via a pre-shared API key passed in the `X-Api-Key` request header; requests without a valid key MUST be rejected with HTTP 401.
- **FR-021**: System MUST NOT persist posts or images locally; all post data is transient and returned in the HTTP response only (fire-and-forget).

### Key Entities

- **Post**: Represents a social media post request. Key attributes: text content, target platforms, status. A Post has zero or more Images and zero or more Hashtags.
- **Image**: Represents an image attachment on a Post. Key attributes: image data (or URL), alt text (mandatory), media type. Belongs to exactly one Post.
- **Hashtag**: Represents a discoverable topic tag. Key attributes: tag text (without `#` prefix). Can be associated with a Post either inline in the text or as a separate list item.
- **Platform**: Represents a target social media service (Bluesky or Mastodon). Key attributes: name, character limit, maximum image count, authentication credentials.
- **PostResult**: Represents the outcome of publishing to a single platform. Key attributes: platform name, success/failure status, platform-specific post identifier (on success), error message (on failure). A Post submission produces one PostResult per targeted Platform.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can compose and publish a post to both platforms in a single request, completing the entire flow in under 10 seconds under normal network conditions.
- **SC-002**: 100% of posts exceeding a platform's character limit are automatically shortened to comply, with no truncation mid-word.
- **SC-003**: 100% of image attachments have alt text validated before upload; posts with missing or blank alt text are rejected prior to contacting any platform.
- **SC-004**: When one platform fails, the other platform's post still succeeds, and the user receives a clear per-platform status in 100% of partial-failure cases.
- **SC-005**: Hashtags from both inline text and the separate list appear correctly as clickable tags on both platforms in 100% of posts containing hashtags.
- **SC-006**: The system supports all four accepted image formats (JPEG, PNG, GIF, WebP) and rejects unsupported formats with a descriptive error in 100% of cases.

## Assumptions

- **A-001**: The Mastodon instance character limit defaults to 500 characters but may be configurable if a different instance is used; the system should allow this to be configured.
- **A-002**: Image size limits follow each platform's defaults (Bluesky: ~1 MB per image; Mastodon: ~8 MB for images). The system will validate against these limits and return clear errors when exceeded.
- **A-003**: Authentication credentials for each platform (Bluesky app password; Mastodon OAuth2 access token) are pre-configured and managed through the Aspire AppHost configuration — not passed per request.
- **A-008**: The API key used for endpoint authentication is stored in the Aspire AppHost User Secrets and validated on every request via the `X-Api-Key` header.
- **A-004**: The API is intended for a single user (the owner); multi-tenant or multi-user support is out of scope for the MVP.
- **A-007**: The API is fire-and-forget for the MVP; posts are not persisted locally. Post results are returned in the HTTP response only. Local storage and post history will be added in a future iteration.
- **A-005**: Rate limiting is handled by the platforms themselves; the system will surface rate-limit errors from the platform APIs in the response rather than implementing its own rate limiter.
- **A-009**: Default retry configuration is 3 attempts with exponential backoff (initial delay ~1 second). Retries apply only to transient errors (network timeouts, HTTP 5xx); non-transient errors (4xx) are not retried.
- **A-006**: The API exposes HTTP endpoints (not a CLI or UI); consumption is via HTTP clients, scripts, or automation tools.

## Clarifications

### Session 2026-02-28

- Q: What is the default behaviour when no target platforms are specified? → A: Default to all configured platforms (not hardcoded to Bluesky + Mastodon).
- Q: Should the API persist posts locally or be fire-and-forget? → A: Fire-and-forget for MVP; no local storage. Persistence will be added later.
- Q: What authentication mechanism should protect the API endpoints? → A: Pre-shared API key via `X-Api-Key` request header, stored in Aspire User Secrets.
- Q: How should images be sent to the API in the post request? → A: Support both multipart form upload and URL references.
- Q: Should the API retry automatically on platform failure? → A: Yes, configurable retry count with exponential backoff for transient errors.
- Q: Should the spec acknowledge Bluesky's rich text facets requirement? → A: Yes, add a requirement for platform-specific rich text metadata so hashtags and links are clickable.

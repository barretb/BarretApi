# Feature Specification: NASA Astronomy Picture of the Day Social Posting

**Feature Branch**: `001-nasa-apod-post`  
**Created**: 2026-03-08  
**Status**: Draft  
**Input**: User description: "Implement a new endpoint that when called, it will take the NASA Astronomy Picture of the Day and post it to the selected social media platforms"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Post Today's APOD to Social Platforms (Priority: P1)

As a content curator, I want to trigger an API call that fetches today's NASA Astronomy Picture of the Day and posts it (with its title and image) to my chosen social media platforms, so I can share interesting astronomy content with my audience without manual effort.

**Why this priority**: This is the core feature — without it, nothing else matters. It delivers the fundamental value of automating APOD sharing.

**Independent Test**: Can be fully tested by calling the endpoint with no date parameter and verifying that the current day's APOD is fetched from NASA and posted to at least one configured platform.

**Acceptance Scenarios**:

1. **Given** the NASA APOD API is reachable and returns today's image, **When** the user calls the endpoint with platforms `["bluesky", "mastodon"]`, **Then** the system fetches today's APOD, constructs a post with the title and link, attaches the image, and posts to both Bluesky and Mastodon, returning success results for each platform.
2. **Given** the NASA APOD API is reachable and returns today's image, **When** the user calls the endpoint without specifying platforms, **Then** the system posts to all configured platforms.
3. **Given** the NASA APOD API is reachable, **When** the user calls the endpoint, **Then** the response includes the APOD title, date, the image URL used, and per-platform posting results.

---

### User Story 2 - Post a Specific Date's APOD (Priority: P2)

As a content curator, I want to optionally specify a date so I can post a particular day's APOD instead of only today's, allowing me to share noteworthy past images.

**Why this priority**: Extends the core feature with flexibility. The MVP works without it (defaults to today), but it adds valuable control.

**Independent Test**: Can be tested by calling the endpoint with a specific past date and verifying the returned APOD matches that date.

**Acceptance Scenarios**:

1. **Given** a valid date `2026-02-14` is provided, **When** the user calls the endpoint, **Then** the system fetches the APOD for February 14, 2026 and posts it to the selected platforms.
2. **Given** an invalid date (future date or before 1995-06-16) is provided, **When** the user calls the endpoint, **Then** the system returns a validation error with a clear message.
3. **Given** no date is provided, **When** the user calls the endpoint, **Then** the system defaults to today's date.

---

### User Story 3 - Graceful Handling of Video APOD (Priority: P2)

As a content curator, I want the system to handle days when the APOD is a video (not an image) gracefully, so the post still goes out with a meaningful representation.

**Why this priority**: The APOD is sometimes a video (typically a YouTube embed) rather than a static image. Without handling this, the feature would silently fail or produce broken posts on those days.

**Independent Test**: Can be tested by calling the endpoint with a known video APOD date and verifying the post includes the video thumbnail (if available) or posts text-only with the video link.

**Acceptance Scenarios**:

1. **Given** the APOD for the requested date is a video with a thumbnail available, **When** the user calls the endpoint, **Then** the system uses the video thumbnail as the post image and includes the video URL in the post text.
2. **Given** the APOD for the requested date is a video without a thumbnail, **When** the user calls the endpoint, **Then** the system posts text-only content with the APOD title and video URL.

---

### User Story 4 - Copyright Attribution (Priority: P3)

As a content curator, I want the system to include copyright attribution when the APOD image is not public domain, so I respect intellectual property when sharing.

**Why this priority**: Important for legal compliance but does not block core functionality. Most APOD images are NASA public domain, so this is a refinement.

**Independent Test**: Can be tested by calling the endpoint with a date known to have a copyrighted APOD and verifying the post text includes credit.

**Acceptance Scenarios**:

1. **Given** the APOD has a copyright field, **When** the system constructs the post, **Then** the post text includes a credit line (e.g., "Credit: [copyright holder]").
2. **Given** the APOD has no copyright field (public domain), **When** the system constructs the post, **Then** no credit line is included.

---

### Edge Cases

- What happens when the NASA APOD API is unreachable or returns an error? The system returns a clear error response indicating the upstream failure.
- What happens when the NASA APOD API rate limit is exceeded? The system returns an error with an appropriate message indicating rate limiting.
- What happens when the APOD image URL is broken or the image cannot be downloaded? The system falls back to a text-only post and reports the image download failure in the response.
- What happens when the APOD image exceeds a platform's maximum upload size? The system resizes the image to fit within the platform's size limit while maintaining aspect ratio, then uploads the resized image.
- What happens when all targeted platforms fail? The system returns a 502 response with per-platform error details.
- What happens when some platforms succeed and some fail? The system returns a 207 partial-success response with individual results.
- What happens when the post text (title + explanation excerpt + URL) exceeds a platform's character limit? The system truncates the text using the existing text-shortening logic.

## Clarifications

### Session 2026-03-08

- Q: How should the system handle images that exceed a platform's maximum upload size? → A: Resize the image smaller so it can be posted.
- Q: What alt text should be used for posted images? → A: Use the APOD explanation (description) from the NASA API as the image alt text.
- Q: Should the APOD explanation be included in the social post text? → A: No. Explanation is used as image alt text only. Post text is Title + URL + optional Credit.
- Q: What image format should resized images be saved as? → A: Always JPEG (best compression for photos, no transparency needed).
- Q: Should the system prefer standard-resolution or HD image for the social post image? → A: Use standard-resolution (`url`) for the attached image; include HD image link (`hdurl`) in the post text when available.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST expose an endpoint that accepts a request to fetch a NASA APOD and post it to social media platforms.
- **FR-002**: System MUST call the NASA APOD API (`GET https://api.nasa.gov/planetary/apod`) to retrieve the picture of the day data.
- **FR-003**: System MUST authenticate to the NASA APOD API using a configured API key.
- **FR-004**: System MUST default to today's APOD when no date is specified in the request.
- **FR-005**: System MUST allow the caller to specify an optional date (YYYY-MM-DD format) to retrieve a specific day's APOD.
- **FR-006**: System MUST validate that the requested date is not in the future and not before 1995-06-16 (the first APOD).
- **FR-007**: System MUST construct a social post containing the APOD title and the APOD page URL or image URL. The APOD explanation is NOT included in the post text (it is used as image alt text only).
- **FR-008**: System MUST attach the APOD image to the social post when the media type is "image".
- **FR-009**: System MUST use the standard-resolution image URL (`url`) for the attached post image to minimize download and resize overhead. The HD image URL (`hdurl`) is NOT used for the attachment.
- **FR-021**: System MUST include the HD image download link (`hdurl`) in the post text when available, so viewers can access the full-resolution image.
- **FR-018**: System MUST resize images that exceed a platform's maximum upload size (e.g., Bluesky 1 MB, Mastodon 16 MB, LinkedIn 20 MB) to fit within the limit while maintaining aspect ratio, rather than rejecting the image.
- **FR-019**: System MUST set the image alt text to the APOD `explanation` field from the NASA API response, truncated to the platform's maximum alt text length if necessary (Bluesky: 1,000 chars, Mastodon: 1,500 chars, LinkedIn: 4,086 chars).
- **FR-020**: System MUST convert resized images to JPEG format regardless of the original format, as APOD photographs do not require transparency.
- **FR-010**: System MUST handle video-type APODs by using the thumbnail URL (if available via `thumbs=True`) or posting text-only with the video link.
- **FR-011**: System MUST include copyright attribution in the post text when the APOD response contains a `copyright` field.
- **FR-012**: System MUST allow the caller to specify which platforms to post to; if none are specified, the system posts to all configured platforms.
- **FR-013**: System MUST return per-platform results (success/failure, post ID, post URL, or error details) in the response.
- **FR-014**: System MUST return the selected APOD metadata (title, date, image URL, media type) in the response.
- **FR-015**: System MUST apply existing text-shortening and hashtag logic to fit platform character limits.
- **FR-016**: System MUST require API key authentication (same as existing endpoints) for the new endpoint.
- **FR-017**: System MUST return appropriate error responses when the NASA API is unreachable, rate-limited, or returns an error.

### Key Entities

- **APOD Entry**: Represents a single Astronomy Picture of the Day — includes title, date, explanation, image URL, HD image URL, media type (image or video), thumbnail URL, and optional copyright holder.
- **APOD Post Request**: The caller's request specifying an optional date and optional list of target platforms.
- **APOD Post Response**: The result containing the APOD metadata and per-platform posting results.

## Assumptions

- The NASA API key will be stored as a configuration secret managed through the existing Aspire AppHost, consistent with how other secrets are handled.
- The existing social posting infrastructure (text shortening, hashtag processing, image downloading, platform clients for Bluesky, Mastodon, and LinkedIn) will be reused.
- The APOD explanation text will be used to derive hashtags (e.g., extracting astronomy-related keywords), following the existing hashtag service conventions.
- The post text format will be: `{Title}\n{HD Image URL or APOD URL}` with optional `Credit: {copyright}` line. When `hdurl` is available, it is used as the link in the post text; otherwise the standard `url` is used.
- Rate limits for the NASA API (1,000 requests/hour with a registered key) are sufficient for this use case, as the endpoint is manually triggered.
- The APOD API sometimes returns videos (typically YouTube embeds). The `thumbs=True` parameter will be sent to request thumbnail URLs for video content.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user can trigger an APOD post to all configured platforms in a single API call and receive a response within 30 seconds.
- **SC-002**: The posted content includes the APOD image (or video thumbnail) on 100% of image-type APODs.
- **SC-003**: The posted content correctly attributes copyright on 100% of copyrighted APODs.
- **SC-004**: When one or more platforms fail, the user receives per-platform error details and successful posts are not rolled back.
- **SC-005**: The endpoint returns a clear, actionable error when the NASA API is unavailable, rather than an opaque server error.
- **SC-006**: The endpoint validates date input and rejects invalid dates with a user-friendly message within 1 second.

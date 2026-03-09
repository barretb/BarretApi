# Feature Specification: NASA GIBS Ohio Satellite Image Social Posting

**Feature Branch**: `003-nasa-gibs-post`  
**Created**: 2026-03-09  
**Status**: Draft  
**Input**: User description: "I want to add another NASA api endpoint. I want to retrieve the most recent satellite image from NASA GIBS API of the state of Ohio and post it to social media with a description in a manner similar to the APODS endpoint we just added"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Post Recent Ohio Satellite Image to Social Platforms (Priority: P1)

As a content curator, I want to trigger an API call that fetches the most recent satellite image of Ohio from NASA GIBS and posts it (with a descriptive caption) to my chosen social media platforms, so I can share interesting Earth observation imagery with my audience without manual effort.

**Why this priority**: This is the core feature — without it, nothing else matters. It delivers the fundamental value of automating the retrieval and sharing of satellite imagery of Ohio.

**Independent Test**: Can be fully tested by calling the endpoint with no date parameter and verifying that a recent satellite image of Ohio is fetched from NASA GIBS and posted to at least one configured platform.

**Acceptance Scenarios**:

1. **Given** the NASA GIBS service is reachable and returns an image for the most recent available date, **When** the user calls the endpoint with platforms `["bluesky", "mastodon"]`, **Then** the system fetches a satellite image of Ohio, constructs a post with a descriptive caption, attaches the image, and posts to both Bluesky and Mastodon, returning success results for each platform.
2. **Given** the NASA GIBS service is reachable, **When** the user calls the endpoint without specifying platforms, **Then** the system posts to all configured platforms.
3. **Given** the NASA GIBS service is reachable, **When** the user calls the endpoint, **Then** the response includes the date of the image, the imagery layer used, whether an image was attached, and per-platform posting results.

---

### User Story 2 - Post a Specific Date's Ohio Satellite Image (Priority: P2)

As a content curator, I want to optionally specify a date so I can post a particular day's satellite image of Ohio instead of only the most recent, allowing me to share notable imagery from specific dates (e.g., weather events, seasonal changes).

**Why this priority**: Extends the core feature with flexibility. The MVP works without it (defaults to the most recent date), but it adds valuable control for curating content around specific events.

**Independent Test**: Can be tested by calling the endpoint with a specific past date and verifying the returned image corresponds to that date.

**Acceptance Scenarios**:

1. **Given** a valid date `2026-02-14` is provided, **When** the user calls the endpoint, **Then** the system fetches the satellite image of Ohio for February 14, 2026 and posts it to the selected platforms.
2. **Given** an invalid date (future date or unreasonably old date) is provided, **When** the user calls the endpoint, **Then** the system returns a validation error with a clear message.
3. **Given** no date is provided, **When** the user calls the endpoint, **Then** the system defaults to the most recent available date (typically yesterday, as satellite data has a processing delay).

---

### User Story 3 - Configurable Imagery Layer (Priority: P3)

As a content curator, I want to optionally specify which satellite imagery layer to use (e.g., true color, corrected reflectance from different instruments), so I can choose the most visually appealing or scientifically relevant imagery for a given post.

**Why this priority**: The default true-color layer covers the vast majority of use cases. This is a refinement that adds variety and flexibility for power users.

**Independent Test**: Can be tested by calling the endpoint with a specific layer identifier and verifying the response indicates the requested layer was used.

**Acceptance Scenarios**:

1. **Given** a valid GIBS layer identifier `VIIRS_SNPP_CorrectedReflectance_TrueColor` is provided, **When** the user calls the endpoint, **Then** the system uses that layer for the satellite image.
2. **Given** no layer is specified, **When** the user calls the endpoint, **Then** the system uses the default configured layer (MODIS Terra Corrected Reflectance True Color).
3. **Given** an invalid or unsupported layer identifier is provided, **When** the user calls the endpoint, **Then** the system returns a validation error listing the supported layers.

---

### Edge Cases

- What happens when the NASA GIBS service is unreachable or returns an error? The system returns a clear error response indicating the upstream failure.
- What happens when the requested date's imagery is not yet available (e.g., today's date when data hasn't been processed)? The system returns an informative error explaining that imagery for the requested date is not yet available and suggests trying a previous date.
- What happens when the satellite image for the requested date is mostly or entirely cloud-covered? The system posts the image as-is. Cloud cover is a natural part of satellite imagery and is noted in the post caption.
- What happens when the GIBS snapshot image exceeds a platform's maximum upload size? The system resizes the image using the existing image resizer to fit within the platform's size limit.
- What happens when all targeted platforms fail? The system returns a 502 response with per-platform error details.
- What happens when some platforms succeed and some fail? The system returns a 207 partial-success response with individual results.
- What happens when the post caption exceeds a platform's character limit? The system truncates the text using the existing text-shortening logic.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST expose an endpoint that accepts a request to fetch a NASA GIBS satellite image of Ohio and post it to social media platforms.
- **FR-002**: System MUST retrieve the satellite image from NASA GIBS using the Worldview Snapshot API (`https://wvs.earthdata.nasa.gov/api/v1/snapshot`) with a `GetSnapshot` request, specifying Ohio's bounding box, the selected imagery layer, date, and output format.
- **FR-003**: System MUST use a pre-configured bounding box for the state of Ohio (approximately latitude 38.40°–42.32° N, longitude 84.82°–80.52° W in EPSG:4326).
- **FR-004**: System MUST default to the most recent available date when no date is specified in the request. Since satellite imagery typically has a 1–2 day processing delay, the system defaults to yesterday's date.
- **FR-005**: System MUST allow the caller to specify an optional date (YYYY-MM-DD format) to retrieve a specific day's satellite image.
- **FR-006**: System MUST validate that the requested date is not in the future and not before the earliest available imagery date for the configured layer.
- **FR-007**: System MUST construct a social post containing a descriptive caption including the date, imagery layer name, and a link to the NASA Worldview application showing the same view.
- **FR-008**: System MUST attach the satellite image to the social post with appropriate alt text describing the image content (e.g., "Satellite image of Ohio captured on {date} by {instrument}").
- **FR-009**: System MUST use a configurable default imagery layer, defaulting to `MODIS_Terra_CorrectedReflectance_TrueColor` (true-color satellite imagery).
- **FR-010**: System MUST allow the caller to optionally specify a supported imagery layer identifier to override the default.
- **FR-011**: System MUST validate that the specified layer is in the list of supported layers and return an error if not.
- **FR-012**: System MUST resize images that exceed a platform's maximum upload size using the existing image resizer, maintaining aspect ratio.
- **FR-013**: System MUST allow the caller to specify which platforms to post to; if none are specified, the system posts to all configured platforms.
- **FR-014**: System MUST return per-platform results (success/failure, post ID, post URL, or error details) in the response.
- **FR-015**: System MUST return metadata in the response including the date, layer used, image dimensions, and whether the image was resized.
- **FR-016**: System MUST apply existing text-shortening and hashtag logic to fit platform character limits.
- **FR-017**: System MUST require API key authentication (same as existing endpoints) for the new endpoint.
- **FR-018**: System MUST return appropriate error responses when the NASA GIBS service is unreachable or returns an error.
- **FR-019**: System MUST include a NASA GIBS acknowledgement in the post text or caption, per NASA's data use guidelines: imagery is provided by NASA's Global Imagery Browse Services (GIBS).
- **FR-020**: System MUST request the snapshot image in JPEG format for optimal file size and social media compatibility.
- **FR-021**: System MUST request a snapshot image of sufficient resolution for social media posting (at least 1024×768 pixels).

### Key Entities

- **GIBS Snapshot Request**: Represents a request to the NASA GIBS Worldview Snapshot API — includes date, layer identifier, bounding box, image dimensions, and output format.
- **Ohio Satellite Post Request**: The caller's request specifying an optional date, optional layer, and optional list of target platforms.
- **Ohio Satellite Post Response**: The result containing the image metadata (date, layer, dimensions, resized status) and per-platform posting results.

## Assumptions

- The NASA GIBS Worldview Snapshot API is publicly accessible and does not require an API key or authentication, unlike the NASA APOD API.
- The Ohio bounding box will be stored as configuration values in the Aspire AppHost, consistent with how other configuration is managed. This allows future extension to other geographic regions.
- The existing social posting infrastructure (text shortening, hashtag processing, image resizing, platform clients for Bluesky, Mastodon, and LinkedIn) will be reused.
- The default imagery layer (`MODIS_Terra_CorrectedReflectance_TrueColor`) provides daily global coverage and is the most commonly used true-color layer. MODIS Terra data is typically available with a 1–2 day delay.
- The list of supported imagery layers will be a curated, configured set rather than dynamically queried from GIBS capabilities, to ensure only visually meaningful layers are offered.
- The post caption format will be: `"Satellite view of Ohio — {date}\nImagery: {layer name}\n{Worldview link}\n\nImagery: NASA GIBS"` with optional hashtags like `#Ohio #satellite #NASA #EarthObservation`.
- The Worldview link will be constructed to open NASA Worldview centered on Ohio for the same date, providing viewers an interactive way to explore the imagery.
- Snapshot image dimensions of 1024×768 will be the default, providing a good balance between quality and file size for social media platforms.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user can trigger an Ohio satellite image post to all configured platforms in a single API call and receive a response within 30 seconds.
- **SC-002**: The posted content includes the satellite image attached on 100% of successful requests where the GIBS service returns valid imagery.
- **SC-003**: The posted content includes proper NASA GIBS acknowledgement on 100% of posts.
- **SC-004**: When one or more platforms fail, the user receives per-platform error details and successful posts are not rolled back.
- **SC-005**: The endpoint returns a clear, actionable error when the NASA GIBS service is unavailable, rather than an opaque server error.
- **SC-006**: The endpoint validates date and layer input and rejects invalid values with a user-friendly message within 1 second.
- **SC-007**: Users can choose from at least 3 different satellite imagery layers to vary the visual content of their posts.

# Feature Specification: Signboard Image Generator

**Feature Branch**: `010-signboard-generator`
**Created**: 2026-07-28
**Status**: Draft
**Input**: User description: "I want to be able to generate images that look like signboards that would be in front of a business (a white lightbox letterboard with plastic tile letters). It will allow me to pass in a string of text, convert that text into words on the signboard in the style of letter tiles, and return that as a generated PNG image. Optionally, it should allow me to use the social media posting tools to post the image to social media."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Generate Signboard PNG (Priority: P1)

As an API consumer, I want to submit a text message and receive a PNG image of that message rendered on a letterboard-style lightbox sign, so that I can create eye-catching quote/joke images without manual graphic design work.

**Why this priority**: This is the core value proposition — turning text into a finished signboard image. The posting story builds on this foundation.

**Independent Test**: Can be fully tested by submitting a text string and verifying the response is a valid PNG of the requested dimensions showing the text rendered as uppercase letter tiles on a white lightbox board with a dark frame.

**Acceptance Scenarios**:

1. **Given** the endpoint is available, **When** I submit a request with text "I used to think I was indecisive but now I'm not sure", **Then** the system returns a PNG image (`Content-Type: image/png`) showing the text in uppercase letter-tile style, centered line by line on a white board with a dark frame and visible horizontal track lines.
2. **Given** the endpoint is available, **When** I submit text containing explicit newline characters, **Then** each newline forces a line break at that position and the resulting lines are individually centered.
3. **Given** the endpoint is available, **When** I submit text without newlines that is too wide for one line, **Then** the text automatically wraps at word boundaries into multiple centered lines.
4. **Given** the endpoint is available, **When** I submit the same text with the same `seed` value twice, **Then** both responses are byte-identical PNG images (identical accent-letter placement and letter jitter).

---

### User Story 2 - Custom Image Dimensions (Priority: P2)

As an API consumer, I want to optionally control the output image dimensions so that the image suits different destinations (social posts, blog headers).

**Why this priority**: Sensible defaults cover most uses; custom sizing adds flexibility but is not required for the core experience.

**Independent Test**: Submit a request with explicit `width` and `height` values and verify the returned PNG has exactly those pixel dimensions.

**Acceptance Scenarios**:

1. **Given** the endpoint is available, **When** I submit text with `width: 1600` and `height: 1200`, **Then** the returned PNG is exactly 1600×1200 pixels.
2. **Given** the endpoint is available, **When** I omit `width` and `height`, **Then** the returned PNG is 1200×900 pixels (defaults).
3. **Given** the endpoint is available, **When** I submit dimensions outside the allowed range (below 400 or above 2000), **Then** the system returns a 400 validation error.

---

### User Story 3 - Post Signboard to Social Media (Priority: P2)

As an API consumer, I want to optionally post the generated signboard image directly to my configured social platforms (Bluesky, Mastodon, LinkedIn), so that one request generates and publishes the image.

**Why this priority**: Reuses the platform-posting capability the API already provides for other image sources (NASA APOD, satellite). High-value convenience, but the generate-only flow is independently useful.

**Independent Test**: Submit a request with `platforms` populated and verify the response is JSON containing per-platform results, and the image is attached to the created posts.

**Acceptance Scenarios**:

1. **Given** at least one social platform is configured, **When** I submit text with `platforms: ["bluesky"]`, **Then** the system generates the signboard PNG, posts it to Bluesky with the caption text and alt text, and returns a JSON response with the per-platform result (not the raw PNG).
2. **Given** two platforms are targeted and one fails, **When** I submit the request, **Then** the system returns HTTP 207 with per-platform success/failure details, matching the behavior of the other posting endpoints.
3. **Given** all targeted platforms fail, **When** I submit the request, **Then** the system returns HTTP 502 with per-platform error details.
4. **Given** I supply a `caption`, **Then** the caption (plus any `hashtags`) is used as the post body text; **Given** I omit the caption, **Then** the sign text is used as the post body text.
5. **Given** I omit `altText`, **Then** the image alt text defaults to "A signboard that reads: {text}".

---

### Edge Cases

- What happens when the text is empty or whitespace-only? The system rejects the request with a 400 validation error.
- What happens when the text contains characters outside the supported letter-tile set (e.g., emoji, non-Latin scripts)? The system returns a 400 validation error listing the unsupported characters.
- What happens when the text is very long (approaching the 200-character limit) on a small board? The tile size scales down so all lines fit within the board; the request is still rejected if text exceeds 200 characters.
- What happens when a single word is longer than the widest possible line? Tile size scales down until the word fits (no mid-word breaking at the default size unless unavoidable).
- What happens when lowercase text is submitted? It is rendered uppercase (letterboard tiles are uppercase only); the request is not rejected.
- What happens when `platforms` contains an unknown platform name? The system returns a 400 validation error, consistent with other posting endpoints.
- What happens when `caption`, `hashtags`, or `altText` are supplied without `platforms`? They are ignored (the response is the raw PNG).
- What happens when rendering fails unexpectedly? The system returns a 500 error with a generic message and logs the failure.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST expose an authenticated endpoint (`X-Api-Key`) that accepts a required text string and returns a generated signboard image.
- **FR-002**: System MUST validate that text is non-empty, non-whitespace, and at most 200 characters.
- **FR-003**: System MUST restrict text to the supported tile character set — letters A–Z (either case), digits 0–9, space, newline, and the punctuation `! ? . , ' " & @ # $ % - + / : ;` — and reject other characters with a 400 error naming the offending characters.
- **FR-004**: System MUST render text in uppercase letter-tile style on a white lightbox board with a dark rounded frame, subtle lightbox shading, and horizontal track lines behind each letter row.
- **FR-005**: System MUST render each letter with slight per-letter position/rotation jitter and MUST render a random minority of letters (roughly 1 in 8) in an accent color (red, occasionally gray) to mimic a real letterboard.
- **FR-006**: System MUST honor explicit newline characters as forced line breaks and MUST auto-wrap text at word boundaries when a line exceeds the board width.
- **FR-007**: System MUST center each line horizontally and center the block of lines vertically on the board, scaling tile size down until the longest line and all rows fit within the board face.
- **FR-008**: System MUST accept an optional integer `seed`; identical text + seed + dimensions MUST produce byte-identical output. When omitted, a random seed is used.
- **FR-009**: System MUST accept optional `width` and `height` (400–2000 px each, defaults 1200×900) and return a PNG of exactly those dimensions.
- **FR-010**: When `platforms` is absent or empty, the system MUST return the raw PNG bytes with `Content-Type: image/png`.
- **FR-011**: When `platforms` is present and non-empty, the system MUST post the generated image to the targeted platforms (`bluesky`, `mastodon`, `linkedin`) using the existing social posting services, and return a JSON response with per-platform results using the established 200/207/502 semantics.
- **FR-012**: When posting, the post body text MUST be the supplied `caption` (falling back to the sign text) with any supplied `hashtags` appended; image alt text MUST be the supplied `altText` (falling back to "A signboard that reads: {text}").
- **FR-013**: System MUST resize/re-encode the image as needed to satisfy per-platform image size limits, reusing the existing image resizing behavior used by other image-posting endpoints.
- **FR-014**: System MUST use a bundled condensed sans-serif font with an open license (embedded resource), not a system-installed font.
- **FR-015**: System MUST return appropriate error responses: 400 (validation), 401 (missing/invalid API key), 500 (rendering failure), 502 (all platforms failed).

### Key Entities

- **Signboard Request**: Input DTO — text (required); width, height, seed (optional rendering controls); platforms, caption, hashtags, altText (optional posting controls).
- **Signboard Generation Command**: Core-layer input to the generator — normalized text, dimensions, seed.
- **Signboard Generator**: Core interface with SkiaSharp implementation in Infrastructure that produces PNG bytes from a command.
- **Signboard Post Result**: JSON response for posting mode — echo of dimensions/seed plus the standard per-platform result list.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A valid generate-only request returns a finished PNG in under 3 seconds.
- **SC-002**: 100% of generated images render all submitted characters legibly within the board face with no letters overlapping the frame.
- **SC-003**: Identical text + seed + dimensions produce byte-identical images across requests.
- **SC-004**: Posting mode succeeds against configured platforms with the same reliability semantics (200/207/502) as existing posting endpoints.
- **SC-005**: The rendered style is recognizable as a lightbox letterboard (white board, dark frame, track lines, tile letters with occasional red accents) at both full size and social-feed thumbnail size.

## Assumptions

- The letterboard aesthetic is fixed (white board, dark frame, black letters with red/gray accents); board and letter colors are not user-configurable in this release.
- Only the board face (with frame) is rendered — no legs, ground, or background scene.
- An OFL-licensed condensed font (e.g., Oswald Bold) will be added as an embedded resource in the Infrastructure project, following the existing JetBrains Mono pattern.
- Accent-letter frequency (~1 in 8) and jitter magnitude are implementation-tuned constants, not request parameters.
- Scheduled/deferred posting is out of scope; posting is immediate only (matching NASA APOD behavior).
- The endpoint is stateless — no persistence of generated images.

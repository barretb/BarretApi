# Feature Specification: Hero Image Generator

**Feature Branch**: `009-hero-image-generator`  
**Created**: 2026-04-10  
**Status**: Draft  
**Input**: User description: "I want to design a new feature endpoint. This endpoint will be used to generate hero images for blog posts and YouTube videos. It should feature my face (file images/barretcircle2.png) in the lower right of the image, and my logo (file images/barret-blake-logo-1024.png) in the lower left. It should take as input a title string, a subtitle string (optional), and a background image upload (optional). If no background image is uploaded, it should use the generic background image (file images/generic-background.jpg). The background image should be faded so the text and face and logo images stand out. Select a good font that is readable and stands out and suggests a tech theme. The title should be larger than the subtitle, and both text fields when added to the image should fit between the face and logo images without overlap."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Generate Hero Image with Title Only (Priority: P1)

As a content creator, I want to submit a title and receive a branded hero image so that I can quickly produce consistent visual assets for my blog posts and YouTube videos without manual graphic design work.

**Why this priority**: This is the core value proposition — generating a usable hero image with minimal input. Every other story builds on this foundation.

**Independent Test**: Can be fully tested by submitting a title string and verifying the returned image contains the title text, the face image in the lower right, the logo in the lower left, and the generic background (faded). Delivers a complete, usable hero image.

**Acceptance Scenarios**:

1. **Given** the endpoint is available and no background image is provided, **When** I submit a request with a title of "Getting Started with .NET 10", **Then** the system returns an image that includes the title text rendered in a readable tech-themed font, the face image (`images/barretcircle2.png`) positioned in the lower-right area, the logo (`images/barret-blake-logo-1024.png`) positioned in the lower-left area, and the generic background image (`images/generic-background.jpg`) with a faded/dimmed overlay.
2. **Given** the endpoint is available, **When** I submit a request with only a title and no subtitle or background image, **Then** the title text is rendered prominently and centered vertically in the text area between the logo and face images, with no blank subtitle visible.

---

### User Story 2 - Generate Hero Image with Title and Subtitle (Priority: P2)

As a content creator, I want to include an optional subtitle alongside the title so that I can add context like a series name, date, or topic tagline to the hero image.

**Why this priority**: Subtitles add polish and context to hero images and are commonly needed for video thumbnails and blog headers, but the feature is still usable without them.

**Independent Test**: Can be fully tested by submitting a title and subtitle, then verifying both text elements appear on the generated image with correct sizing and positioning.

**Acceptance Scenarios**:

1. **Given** the endpoint is available, **When** I submit a request with title "Blazor Deep Dive" and subtitle "Part 3: Component Lifecycle", **Then** the returned image displays the title in a larger font size above or more prominently than the subtitle, and both text elements fit within the space between the logo and face images without overlapping either image.
2. **Given** the endpoint is available, **When** I submit a title and a subtitle that are both very long strings, **Then** the text is scaled or wrapped to fit within the available space between the logo and face images without overflowing or overlapping.

---

### User Story 3 - Generate Hero Image with Custom Background (Priority: P3)

As a content creator, I want to upload my own background image so that I can create hero images that visually relate to the specific content I am producing.

**Why this priority**: Custom backgrounds enhance visual variety and content relevance, but the generic background provides a fully functional default experience.

**Independent Test**: Can be fully tested by uploading a custom background image along with a title, then verifying the returned image uses the uploaded background (faded) instead of the generic one.

**Acceptance Scenarios**:

1. **Given** the endpoint is available, **When** I submit a request with a title and upload a custom background image, **Then** the returned image uses the uploaded image as the background (with the same fade/dim treatment) instead of the generic background.
2. **Given** the endpoint is available, **When** I upload a background image in a common format (JPEG, PNG), **Then** the system accepts the image and produces a valid hero image using it as the background.

---

### Edge Cases

- What happens when the title text is extremely long (e.g., 200+ characters)? The system should scale or truncate the text to fit within the available space without overlapping the face or logo images.
- What happens when the subtitle text alone is very long but the title is short? The subtitle should still fit within the layout without overlapping other elements.
- What happens when an uploaded background image is very small (e.g., 50x50 pixels)? The system should scale it to fill the hero image dimensions, accepting some quality loss.
- What happens when an uploaded background image is very large (e.g., 8000x6000 pixels)? The system should downscale it appropriately without running out of memory.
- What happens when the uploaded file is not a valid image? The system should return a clear error message indicating the file is not a supported image format.
- What happens when the title contains special characters, emoji, or non-Latin scripts? The system should render them correctly or return a clear error if the font does not support them.
- What happens when an empty title is submitted? The system should reject the request and return a validation error.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST expose an endpoint that accepts a title string (required), a subtitle string (optional), and a background image file upload (optional)
- **FR-002**: System MUST validate that the title is a non-empty string with a maximum length of 200 characters
- **FR-003**: System MUST validate that the subtitle, if provided, is a string with a maximum length of 300 characters
- **FR-004**: System MUST validate that the uploaded background image, if provided, is in JPEG or PNG format and does not exceed 10 MB in file size
- **FR-005**: System MUST use the generic background image (`images/generic-background.jpg`) when no custom background image is uploaded
- **FR-006**: System MUST apply a fade/dim overlay to the background image (whether generic or custom) so that foreground elements (text, face, logo) are clearly visible and stand out
- **FR-007**: System MUST composite the face image (`images/barretcircle2.png`) in the lower-right area of the generated image
- **FR-008**: System MUST composite the logo image (`images/barret-blake-logo-1024.png`) in the lower-left area of the generated image
- **FR-009**: System MUST render the title text using a readable, tech-themed font at a size visibly larger than the subtitle
- **FR-010**: System MUST render the subtitle text (when provided) using the same font family at a smaller size than the title
- **FR-011**: System MUST position all text elements within the horizontal space between the logo and face images, with no overlap between text and either image
- **FR-012**: System MUST dynamically scale or wrap text to fit within the available layout area when title or subtitle strings are long
- **FR-013**: System MUST return the generated hero image in a common image format (PNG or JPEG)
- **FR-014**: System MUST return appropriate error responses with descriptive messages for invalid inputs (missing title, unsupported image format, file too large)
- **FR-015**: System MUST generate images at a resolution suitable for both blog post headers and YouTube video thumbnails (minimum 1280x720 pixels)

### Key Entities

- **Hero Image Request**: Represents the input submitted by the user; includes a title (required), subtitle (optional), and background image file (optional)
- **Hero Image**: The generated output image; a composite of background, face overlay, logo overlay, and rendered text
- **Asset Images**: The pre-configured static images used in composition — face image, logo image, and generic background image; stored as files on the server

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can generate a hero image by providing only a title in under 5 seconds from request to image delivery
- **SC-002**: 100% of generated hero images display the face image in the lower right, the logo in the lower left, and text between them with no overlapping elements
- **SC-003**: Generated hero images are at least 1280x720 pixels, meeting standard blog and video thumbnail resolution requirements
- **SC-004**: Text on generated images is legible at standard viewing distances (full-size desktop browser and YouTube thumbnail grid)
- **SC-005**: The background image (generic or uploaded) is visibly faded so that all foreground elements maintain clear contrast and readability
- **SC-006**: 95% of requests with valid input complete successfully without errors

## Assumptions

- The face image (`images/barretcircle2.png`), logo image (`images/barret-blake-logo-1024.png`), and generic background image (`images/generic-background.jpg`) are pre-existing assets stored in the repository and available to the service at runtime.
- The output image dimensions default to 1280x720 pixels (standard YouTube thumbnail / blog header size). No user-configurable output dimensions are required for the initial release.
- The fade/dim treatment on the background consists of a semi-transparent dark overlay to reduce background visual noise. The exact opacity is an implementation detail determined during development.
- Text color defaults to white or a light color that contrasts well against the darkened background.
- The endpoint does not require authentication for the initial release (consistent with other endpoints in this API).
- Font selection (a readable, tech-themed font) is determined at implementation time and bundled with the service. The user does not select fonts.

# Feature Specification: LinkedIn Posting Support

**Feature Branch**: `001-linkedin-posting`  
**Created**: 2026-03-01  
**Status**: Draft  
**Input**: User description: "Add the ability to post to LinkedIn in addition to existing Bluesky and Mastodon posting capabilities."

## User Scenarios & Testing *(mandatory)*

<!--
  IMPORTANT: User stories should be PRIORITIZED as user journeys ordered by importance.
  Each user story/journey must be INDEPENDENTLY TESTABLE - meaning if you implement just ONE of them,
  you should still have a viable MVP (Minimum Viable Product) that delivers value.
  
  Assign priorities (P1, P2, P3, etc.) to each story, where P1 is the most critical.
  Think of each story as a standalone slice of functionality that can be:
  - Developed independently
  - Tested independently
  - Deployed independently
  - Demonstrated to users independently
-->

### User Story 1 - Publish to LinkedIn with Existing Post Flow (Priority: P1)

As an API user, I want LinkedIn included as a target platform so one request can publish to LinkedIn using the same endpoint and payload pattern I already use for Bluesky and Mastodon.

**Why this priority**: This is the core requested business value and enables immediate multi-platform expansion.

**Independent Test**: Can be tested by invoking the existing post endpoint with `platforms` containing `linkedin` and verifying a LinkedIn post result is returned without requiring other platform changes.

**Acceptance Scenarios**:

1. **Given** valid LinkedIn credentials are configured, **When** a post request targets `linkedin`, **Then** the system attempts LinkedIn publishing and includes LinkedIn result details in the response.
2. **Given** LinkedIn and at least one existing platform are targeted together, **When** publishing is invoked, **Then** all targeted platforms are processed and each platform returns independent success/failure details.

---

### User Story 2 - Handle LinkedIn Failures Without Blocking Other Platforms (Priority: P2)

As an API user, I want LinkedIn-specific failures to be reported without stopping other platform posts so partial success behavior remains consistent.

**Why this priority**: Reliability and consistency in multi-platform runs prevents one platform outage from blocking all publication.

**Independent Test**: Can be tested by forcing LinkedIn failure while another platform succeeds and verifying response status and platform-level error output match existing partial success behavior.

**Acceptance Scenarios**:

1. **Given** LinkedIn publishing fails but another targeted platform succeeds, **When** posting is invoked, **Then** response indicates partial success and includes a LinkedIn-specific error entry.
2. **Given** LinkedIn is the only target and LinkedIn publishing fails, **When** posting is invoked, **Then** response indicates overall failure with LinkedIn error details.

---

### User Story 3 - Configure LinkedIn Credentials per Environment (Priority: P3)

As an operator, I want LinkedIn credentials configured through existing environment configuration patterns so deployment and secret handling remain consistent across local and production environments.

**Why this priority**: Operational consistency and secure configuration are required for safe rollout.

**Independent Test**: Can be tested by providing valid LinkedIn configuration in one environment, missing configuration in another, and verifying startup/runtime behavior is explicit and actionable.

**Acceptance Scenarios**:

1. **Given** required LinkedIn settings are present, **When** the API starts and posts are requested, **Then** LinkedIn integration is available for publishing.
2. **Given** required LinkedIn settings are missing, **When** LinkedIn posting is requested, **Then** the response reports a clear LinkedIn configuration/authentication failure without exposing sensitive values.

---

### Edge Cases

- LinkedIn target is requested with unsupported content constraints (e.g., text length/format differences) while other platforms remain valid.
- LinkedIn credentials expire or are revoked between successful runs.
- LinkedIn API rate limits are reached during multi-platform posting.
- Request includes `linkedin` plus image attachments that fail upload only for LinkedIn.
- Request uses unknown platform name near `linkedin` typo (e.g., `linkedinn`) and must still process valid targets.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST support `linkedin` as a valid target platform in existing social post request flows.
- **FR-002**: System MUST attempt LinkedIn publishing when `linkedin` is requested explicitly.
- **FR-003**: System MUST include LinkedIn platform result objects in API responses using the same response shape as existing platforms.
- **FR-004**: System MUST preserve existing multi-platform behavior where one platform failure does not stop attempts for other targeted platforms.
- **FR-005**: System MUST return LinkedIn-specific error code and message details when LinkedIn publishing fails.
- **FR-006**: System MUST support environment-based configuration of LinkedIn authentication/settings using existing secure configuration patterns.
- **FR-007**: System MUST prevent sensitive LinkedIn credential values from being returned in API responses or logs.
- **FR-008**: System MUST apply existing platform-selection validation to include `linkedin` as an allowed value.
- **FR-009**: System MUST continue to support current Bluesky and Mastodon behavior without regression.

### Assumptions

- LinkedIn posting can be represented through the same high-level post payload model currently used for other platforms.
- Required LinkedIn app/account permissions are provisioned outside this feature scope.
- Existing authentication for API endpoint access (`X-Api-Key`) remains unchanged.
- Any LinkedIn-specific post formatting constraints are handled as part of platform client logic, not by introducing a new endpoint.

### Key Entities *(include if feature involves data)*

- **LinkedIn Platform Client Configuration**: Environment-managed settings required to authenticate and publish posts to LinkedIn.
- **Platform Post Result**: Per-platform outcome record including success flag, platform identifier, post identifiers/URLs when available, and error details when failed.
- **Social Post Request**: Existing request payload containing text, optional media/hashtags, and target platform list now including `linkedin`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In a test run of at least 20 requests targeting `linkedin`, at least 95% of requests with valid LinkedIn credentials complete with a LinkedIn success result.
- **SC-002**: In mixed-platform requests (`linkedin` plus existing platforms), 100% of responses include a distinct result entry for each requested platform.
- **SC-003**: In controlled failure tests, 100% of LinkedIn failures return explicit LinkedIn error details while unaffected platforms continue processing.
- **SC-004**: Existing Bluesky and Mastodon request success/failure behavior remains unchanged across regression checks for current scenarios.

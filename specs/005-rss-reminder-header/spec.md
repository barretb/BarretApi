# Feature Specification: RSS Reminder Post Header Update

**Feature Branch**: `005-rss-reminder-header`  
**Created**: 2026-03-11  
**Status**: Draft  
**Input**: User description: "Update the RSS blog posting feature such that the reminder post features a header of 'In case you missed it earlier...' and two newlines at the start of the social media post"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Updated Reminder Post Header Text (Priority: P1)

As a content owner, I want reminder posts for previously promoted blog entries to begin with "In case you missed it earlier..." followed by two newlines before the entry title and URL, so that the reminder text is visually distinct and more conversational than the current inline prefix.

**Why this priority**: This is the entire scope of the feature — changing the reminder post text format. Without it, no value is delivered.

**Independent Test**: Can be fully tested by triggering a reminder post for an already-promoted blog entry and verifying the resulting social post text starts with "In case you missed it earlier..." followed by two newline characters before the entry title.

**Acceptance Scenarios**:

1. **Given** a tracked blog entry has a successful initial post, reminder posting is enabled, and the configured reminder delay has elapsed, **When** the RSS promotion endpoint is invoked, **Then** the reminder post text begins with "In case you missed it earlier...\n\n" followed by the entry title and canonical URL.
2. **Given** a reminder post is generated, **When** the post text is examined, **Then** the header "In case you missed it earlier..." appears on its own line, separated from the title by exactly one blank line (two newline characters).
3. **Given** an initial (non-reminder) post is generated, **When** the post text is examined, **Then** the text format remains unchanged — no header is prepended.

---

### Edge Cases

- Reminder post text with a very long entry title still begins with the header and two newlines even if the combined length approaches platform character limits.
- The header text uses an ellipsis ("...") — three literal period characters, not a Unicode ellipsis character (U+2026).
- Initial posts remain unaffected by this change.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST prefix all reminder social post text with the exact string "In case you missed it earlier..." followed by two newline characters (\n\n) before the entry title.
- **FR-002**: System MUST NOT alter the text format of initial (non-reminder) blog promotion posts.
- **FR-003**: The reminder post text MUST follow the format: `In case you missed it earlier...\n\n{Entry Title}\n{Entry Canonical URL}`.
- **FR-004**: The header MUST use three literal ASCII period characters (U+002E) for the ellipsis, not a Unicode ellipsis character.

### Assumptions

- The existing RSS blog promotion endpoint, tracking store, and reminder eligibility logic remain unchanged.
- Platform-specific text shortening and hashtag processing continue to apply after the header is prepended.
- No new configuration parameters are introduced; the header text is a fixed string.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of reminder posts begin with "In case you missed it earlier..." followed by a blank line before the entry title.
- **SC-002**: 100% of initial (non-reminder) posts remain in their current format with no header prepended.
- **SC-003**: The change is verified by unit tests covering both reminder and initial post text construction.

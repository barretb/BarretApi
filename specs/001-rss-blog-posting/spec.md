# Feature Specification: RSS Blog Post Promotion

**Feature Branch**: `001-rss-blog-posting`  
**Created**: 2026-03-01  
**Status**: Draft  
**Input**: User description: "A new endpoint that checks an RSS feed for recent blog posts, posts qualifying entries to social platforms, tracks what has already been posted, and optionally posts a delayed reminder message."

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

### User Story 1 - Post Newly Published Blog Entries (Priority: P1)

As a content owner, I want to trigger one endpoint call that checks my blog RSS feed and posts any newly published entries from a configurable recent-day window so I can promote fresh content quickly without manual review of each platform.

**Why this priority**: Detecting and posting new content is the core business value; without it, the feature does not provide automation value.

**Independent Test**: Can be fully tested by seeding an RSS feed with entries inside and outside the configured day window, invoking the endpoint once, and verifying only qualifying unposted entries are posted.

**Acceptance Scenarios**:

1. **Given** at least one RSS entry is within the configured day window and has not been posted before, **When** the endpoint is invoked, **Then** the system posts that entry to the configured social platforms and records that an initial post has been made.
2. **Given** all RSS entries in the day window were already posted, **When** the endpoint is invoked, **Then** the system does not create duplicate initial posts.
3. **Given** an RSS entry is older than the configured day window, **When** the endpoint is invoked, **Then** the entry is ignored for initial posting.

---

### User Story 2 - Send Delayed Reminder Posts (Priority: P2)

As a content owner, I want an optional follow-up reminder post for recently posted blog entries after a configurable number of hours so I can increase visibility for important posts.

**Why this priority**: Reminder posts add engagement value, but they depend on initial posting behavior and are therefore secondary.

**Independent Test**: Can be tested by creating a tracked entry with an initial-post timestamp, enabling reminder behavior, setting a short reminder delay, invoking the endpoint, and verifying the reminder message is posted once with the required leader text.

**Acceptance Scenarios**:

1. **Given** a tracked entry has an initial post and reminder posting is enabled and the configured reminder delay has elapsed, **When** the endpoint is invoked, **Then** the system posts exactly one reminder with a "Did you miss it earlier?" leader.
2. **Given** reminder posting is disabled, **When** the endpoint is invoked, **Then** no reminder posts are created regardless of elapsed time.
3. **Given** a reminder was already posted for an entry, **When** the endpoint is invoked again, **Then** no additional reminder is created for that entry.

---

### User Story 3 - Enforce Posting Order in One Endpoint Run (Priority: P3)

As a content owner, I want each endpoint run to always process new-post publishing first and reminder publishing second so promotion timing remains predictable.

**Why this priority**: Order consistency reduces surprises and avoids reminder activity overshadowing new-post publication.

**Independent Test**: Can be tested by preparing both eligible new entries and eligible reminders, invoking the endpoint once, and verifying recorded actions show all initial new posts occur before any reminder posts.

**Acceptance Scenarios**:

1. **Given** both new entries and reminder-eligible entries exist in the same run, **When** the endpoint is invoked, **Then** the system completes all initial new-entry posting attempts before starting reminder posting attempts.

---

### Edge Cases

- RSS feed is temporarily unavailable, malformed, or returns no entries.
- Two endpoint invocations happen close together and evaluate the same newly published entry.
- A blog entry has changed title/content but keeps the same canonical link or GUID.
- A reminder becomes eligible in the same run where an initial post for that entry is first created (it must not be reminded immediately).
- Social posting succeeds on some platforms and fails on others for the same entry.
- Configured day-window or reminder-hours values are missing, zero, or negative.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST expose an endpoint that, when called, executes a feed-check and posting workflow.
- **FR-002**: System MUST read blog entries from a configured RSS feed URL.
- **FR-003**: System MUST treat a blog entry as eligible for initial posting only when its publish date is within a configurable number of days from invocation time.
- **FR-004**: System MUST create initial social posts only for eligible entries that have not yet received an initial post record.
- **FR-005**: System MUST persist per-entry posting state in a durable tabular tracking store, including whether an initial post and reminder post have been completed.
- **FR-006**: System MUST support a configurable toggle to enable or disable reminder posting.
- **FR-007**: System MUST support a configurable reminder delay in hours used when reminder posting is enabled.
- **FR-008**: System MUST create a reminder post only for entries with a successful initial post record, no prior reminder post record, and elapsed time greater than or equal to the configured reminder delay.
- **FR-009**: System MUST prefix reminder social post text with "Did you miss it earlier?".
- **FR-010**: System MUST execute initial new-entry posting before reminder posting within the same endpoint invocation.
- **FR-011**: System MUST prevent duplicate initial posts and duplicate reminders for the same blog entry across repeated endpoint invocations.
- **FR-012**: System MUST return a run summary indicating counts of entries evaluated, newly posted, reminders posted, skipped, and failed posting attempts.
- **FR-013**: System MUST continue processing remaining eligible entries when one entry fails to post, and include the failure in the run summary.

### Assumptions

- The endpoint is invoked manually or by an external scheduler; automatic scheduling behavior is out of scope for this feature.
- Existing social platform integrations and credentials are already available and remain unchanged by this feature.
- A stable unique identifier (such as entry GUID or canonical URL) is available from each RSS entry for deduplication.
- Configuration values are environment-managed and can be changed without code changes.

### Key Entities *(include if feature involves data)*

- **Blog Entry**: A feed item candidate for social promotion, with identifier, title, link, publish timestamp, and optional summary.
- **Post Tracking Record**: Persistent state for a blog entry that stores initial-post status/time, reminder-post status/time, and latest processing result.
- **Promotion Run Result**: Summary produced for one endpoint invocation containing totals and per-entry outcomes for initial and reminder attempts.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For a test feed containing at least 20 entries, 100% of entries within the configured day window that are not previously tracked receive exactly one initial post attempt in a single endpoint run.
- **SC-002**: Across repeated runs on unchanged feed data, duplicate initial posts and duplicate reminders occur at a rate of 0%.
- **SC-003**: When reminder posting is enabled, 100% of entries that meet reminder eligibility conditions receive one reminder with the required leader text, and ineligible entries receive none.
- **SC-004**: In runs where both new and reminder-eligible entries exist, action logs show 100% adherence to ordering: all initial posting actions complete before the first reminder action begins.
- **SC-005**: For runs that include at least one posting failure, the endpoint still processes remaining eligible entries and returns a summary that reports non-zero failure count and accurate totals.

# Feature Specification: RSS Random Post

**Feature Branch**: `002-rss-random-post`  
**Created**: 2026-03-04  
**Status**: Draft  
**Input**: User description: "I want to add a feature to look at a passed in RSS feed URL, picks a random post from the list, and posts to the social feeds. It should have optional parameters for socials to post to (default to all of them), and another parameter for a list of tags to exclude so that posts with that tag will be ignored. Another optional parameter will allow the user to filter to the last X number of days (default no time limit) so that older posts will be excluded."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Post a Random RSS Entry to All Platforms (Priority: P1)

As a content promoter, I want to provide an RSS feed URL and have the system pick one random entry from the feed and post it to all configured social platforms so that I can resurface older content with minimal effort.

**Why this priority**: This is the core value of the feature — fetching a feed, selecting a random entry, and cross-posting it. Without this, no other filtering or targeting behavior matters.

**Independent Test**: Can be fully tested by providing a valid RSS feed URL with multiple entries, invoking the endpoint with no optional parameters, and verifying that exactly one entry is randomly selected and posted to all configured platforms.

**Acceptance Scenarios**:

1. **Given** a valid RSS feed URL containing multiple entries, **When** the user invokes the endpoint with only the feed URL, **Then** the system fetches the feed, selects one entry at random, and posts it to all configured social platforms.
2. **Given** a valid RSS feed URL, **When** the post succeeds on all platforms, **Then** the response includes the selected entry title, the selected entry URL, and per-platform results indicating success.
3. **Given** a valid RSS feed URL, **When** the post succeeds on some platforms and fails on others, **Then** the response indicates partial success with per-platform details.

---

### User Story 2 - Filter by Target Platforms (Priority: P2)

As a content promoter, I want to optionally specify which social platforms to post to so that I can target only the channels that are appropriate for the content.

**Why this priority**: Platform targeting is a natural extension of the core posting behavior and limits blast radius when testing or when certain content suits only specific audiences.

**Independent Test**: Can be tested by providing a feed URL and specifying a subset of platforms (e.g., only Bluesky), invoking the endpoint, and verifying the post is published only to the specified platform(s).

**Acceptance Scenarios**:

1. **Given** a valid RSS feed URL and `platforms` set to a single platform, **When** the endpoint is invoked, **Then** the random entry is posted only to the specified platform.
2. **Given** a valid RSS feed URL and `platforms` omitted, **When** the endpoint is invoked, **Then** the random entry is posted to all configured platforms (Bluesky, Mastodon, LinkedIn).
3. **Given** an invalid platform name in the `platforms` list, **When** the endpoint is invoked, **Then** the system rejects the request with a validation error.

---

### User Story 3 - Exclude Posts by Tag (Priority: P2)

As a content promoter, I want to provide a list of tags to exclude so that entries tagged with certain topics (e.g., "personal", "draft") are not eligible for random selection.

**Why this priority**: Tag exclusion prevents inappropriate or off-topic content from being promoted and is critical for professional use, making it equal in importance to platform targeting.

**Independent Test**: Can be tested by providing a feed URL alongside an `excludeTags` list that matches tags on some feed entries, invoking the endpoint, and verifying the selected entry does not carry any of the excluded tags.

**Acceptance Scenarios**:

1. **Given** a feed with entries tagged "personal" and "tech", and `excludeTags` contains "personal", **When** the endpoint is invoked, **Then** only entries not tagged "personal" are eligible for random selection.
2. **Given** all feed entries match the excluded tags, **When** the endpoint is invoked, **Then** the system returns an informative error indicating no eligible entries remain after filtering.
3. **Given** `excludeTags` is omitted, **When** the endpoint is invoked, **Then** all feed entries are eligible regardless of their tags.
4. **Given** tag matching, **When** comparing entry tags against excluded tags, **Then** comparison is case-insensitive.

---

### User Story 4 - Filter by Recency (Priority: P3)

As a content promoter, I want to optionally limit selection to entries published within the last X days so that I only resurface relatively recent content.

**Why this priority**: Recency filtering is a convenience enhancement that narrows the selection pool; it is not required for core value delivery but improves content relevance.

**Independent Test**: Can be tested by providing a feed with entries spanning several months, setting `maxAgeDays` to 7, and verifying the selected entry was published within the last 7 days.

**Acceptance Scenarios**:

1. **Given** a feed with entries from the last 3 days and from 30 days ago, and `maxAgeDays` is 7, **When** the endpoint is invoked, **Then** only entries published within the last 7 days are eligible for random selection.
2. **Given** `maxAgeDays` is omitted, **When** the endpoint is invoked, **Then** all entries are eligible regardless of publication date.
3. **Given** `maxAgeDays` is set but no entries fall within the window, **When** the endpoint is invoked, **Then** the system returns an informative error indicating no eligible entries remain after filtering.
4. **Given** `maxAgeDays` is set to 0 or a negative number, **When** the endpoint is invoked, **Then** the system rejects the request with a validation error.

---

### Edge Cases

- What happens when the RSS feed URL is unreachable or returns invalid XML? The system returns an error indicating the feed could not be read.
- What happens when the RSS feed is valid but contains zero entries? The system returns an informative error indicating the feed is empty.
- What happens when all entries are filtered out by the combination of tag exclusion and recency? The system returns an error indicating no eligible entries remain after all filters are applied.
- What happens when an RSS entry has no tags? It is never excluded by tag filtering (only explicit tag matches cause exclusion).
- What happens when an RSS entry has no publication date? It is treated as ineligible when `maxAgeDays` is specified, but eligible when `maxAgeDays` is omitted.
- What happens when the same endpoint is called twice in quick succession? Each call independently selects a random entry; the system does not deduplicate across calls.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST accept an RSS feed URL as a required parameter in the request body.
- **FR-002**: System MUST fetch and parse the RSS feed from the provided URL.
- **FR-003**: System MUST select exactly one entry at random from the eligible entries in the feed.
- **FR-004**: System MUST post the selected entry's title, URL, and any associated hashtags to the targeted social platforms.
- **FR-005**: System MUST default to posting to all configured platforms (Bluesky, Mastodon, LinkedIn) when no `platforms` parameter is provided.
- **FR-006**: System MUST accept an optional `platforms` parameter to restrict posting to a subset of configured platforms.
- **FR-007**: System MUST validate that each specified platform is one of the supported platforms (bluesky, mastodon, linkedin).
- **FR-008**: System MUST accept an optional `excludeTags` parameter containing a list of tags.
- **FR-009**: System MUST exclude any feed entry whose tags match any value in the `excludeTags` list, using case-insensitive comparison.
- **FR-010**: System MUST accept an optional `maxAgeDays` parameter specifying the maximum age of eligible entries in days.
- **FR-011**: System MUST exclude entries whose publication date is older than `maxAgeDays` days from the current date when the parameter is provided.
- **FR-012**: System MUST treat entries with no publication date as ineligible when `maxAgeDays` is specified.
- **FR-013**: System MUST return a validation error when `maxAgeDays` is provided with a value of zero or less.
- **FR-014**: System MUST return an informative error when the feed cannot be fetched or parsed.
- **FR-015**: System MUST return an informative error when no entries remain after all filters are applied.
- **FR-016**: System MUST require authentication via the existing API key mechanism.
- **FR-017**: System MUST return per-platform results in the response, following the same success/partial-success/failure pattern used by the existing social post endpoints.
- **FR-018**: System MUST include the selected entry's title and canonical URL in the response so the caller knows which entry was posted.
- **FR-019**: System MUST use the feed entry's tags as hashtags when posting to social platforms, excluding any tags present in the `excludeTags` list.

### Key Entities

- **RSS Feed**: A remote resource identified by URL that contains a collection of syndicated entries, each with a title, link, publication date, optional summary, optional hero image, and zero or more tags/categories.
- **Eligible Entry**: A single entry from the RSS feed that passes all applied filters (tag exclusion, recency). Exactly one eligible entry is randomly selected for posting.
- **Platform Result**: The outcome of posting the selected entry to a single social platform, including success/failure status, post identifier, post URL, and any error details.

## Assumptions

- The RSS feed follows standard RSS 2.0 or Atom format, consistent with what the existing `IBlogFeedReader` implementation already parses.
- The post content is constructed from the entry's title, canonical URL, and qualifying tags — no custom message template is required.
- The feature does not track which entries have been previously posted via this endpoint. Each invocation is stateless and a previously posted entry may be randomly selected again.
- Platform character limits and text shortening apply the same rules as existing social posting behavior.
- Image support: if the feed entry has a hero image, it is included in the post (consistent with existing RSS promotion behavior).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user can submit a single request with an RSS feed URL and receive a successfully posted random entry across all platforms within 30 seconds.
- **SC-002**: When `platforms` is specified, the post is published only to the listed platforms with zero posts to unlisted platforms.
- **SC-003**: When `excludeTags` is provided, the selected entry has zero overlap between its tags and the excluded tags list.
- **SC-004**: When `maxAgeDays` is provided, the selected entry's publication date falls within the specified window 100% of the time.
- **SC-005**: When the feed is empty or all entries are filtered out, the user receives a clear, actionable error message within 10 seconds.
- **SC-006**: The feature reuses existing social posting infrastructure with no duplication of platform-specific publishing logic.

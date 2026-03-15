# Feature Specification: Standard Atom/RSS Feed Support

**Feature Branch**: `006-atom-feed-support`  
**Created**: 2026-03-14  
**Status**: Draft  
**Input**: User description: "The rss blog posting endpoint is specific to changes I made to my blog sites rss feed. I want to update the endpoint so that does the same thing, but for a standard atom 2.0 rss feed. It will need to handle either situation"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Post from a Standard Atom/RSS Feed (Priority: P1)

As a content owner, I want to provide any standard Atom or RSS feed URL and have the system read entries, select an eligible post, and publish it to social platforms — even when the feed does not contain custom blog-specific extensions — so I can promote content from any source, not only my personal blog.

**Why this priority**: This is the core ask. Without the ability to parse and post from a standard feed that lacks custom namespace elements, the feature has no new value.

**Independent Test**: Can be fully tested by pointing the endpoint at a publicly available standard Atom 2.0 or RSS 2.0 feed with no custom namespace elements, invoking the endpoint, and verifying a post is created from an eligible entry.

**Acceptance Scenarios**:

1. **Given** a standard Atom 2.0 feed URL with entries that have no custom namespace extensions, **When** the endpoint is invoked with that feed URL, **Then** the system parses entries using standard Atom fields (title, link, published date, summary) and successfully selects and posts an eligible entry.
2. **Given** a standard RSS 2.0 feed URL with entries that have no custom namespace extensions, **When** the endpoint is invoked with that feed URL, **Then** the system parses entries using standard RSS fields and successfully selects and posts an eligible entry.
3. **Given** a feed entry that has no tags or categories, **When** the system evaluates eligibility, **Then** the entry is still considered eligible (tag presence is not required for standard feeds).

---

### User Story 2 - Continue Supporting Custom Blog Feed (Priority: P2)

As the blog owner, I want the system to continue extracting custom extensions (hero image, tags) from my blog's RSS feed when those extensions are present, so existing functionality is preserved without regression.

**Why this priority**: Backward compatibility with the existing custom feed is essential to avoid breaking current workflows but is secondary to new standard-feed support.

**Independent Test**: Can be tested by invoking the endpoint with the existing custom blog RSS feed and verifying hero images and tags are still extracted and used in filtering and posting.

**Acceptance Scenarios**:

1. **Given** a feed that includes custom namespace elements (hero image and tags), **When** the system parses entries, **Then** the hero image URL and tags are extracted and populated on the entry model.
2. **Given** a feed entry that contains custom tags and the request specifies tag exclusions, **When** the system evaluates eligibility, **Then** entries matching excluded tags are filtered out as they are today.
3. **Given** a feed that previously worked with the existing endpoint, **When** the updated endpoint is invoked with the same feed URL, **Then** all behavior (entry selection, posting, response format) remains identical.

---

### User Story 3 - Use Standard Categories as Tags Fallback (Priority: P3)

As a content owner, I want the system to read standard RSS/Atom category elements as tags when custom tag extensions are absent, so tag-based filtering works for any feed.

**Why this priority**: This bridges the gap between custom and standard feeds for tag-based functionality, improving filtering quality, but is not required for basic posting.

**Independent Test**: Can be tested by providing a standard feed whose entries include `<category>` elements, invoking the endpoint with tag exclusions, and verifying category-based filtering works.

**Acceptance Scenarios**:

1. **Given** a standard feed entry with `<category>` elements but no custom tag extensions, **When** the system parses the entry, **Then** the standard categories are used as the entry's tags.
2. **Given** a feed entry that has both custom tag extensions and standard categories, **When** the system parses the entry, **Then** the custom tags take precedence.
3. **Given** a standard feed entry with categories and the request specifies tag exclusions matching one of those categories, **When** the system evaluates eligibility, **Then** that entry is excluded.

---

### Edge Cases

- Feed is an Atom 1.0 feed (not explicitly 2.0) — the system should handle Atom 1.0 as well since the underlying parser supports it.
- Feed entries lack both a publish date and a last-updated date.
- Feed entries have no link element.
- Feed contains a mix of entries with and without custom namespace extensions.
- Feed URL returns content with an unexpected content type header but valid XML.
- Feed contains entries with empty or whitespace-only titles.
- Feed entry has only a relative link rather than an absolute URL.
- Tag exclusion list is provided but the feed has no tags or categories of any kind.
- Feed entry has multiple enclosure or media elements with different image URLs (system should pick the first valid one).
- Feed entry enclosure is not an image (e.g., audio podcast enclosure) — should not be used as hero image.

## Clarifications

### Session 2026-03-14

- Q: Should tagless entries be eligible for all feed types or only standard feeds? → A: Tagless entries are eligible universally for all feeds (custom and standard alike).
- Q: When both the optional header and the reminder leader text are present, what is the ordering? → A: Reminder leader first, then header, then entry content.
- Q: Should the system validate or constrain the header length separately? → A: No separate header length validation; the header is part of the overall post body and platform character limits apply naturally.
- Q: How should HTML content in standard feed summary/content elements be handled? → A: Strip HTML tags and store plain-text summary only.
- Q: Should the system extract hero images from standard feed elements (enclosure, media) as a fallback? → A: Yes, extract image from standard enclosure or media elements as hero image fallback.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST parse and extract entries from standard Atom 1.0/2.0 feeds using standard Atom elements (title, link, published/updated, summary/content).
- **FR-002**: System MUST parse and extract entries from standard RSS 2.0 feeds using standard RSS elements (title, link, pubDate, description).
- **FR-003**: System MUST continue to extract custom namespace extensions (hero image, tags) from feeds that include the existing custom namespace, preserving all current behavior.
- **FR-004**: When a feed entry does not include custom tag extensions, the system MUST fall back to reading standard RSS `<category>` or Atom `<category>` elements as the entry's tags.
- **FR-005**: When a feed entry contains both custom tag extensions and standard categories, the system MUST use the custom tags and ignore standard categories, maintaining backward compatibility.
- **FR-006**: System MUST treat entries with no tags and no categories as eligible for posting across all feed types, including the existing custom blog feed (tag presence must not be a hard requirement).
- **FR-007**: When a feed entry lacks the custom hero image extension, the system MUST attempt to extract an image URL from standard RSS `<enclosure>` (with image MIME type), `<media:content>`, or `<media:thumbnail>` elements as a fallback. If none are found, the hero image field is populated as null without causing errors or skipping the entry.
- **FR-008**: When a feed entry contains a custom hero image extension AND standard media elements, the system MUST use the custom hero image and ignore standard media elements.
- **FR-009**: System MUST support any feed URL regardless of whether it contains custom extensions, determining available metadata dynamically per entry.
- **FR-010**: System MUST accept feeds in both Atom and RSS formats through the same endpoint and feed URL parameter without requiring the caller to specify the format.
- **FR-011**: Tag-based exclusion filtering MUST work consistently whether tags originate from custom extensions or standard category elements.
- **FR-012**: The endpoint request contract adds one optional field: a header text string. The response contract remains unchanged. No other new parameters are required for standard feed support.
- **FR-013**: System MUST handle feeds that contain a mix of entries — some with custom extensions and some without — within the same feed document.
- **FR-014**: When the optional header field is populated in the request, the system MUST prepend that text followed by a newline to the beginning of the social media post body before any entry content. When both a reminder leader and a header are applicable, the ordering MUST be: reminder leader first, then header, then entry content.
- **FR-015**: When the optional header field is absent or empty, the system MUST produce the social media post body exactly as it does today, with no extra whitespace or separator.
- **FR-016**: When a feed entry's summary or content contains HTML markup, the system MUST strip all HTML tags and store only the resulting plain text in the entry's summary field.

### Assumptions

- The caller does not need to indicate whether a feed uses custom extensions or is a standard feed; the system detects this automatically per entry.
- Standard Atom and RSS category elements are plain text labels equivalent to the custom tag values.
- Feeds conform to well-formed XML; the system is not responsible for recovering from malformed XML beyond what the parser already handles.
- Existing social platform integrations, credentials, and posting behavior remain unchanged by this feature.
- The endpoint URL and response structure remain identical. The request adds one optional header field; all other request fields are unchanged.

### Key Entities

- **Blog Feed Entry**: A feed item with identifier, title, link, publish timestamp, optional summary, optional hero image URL, and zero or more tags. Tags may originate from custom extensions or standard category elements.
- **Feed Source**: A URL pointing to an Atom or RSS feed. The system auto-detects the format and available metadata from the feed content.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Given a standard Atom 2.0 feed with at least 10 entries and no custom namespace extensions, 100% of entries with valid titles and links are parsed and available for selection.
- **SC-002**: Given the existing custom blog RSS feed, all entries produce identical parsed results (title, link, date, hero image, tags) compared to the current implementation — zero regressions.
- **SC-003**: Given a standard feed whose entries include category elements, the system applies tag-based exclusion filtering with the same accuracy as custom tag filtering (100% match rate).
- **SC-004**: Given a feed with entries that have no tags and no categories, 100% of those entries are still considered eligible for posting (not filtered out due to missing tags).
- **SC-005**: Given a feed that mixes entries with and without custom extensions, the system correctly extracts custom metadata where present and falls back to standard metadata where absent, with zero parsing errors.

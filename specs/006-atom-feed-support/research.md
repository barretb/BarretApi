# Research: Standard Atom/RSS Feed Support

**Feature**: 006-atom-feed-support  
**Date**: 2026-03-14

## Research Tasks

### 1. Standard Category Extraction via SyndicationItem.Categories

**Decision**: Use `SyndicationItem.Categories` collection as the tag fallback source.

**Rationale**: The `SyndicationFeed.Load()` parser automatically parses both RSS 2.0 `<category>` and Atom `<category>` elements into the `SyndicationItem.Categories` collection. Each `SyndicationCategory` exposes `Name` (the term), `Label` (optional display text), and `Scheme` (optional domain URI). Using `category.Name` as the tag value provides a direct mapping to the existing custom tag strings.

**Alternatives considered**:

- Manual XML parsing of category elements from `ElementExtensions` — rejected because the standard `Categories` property already handles both feed formats automatically.

### 2. Image Extraction from Standard RSS/Atom Elements

**Decision**: Use a three-tier fallback for hero image extraction:

1. Custom namespace `<hero>` element (existing, highest priority)
2. `SyndicationItem.Links` where `RelationshipType == "enclosure"` and `MediaType` starts with `image/`
3. `ElementExtensions` for `<media:thumbnail>` and `<media:content>` in namespace `http://search.yahoo.com/mrss/`

**Rationale**: `SyndicationFeed.Load()` automatically parses RSS `<enclosure>` elements into `SyndicationItem.Links` with `RelationshipType = "enclosure"`. Media RSS elements (`media:content`, `media:thumbnail`) are extension elements and must be read via `ElementExtensions.ReadElementExtensions<XElement>()`. The existing codebase already uses this pattern for custom namespace elements. Prioritising enclosure over media elements aligns with RSS 2.0 conventions (enclosure is the standard mechanism for attached media).

**Alternatives considered**:

- Ignoring media elements entirely (only enclosure) — rejected per clarification session answer requesting full fallback.
- Parsing raw XML instead of using `SyndicationLink` — rejected because the built-in parsing is sufficient for enclosures.

### 3. HTML Stripping for Feed Summary Content

**Decision**: Use AngleSharp's `IHtmlParser` to parse HTML content and extract plain text via `document.Body?.TextContent`.

**Rationale**: AngleSharp 1.4.0 is already a project dependency, used in `AngleSharpHtmlTextExtractor`. The pattern is proven in the codebase: create `BrowsingContext`, parse HTML with `IHtmlParser.ParseDocument()`, remove `script`/`style`/`noscript`/`svg`/`head` elements, then read `TextContent`. This safely handles malformed HTML, entity decoding, and nested elements. The stripping logic will be a small private method on `RssBlogFeedReader` (not a separate service, since it's a simple one-use transform).

**Alternatives considered**:

- Regex-based tag stripping — rejected because regex cannot reliably handle nested/malformed HTML, CDATA sections, or entity decoding.
- `HttpUtility.HtmlDecode` + string replacement — rejected because it doesn't handle element removal or structural HTML.
- Creating a new shared service — rejected per YAGNI; the only consumer is the feed reader's summary field.

### 4. SyndicationItem Summary vs Content Mapping

**Decision**: For summary extraction, prefer `SyndicationItem.Summary.Text` then fall back to `SyndicationItem.Content` (cast to `TextSyndicationContent`).

**Rationale**: Standard mapping is:

| Format | Element | Property |
|--------|---------|----------|
| Atom | `<summary>` | `SyndicationItem.Summary` |
| Atom | `<content>` | `SyndicationItem.Content` |
| RSS 2.0 | `<description>` | `SyndicationItem.Summary` |

The current code already reads `item.Summary?.Text`. For Atom feeds that have `<content>` but no `<summary>`, the fallback to `Content` provides a reasonable summary source after HTML stripping.

**Alternatives considered**:

- Only reading `Summary` and ignoring `Content` — rejected because many Atom feeds omit `<summary>` and put all content in `<content>`.

### 5. Tagless Entry Eligibility — Service Layer Change

**Decision**: Remove the `Tags.Count == 0` filter from `RssRandomPostService` and `BlogPromotionOrchestrator`.

**Rationale**: Per clarification, tagless entries are eligible universally. The existing filter in `RssRandomPostService` (`entries.Where(e => e.Tags.Count > 0)`) and in `BlogPromotionOrchestrator` must be removed. Tag exclusion filtering still applies when tags are present, but the absence of tags no longer disqualifies an entry.

**Alternatives considered**:

- Making the tag-required filter configurable per request — rejected because the clarification explicitly states universal eligibility.

### 6. Header Text Prepend — Post Body Composition

**Decision**: Add optional `Header` property to `RssRandomPostRequest`. In `RssRandomPostService`, prepend the header text with a trailing newline before the entry title/URL when the header is non-empty. In `BlogPromotionOrchestrator`, when both reminder leader and header are present, compose as: reminder leader → header → entry content.

**Rationale**: Per clarification, the ordering when both are present is: reminder leader first, then header, then entry content. No separate length validation — platform character limits apply naturally via the existing `TextShorteningService`.

**Alternatives considered**:

- Adding header as a configuration parameter instead of a per-request field — rejected because multiple callers may want different headers for different feeds.
- Validating header length separately — rejected per clarification.

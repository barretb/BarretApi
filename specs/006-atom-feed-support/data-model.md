# Data Model: Standard Atom/RSS Feed Support

**Feature**: 006-atom-feed-support  
**Date**: 2026-03-14

## Entities

### BlogFeedEntry (existing — NO changes)

The existing `BlogFeedEntry` model already supports all scenarios. Tags are an optional list (`IReadOnlyList<string>` defaulting to `[]`), HeroImageUrl is nullable, and Summary is nullable. No schema changes are needed — the changes are in how the feed reader populates these fields.

| Field | Type | Required | Source (current) | Source (after change) |
|-------|------|----------|------------------|-----------------------|
| EntryIdentity | string | Yes | GUID or canonical URL | No change |
| Guid | string? | No | RSS `<guid>` / Atom `<id>` | No change |
| CanonicalUrl | string | Yes | First `<link>` | No change |
| Title | string | Yes | `<title>` | No change |
| PublishedAtUtc | DateTimeOffset | Yes | `<pubDate>` / `<published>` with fallbacks | No change |
| Summary | string? | No | `<description>` / `<summary>` (raw text) | `<description>` / `<summary>` / `<content>` → **HTML-stripped to plain text** |
| HeroImageUrl | string? | No | Custom `<hero>` extension only | Custom `<hero>` → enclosure (image) → `<media:thumbnail>` → `<media:content>` (image) |
| Tags | IReadOnlyList\<string\> | No (defaults to []) | Custom `<tags><tag>` extension only | Custom `<tags>` → SyndicationItem.Categories (standard) |

### RssRandomPostQuery (existing — ADD Header field)

| Field | Type | Required | Change |
|-------|------|----------|--------|
| FeedUrl | string | Yes | No change |
| Platforms | IReadOnlyList\<string\> | No | No change |
| ExcludeTags | IReadOnlyList\<string\> | No | No change |
| MaxAgeDays | int? | No | No change |
| Header | string? | No | **NEW** — optional text to prepend to social post body |

### BlogPostPromotionRecord (existing — NO changes)

No schema changes. The Azure Table entity is keyed by `EntryIdentity` (SHA256 hash), which remains the same regardless of feed format. Standard feed entries produce identity values from the same sources (GUID or canonical URL).

## Relationships

```text
Feed URL (Atom or RSS)
  │
  ├─ parsed by ─→ RssBlogFeedReader
  │                  │
  │                  └─→ BlogFeedEntry[]
  │                        ├─ Tags: custom extensions → standard categories (fallback)
  │                        ├─ HeroImageUrl: custom hero → enclosure → media (fallback)
  │                        └─ Summary: html-stripped text
  │
  ├─ RssRandomPostService (entry selection + filtering)
  │    ├─ Tagless entries now eligible (filter removed)
  │    └─ Header prepended to post text when present
  │
  └─ BlogPromotionOrchestrator (scheduled promotion)
       └─ Tagless entries now eligible (filter removed)
```

## Validation Rules

- **Tags**: Empty list is valid and no longer causes entry exclusion.
- **HeroImageUrl**: Null is valid. When populated from enclosure/media, must be an absolute HTTP/HTTPS URL with image MIME type.
- **Summary**: After HTML stripping, null or empty is valid. Whitespace-only results are treated as null.
- **Header** (request): Optional. No length constraint. Empty string treated same as absent.

## State Transitions

No state machines involved. This feature is a stateless parsing enhancement + a minor request field addition. The blog promotion tracking state machine (initial post → reminder) is unchanged.

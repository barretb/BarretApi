# Data Model: RSS Random Post

**Feature**: 002-rss-random-post
**Date**: 2026-03-04

## Entities

### BlogFeedEntry (existing — no changes)

Represents a single item from an RSS/Atom feed.

| Field | Type | Required | Description |
|---|---|---|---|
| EntryIdentity | string | Yes | Unique identifier (GUID or canonical URL). |
| Guid | string | No | RSS `<guid>` element value. |
| CanonicalUrl | string | Yes | Primary link to the content. |
| Title | string | Yes | Entry title. |
| PublishedAtUtc | DateTimeOffset | Yes | Publication date in UTC. |
| Summary | string | No | Entry summary/description text. |
| HeroImageUrl | string | No | URL of the hero/featured image. |
| Tags | string[] | No | Categories/tags associated with the entry. |

### RssRandomPostRequest (new)

The API request body for the RSS random post endpoint.

| Field | Type | Required | Default | Validation |
|---|---|---|---|---|
| feedUrl | string | Yes | — | Must be a valid absolute URL with http/https scheme. |
| platforms | string[] | No | All configured | Each value must be `bluesky`, `mastodon`, or `linkedin` (case-insensitive). |
| excludeTags | string[] | No | Empty | No constraints on individual values; used for case-insensitive matching. |
| maxAgeDays | int | No | null (no limit) | Must be > 0 when provided. |

### RssRandomPostResponse (new)

The API response body for the RSS random post endpoint.

| Field | Type | Always Present | Description |
|---|---|---|---|
| selectedTitle | string | Yes | Title of the randomly selected feed entry. |
| selectedUrl | string | Yes | Canonical URL of the randomly selected feed entry. |
| results | PlatformResult[] | Yes | Per-platform posting outcomes. |
| postedAt | DateTimeOffset | Yes | UTC timestamp of when the posting was completed. |

### PlatformResult (existing — reused from CreateSocialPostResponse)

Per-platform posting outcome.

| Field | Type | Always Present | Description |
|---|---|---|---|
| platform | string | Yes | Platform name (bluesky, mastodon, linkedin). |
| success | bool | Yes | Whether the post succeeded. |
| postId | string | On success | Platform-specific post identifier. |
| postUrl | string | On success | URL to the published post. |
| shortenedText | string | On success | The final text after hashtag processing and shortening. |
| error | string | On failure | Human-readable error message. |
| errorCode | string | On failure | Machine-readable error code. |

### RssRandomPostResult (new — internal)

Internal result returned by `RssRandomPostService` to the endpoint.

| Field | Type | Description |
|---|---|---|
| SelectedEntry | BlogFeedEntry | The randomly selected feed entry. |
| PlatformResults | PlatformPostResult[] | Per-platform results from SocialPostService. |

## Relationships

```
RssRandomPostRequest
  └─→ IBlogFeedReader.ReadEntriesAsync(feedUrl)
        └─→ List<BlogFeedEntry>
              │  (filter by excludeTags, maxAgeDays)
              └─→ Single BlogFeedEntry (random)
                    └─→ SocialPost (text = Title + URL, hashtags = Tags - excludeTags, images = HeroImage)
                          └─→ SocialPostService.PostAsync()
                                └─→ List<PlatformPostResult>
                                      └─→ RssRandomPostResponse
```

## State Transitions

This feature is stateless. There are no state transitions or persistence operations. Each request independently:

1. Fetches the RSS feed
2. Filters entries
3. Selects one randomly
4. Posts to platforms
5. Returns the result

No record of which entries have been previously selected is maintained.

## Validation Rules

| Rule | Source | Error Behavior |
|---|---|---|
| `feedUrl` is required and must be a valid absolute http/https URL | FR-001, FR-002, edge case (SSRF) | 400 — validation error |
| `platforms` values must be supported platform names | FR-007 | 400 — validation error |
| `maxAgeDays` must be > 0 when provided | FR-013 | 400 — validation error |
| Feed must contain at least one entry after parsing | Edge case (empty feed) | 422 — "Feed contains no entries" |
| At least one entry must remain after all filters | FR-015, edge case (all filtered) | 422 — "No eligible entries remain after filtering" |
| Feed URL must be reachable and parseable | FR-014 | 502 — "Feed could not be read" |

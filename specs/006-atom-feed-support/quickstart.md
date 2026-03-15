# Quickstart: Standard Atom/RSS Feed Support

**Feature**: 006-atom-feed-support  
**Date**: 2026-03-14

## What Changed

This feature enhances the existing RSS feed reader and random post endpoint to work with **any** standard Atom or RSS feed — not just the custom blog feed. Key changes:

1. **Standard feed support**: Feeds without custom namespace extensions are now fully supported
2. **Category fallback**: Standard `<category>` elements are read as tags when custom tags are absent
3. **Image fallback**: `<enclosure>`, `<media:content>`, and `<media:thumbnail>` are used as hero image sources when the custom `<hero>` element is absent
4. **HTML stripping**: Feed summaries containing HTML are stripped to plain text
5. **Tagless eligibility**: Entries without any tags/categories are now eligible for posting
6. **Optional header**: A new `header` request field prepends caller-supplied text to the social post

## Usage

### Post from a Standard Atom Feed

```bash
curl -X POST https://localhost:5001/api/social-posts/rss-random \
  -H "X-Api-Key: YOUR_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "feedUrl": "https://example.com/atom.xml",
    "platforms": ["bluesky", "mastodon"]
  }'
```

### Post with a Custom Header

```bash
curl -X POST https://localhost:5001/api/social-posts/rss-random \
  -H "X-Api-Key: YOUR_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "feedUrl": "https://example.com/feed.xml",
    "platforms": ["bluesky"],
    "header": "From the archives..."
  }'
```

The resulting social post text will be:

```text
From the archives...
Example Post Title
https://example.com/post
```

### Existing Custom Blog Feed (Unchanged)

The existing custom blog RSS feed continues to work identically:

```bash
curl -X POST https://localhost:5001/api/social-posts/rss-random \
  -H "X-Api-Key: YOUR_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "feedUrl": "https://barretblake.dev/feed.xml",
    "platforms": ["bluesky", "mastodon", "linkedin"],
    "excludeTags": ["personal"],
    "maxAgeDays": 365
  }'
```

## Files Modified

| File | Change |
|------|--------|
| `src/BarretApi.Infrastructure/Services/RssBlogFeedReader.cs` | Add category fallback, image fallback, HTML stripping, Atom content fallback |
| `src/BarretApi.Core/Services/RssRandomPostService.cs` | Remove tag-required filter; prepend header to post text |
| `src/BarretApi.Api/Features/SocialPost/RssRandomPostRequest.cs` | Add optional `Header` property |
| `src/BarretApi.Core/Models/RssRandomPostQuery.cs` | Add optional `Header` property |

## Testing

Run all affected tests:

```bash
dotnet test tests/BarretApi.Infrastructure.UnitTests
dotnet test tests/BarretApi.Core.UnitTests
dotnet test tests/BarretApi.Api.UnitTests
```

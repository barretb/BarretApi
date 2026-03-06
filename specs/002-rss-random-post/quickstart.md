# Quickstart — 002 RSS Random Post

## Prerequisites

- .NET 10.0 SDK
- Docker Desktop (for Aspire / Azurite)
- A valid API key configured in AppHost user secrets

## Start the API

```bash
cd src/BarretApi.AppHost
dotnet run
```

The Aspire dashboard opens automatically. The API listens on `https://localhost:7042` by default (see `launchSettings.json`).

## Smoke Test — Post a random entry from an RSS feed

### Minimal request (all platforms, no filters)

```bash
curl -s -X POST https://localhost:7042/api/social-posts/rss-random \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: YOUR_API_KEY" \
  -d '{
    "feedUrl": "https://example.com/blog/feed.xml"
  }' | jq .
```

### With all optional parameters

```bash
curl -s -X POST https://localhost:7042/api/social-posts/rss-random \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: YOUR_API_KEY" \
  -d '{
    "feedUrl": "https://example.com/blog/feed.xml",
    "platforms": ["bluesky", "mastodon"],
    "excludeTags": ["personal", "draft"],
    "maxAgeDays": 30
  }' | jq .
```

### Expected 200 response

```json
{
  "selectedTitle": "Building APIs with .NET Aspire",
  "selectedUrl": "https://example.com/blog/aspire-apis",
  "results": [
    {
      "platform": "bluesky",
      "success": true,
      "postId": "at://did:plc:abc123/app.bsky.feed.post/xyz789",
      "postUrl": "https://bsky.app/profile/handle.bsky.social/post/xyz789",
      "shortenedText": "Building APIs with .NET Aspire\nhttps://example.com/blog/aspire-apis #dotnet #aspire"
    },
    {
      "platform": "mastodon",
      "success": true,
      "postId": "109876543210",
      "postUrl": "https://mastodon.social/@you/109876543210",
      "shortenedText": "Building APIs with .NET Aspire\nhttps://example.com/blog/aspire-apis #dotnet #aspire"
    }
  ],
  "postedAt": "2026-03-04T12:00:00+00:00"
}
```

## Error Scenarios

### Missing feed URL → 400

```bash
curl -s -X POST https://localhost:7042/api/social-posts/rss-random \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: YOUR_API_KEY" \
  -d '{}' | jq .
```

### No entries after filtering → 422

```bash
curl -s -X POST https://localhost:7042/api/social-posts/rss-random \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: YOUR_API_KEY" \
  -d '{
    "feedUrl": "https://example.com/blog/feed.xml",
    "maxAgeDays": 1
  }' | jq .
```

### Invalid URL scheme → 400

```bash
curl -s -X POST https://localhost:7042/api/social-posts/rss-random \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: YOUR_API_KEY" \
  -d '{
    "feedUrl": "ftp://evil.example.com/feed.xml"
  }' | jq .
```

## Running Tests

```bash
# All tests
dotnet test

# Only unit tests for the API project
dotnet test tests/BarretApi.Api.UnitTests

# Only core unit tests
dotnet test tests/BarretApi.Core.UnitTests
```

# Quickstart: Social Media Post API

**Date**: 2026-02-28 | **Spec**: [spec.md](spec.md)

---

## Prerequisites

- [.NET 10.0 SDK](https://dot.net) installed
- [.NET Aspire workload](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/setup-tooling) installed (`dotnet workload install aspire`)
- A **Bluesky** account with an [App Password](https://bsky.app/settings/app-passwords) generated
- A **Mastodon** account with an access token generated via **Preferences > Development > New Application** (scopes: `write:statuses`, `write:media`)
- A self-chosen API key string for endpoint authentication

---

## 1. Clone & Build

```bash
git checkout 001-social-post-api
dotnet build
```

---

## 2. Configure Secrets (Aspire AppHost)

All configuration lives in the AppHost project. Initialize User Secrets and add the required values:

```bash
cd src/BarretApi.AppHost
dotnet user-secrets init
dotnet user-secrets set "Parameters:bluesky-handle" "your-handle.bsky.social"
dotnet user-secrets set "Parameters:bluesky-app-password" "your-app-password"
dotnet user-secrets set "Parameters:mastodon-instance-url" "https://mastodon.social"
dotnet user-secrets set "Parameters:mastodon-access-token" "your-mastodon-access-token"
dotnet user-secrets set "Parameters:auth-api-key" "your-chosen-api-key"
```

> **Note**: Replace the placeholder values with your actual credentials. Never commit secrets to source control.

---

## 3. Run with Aspire

```bash
cd src/BarretApi.AppHost
dotnet run
```

The Aspire dashboard URL is shown in the terminal output. Open it to find the API project URL listed under the **api** resource. Ports are dynamically assigned by Aspire.

---

## 4. Make Your First Post

### JSON endpoint (URL-referenced images)

```bash
# Replace <API_URL> with the URL shown in the Aspire dashboard for the "api" resource
curl -X POST <API_URL>/api/social-posts \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: your-chosen-api-key" \
  -d '{
    "text": "Hello from BarretApi! #dotnet #aspire",
    "hashtags": ["webapi"],
    "images": [
      {
        "url": "https://example.com/photo.jpg",
        "altText": "A descriptive alt text for the image"
      }
    ]
  }'
```

### Multipart endpoint (file uploads)

```bash
curl -X POST <API_URL>/api/social-posts/upload \
  -H "X-Api-Key: your-chosen-api-key" \
  -F "text=Hello from BarretApi! #dotnet #aspire" \
  -F "hashtags=webapi" \
  -F "images=@photo.jpg" \
  -F "altTexts=A descriptive alt text for the image"
```

### Expected response (HTTP 200)

```json
{
  "results": [
    {
      "platform": "bluesky",
      "success": true,
      "postId": "at://did:plc:abc123/app.bsky.feed.post/xyz789",
      "postUrl": "https://bsky.app/profile/your-handle.bsky.social/post/xyz789",
      "shortenedText": "Hello from BarretApi! #dotnet #aspire #webapi"
    },
    {
      "platform": "mastodon",
      "success": true,
      "postId": "109876543210",
      "postUrl": "https://mastodon.social/@you/109876543210",
      "shortenedText": "Hello from BarretApi! #dotnet #aspire #webapi"
    }
  ],
  "postedAt": "2026-02-28T14:30:00.000Z"
}
```

---

## 5. Run Tests

```bash
# From repository root
dotnet test
```

---

## 6. Project Layout

```text
src/
├── BarretApi.AppHost/           # Aspire orchestrator — all config & secrets
├── BarretApi.ServiceDefaults/   # Shared resilience, logging, health checks
├── BarretApi.Api/               # FastEndpoints, validation, auth handler
├── BarretApi.Core/              # Interfaces, domain models, text shortening
└── BarretApi.Infrastructure/    # Bluesky & Mastodon client implementations

tests/
├── BarretApi.Core.UnitTests/
├── BarretApi.Api.UnitTests/
└── BarretApi.Integration.Tests/
```

---

## 7. Key Configuration Reference

| Setting | Location | Description |
|---------|----------|-------------|
| `bluesky-handle` | AppHost secrets | Bluesky account handle or DID |
| `bluesky-app-password` | AppHost secrets | Bluesky app password |
| `mastodon-instance-url` | AppHost secrets | Mastodon instance base URL |
| `mastodon-access-token` | AppHost secrets | Mastodon bearer token |
| `auth-api-key` | AppHost secrets | API key for `X-Api-Key` header |

Retry settings are controlled by Aspire ServiceDefaults' standard resilience handler (3 retries, exponential backoff). Override via Aspire configuration if needed.

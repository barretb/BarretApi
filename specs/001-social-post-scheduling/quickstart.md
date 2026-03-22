# Quickstart: Scheduled Social Post Publishing

**Feature**: 001-social-post-scheduling  
**Date**: 2026-03-22

## Overview

This feature adds optional scheduled publishing to existing social post APIs and introduces a dedicated endpoint to process due scheduled posts.

## Prerequisites

- BarretApi solution builds and runs.
- API key authentication is configured.
- Storage backing for scheduled-post records is configured through AppHost settings.

## 1. Create an immediate post (existing behavior)

```bash
curl -X POST http://localhost:5000/api/social-posts \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: YOUR_KEY" \
  -d '{
    "text": "Posting immediately from BarretApi",
    "platforms": ["bluesky", "mastodon"]
  }'
```

Expected: the post is published immediately and response includes per-platform results.

## 2. Create a scheduled post (JSON)

```bash
curl -X POST http://localhost:5000/api/social-posts \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: YOUR_KEY" \
  -d '{
    "text": "Scheduled post from BarretApi",
    "hashtags": ["dotnet", "automation"],
    "platforms": ["linkedin", "bluesky"],
    "scheduledFor": "2026-03-23T14:30:00Z"
  }'
```

Expected: request is accepted as scheduled and not published during this call.

## 3. Create a scheduled post (multipart upload)

```bash
curl -X POST http://localhost:5000/api/social-posts/upload \
  -H "X-Api-Key: YOUR_KEY" \
  -F "text=Scheduled upload post" \
  -F "platforms=bluesky" \
  -F "scheduledFor=2026-03-23T15:00:00Z" \
  -F "images=@./photo.jpg" \
  -F "altTexts=A descriptive alt text"
```

Expected: request is accepted as scheduled; uploaded content is persisted for deferred posting.

## 4. Process due scheduled posts

```bash
curl -X POST http://localhost:5000/api/social-posts/scheduled/process \
  -H "X-Api-Key: YOUR_KEY" \
  -H "Content-Type: application/json" \
  -d '{ "maxCount": 100 }'
```

Expected: endpoint publishes due items where scheduledFor <= now and returns a run summary with due, attempted, succeeded, failed, and skipped counts plus failure details.

## 5. Validate idempotency

Run the processing endpoint a second time immediately.

Expected: previously published items are not reposted; summary indicates zero reprocessed published records.

## Expected Results Matrix

| Scenario | Expected Result |
|----------|-----------------|
| scheduledFor omitted | Immediate publish path |
| scheduledFor in future | Persisted as scheduled pending |
| scheduledFor <= now at create time | Validation error |
| process run with due items | Due items attempted and summary returned |
| process run with no due items | Success response with zero counts |
| process run where all attempts fail | 502 with run summary and failure details |
| process rerun after success | No duplicate publish |

## Testing Focus

- Validator tests for scheduledFor rules (future required when present).
- Endpoint tests for immediate vs scheduled branching.
- Processor tests for due filtering, status transitions, and idempotency.
- Repository tests for storage mapping and state persistence.

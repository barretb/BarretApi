# Data Model: Scheduled Social Post Publishing

**Feature**: 001-social-post-scheduling  
**Date**: 2026-03-22

## Entities

### ScheduledSocialPostRecord

Represents a social post that should be published at a future timestamp.

| Field | Type | Description |
|-------|------|-------------|
| ScheduledPostId | string | Unique identifier for the scheduled post |
| ScheduledForUtc | DateTimeOffset | UTC timestamp when post becomes eligible for publishing |
| Status | ScheduledPostStatus | Current lifecycle status |
| Text | string | Post body text |
| Hashtags | list<string> | Hashtags supplied in request |
| TargetPlatforms | list<string> | Requested target platforms; empty means all configured |
| ImageUrls | list<ImageUrlDetails> | URL-based image attachments |
| UploadedImages | list<UploadedImageDetails> | File-upload image attachments persisted for deferred publishing |
| CreatedAtUtc | DateTimeOffset | Record creation timestamp |
| LastAttemptedAtUtc | DateTimeOffset? | Most recent publish attempt time |
| PublishedAtUtc | DateTimeOffset? | Time successful publish completed |
| LastErrorCode | string? | Last failure code |
| LastErrorMessage | string? | Last failure message |
| AttemptCount | int | Number of processing attempts |

**Validation Rules**:

- ScheduledForUtc must be greater than create-time nowUtc.
- At least one of Text or image content must be present.
- Image metadata must maintain required alt text.
- Target platform names must remain within supported allowlist.

### ScheduledPostStatus

Represents processing state for a scheduled post.

| Value | Meaning |
|-------|---------|
| Pending | Awaiting due time |
| Processing | Claimed by a processing run |
| Published | Successfully published and no longer eligible |
| Failed | Attempted and failed, remains retryable |

### ScheduledPostProcessingSummary

Represents aggregate results of one due-post processing endpoint invocation.

| Field | Type | Description |
|-------|------|-------------|
| RunId | string | Unique identifier for processing run |
| StartedAtUtc | DateTimeOffset | Run start timestamp |
| CompletedAtUtc | DateTimeOffset | Run completion timestamp |
| DueCount | int | Number of posts considered due at run start |
| AttemptedCount | int | Number of due posts attempted |
| SucceededCount | int | Number of posts successfully published |
| FailedCount | int | Number of posts that failed publish |
| SkippedCount | int | Number of due posts skipped (already claimed/published/etc.) |
| Failures | list<ScheduledPostFailureDetails> | Per-post failure diagnostics |

### ScheduledPostFailureDetails

Represents a single failed scheduled-post attempt in a processing run.

| Field | Type | Description |
|-------|------|-------------|
| ScheduledPostId | string | Failed post identifier |
| ScheduledForUtc | DateTimeOffset | Original due time |
| Platforms | list<string> | Target platforms for failed attempt |
| ErrorCode | string | Normalized failure code |
| ErrorMessage | string | Human-readable failure reason |
| AttemptedAtUtc | DateTimeOffset | Attempt time |

### ImageUrlDetails

Represents deferred URL image metadata needed for future publishing.

| Field | Type | Description |
|-------|------|-------------|
| Url | string | Absolute image URL |
| AltText | string | Required accessibility text |

### UploadedImageDetails

Represents deferred uploaded image content for scheduled publish.

| Field | Type | Description |
|-------|------|-------------|
| FileName | string | Original uploaded filename |
| ContentType | string | MIME type |
| ContentBase64 | string | Serialized image bytes for persistence |
| AltText | string | Required accessibility text |

## Relationships

- A ScheduledSocialPostRecord can produce zero or more ScheduledPostFailureDetails across attempts.
- A ScheduledPostProcessingSummary aggregates many ScheduledSocialPostRecord processing outcomes.
- A ScheduledSocialPostRecord contains zero or more image metadata objects of type ImageUrlDetails and UploadedImageDetails.

## State Transitions

```text
Pending -> Processing -> Published
   |           |
   |           -> Failed
   |                |
   +----------------+
```

Transition notes:

- New scheduled posts start as Pending.
- Due-post processing claims eligible Pending or Failed records into Processing.
- Successful publish transitions Processing to Published.
- Failed publish transitions Processing to Failed and increments AttemptCount.
- Failed records remain eligible for future processing retries.

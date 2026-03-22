# Research: Scheduled Social Post Publishing

**Feature**: 001-social-post-scheduling  
**Date**: 2026-03-22

## R1: Scheduled Post Persistence Strategy

**Decision**: Persist scheduled social posts using a dedicated repository abstraction in Core with an Azure Table Storage implementation in Infrastructure.

**Rationale**: The project already uses Azure Table Storage patterns for RSS promotion records, which provides a proven durable store for future-dated work without introducing a new database technology.

**Alternatives considered**:

- In-memory scheduling queue: rejected because due posts would be lost on restart and would not support reliable processing.
- File-based persistence: rejected because it is brittle in containerized deployments and does not align with existing persistence conventions.
- SQL database introduction: rejected for this increment because it adds unnecessary infrastructure and migration complexity.

## R2: API Surface for Scheduling Input

**Decision**: Add an optional `scheduledFor` field to both social post create API variants: JSON (`POST /api/social-posts`) and multipart upload (`POST /api/social-posts/upload`).

**Rationale**: Both endpoints are first-class social post APIs today; users should be able to schedule regardless of whether images are referenced by URL or uploaded as files.

**Alternatives considered**:

- JSON endpoint only: rejected because it creates inconsistent behavior across equivalent posting paths.
- New dedicated schedule-create endpoint: rejected because it duplicates existing create semantics and increases integration overhead.

## R3: Due Processing Trigger Model

**Decision**: Introduce an explicit processing endpoint (`POST /api/social-posts/scheduled/process`) that finds and posts due scheduled items.

**Rationale**: The codebase already uses trigger-style endpoints for orchestrated operations (for example RSS promotion), and the feature request explicitly asks for an endpoint that checks and posts due items.

**Alternatives considered**:

- Background hosted service timer: rejected for this feature because explicit endpoint trigger is requested and avoids extra runtime scheduling complexity.
- External queue worker only: rejected because it would require additional infrastructure and diverges from current API-driven orchestration.

## R4: Time Semantics and Validation

**Decision**: Validate `scheduledFor` as a future timestamp at request time and evaluate due status using UTC comparison where a post is due if `scheduledFor <= nowUtc`.

**Rationale**: UTC-based comparisons prevent timezone drift and ambiguity; due threshold at equality handles exact-time triggers predictably.

**Alternatives considered**:

- Local server timezone comparisons: rejected due to DST and deployment variability.
- Strictly less-than due check: rejected because it can delay exact-time schedules unexpectedly.

## R5: Idempotency and Concurrency Safety

**Decision**: Scheduled posts transition through explicit statuses and processing updates must enforce single-publish behavior so repeated endpoint calls do not repost completed items.

**Rationale**: The feature requires idempotent processing and duplicate-post prevention across overlapping processing calls.

**Alternatives considered**:

- Best-effort status updates after posting only: rejected because race conditions can produce duplicate publishes.
- Global process lock only: rejected because lock contention can reduce throughput and still leaves recovery gaps.

## R6: Failure Handling and Retryability

**Decision**: On publish failure, keep scheduled posts in a retryable state with attempt metadata (attempted at, error code, error message), and include failure summaries in endpoint output.

**Rationale**: Failed due posts must remain visible and retryable by subsequent processing runs, while operators need actionable diagnostics.

**Alternatives considered**:

- Mark all failures as terminal: rejected because transient social platform failures are common and should be retried.
- Silent failure logging only: rejected because API callers need per-run failure visibility.

## R7: Response Contract for Processing Runs

**Decision**: Return a run summary payload with counts for due, attempted, succeeded, failed, and skipped, plus per-post failure details.

**Rationale**: This directly satisfies the feature requirement for operational visibility and mirrors existing summary-oriented endpoint patterns in the repository.

**Alternatives considered**:

- Return only HTTP status code: rejected because it does not provide enough operational detail.
- Return full posted payload echoes for all items: rejected because it increases response size without improving core observability goals.

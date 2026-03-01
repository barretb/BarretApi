# Phase 0 Research: RSS Blog Post Promotion

## Decision 1: Use Azure Table Storage as the source of truth for deduplication

- Decision: Track each blog entry’s posting lifecycle in Azure Table Storage using a stable feed entry identity key, initial-post metadata, and reminder-post metadata.
- Rationale: Table Storage is cost-effective, durable, and sufficient for key-based lookup/update patterns needed for idempotent posting.
- Alternatives considered:
  - In-memory cache: rejected because state is lost across restarts/deployments.
  - Local file persistence: rejected due to reliability and concurrency limitations in cloud hosting.
  - Azure SQL/Cosmos DB: rejected for this scope due to higher operational complexity for simple key-value style records.

## Decision 2: Authenticate Azure data access with Managed Identity in hosted environments

- Decision: Use Managed Identity for App Service access to Table Storage; use local developer credentials for local runs.
- Rationale: Aligns with Azure security best practices and avoids secret/key storage in source control.
- Alternatives considered:
  - Connection string with account key: rejected due to key-management/security risk.
  - SAS token hardcoding: rejected due to rotation burden and security exposure.

## Decision 3: Process promotion in a strict two-pass order

- Decision: Execute initial-post pass first for all eligible new entries, then execute reminder-post pass for already-posted entries.
- Rationale: Matches functional requirement ordering and ensures reminders never preempt new-post promotion.
- Alternatives considered:
  - Interleaved per-entry processing: rejected because it can violate ordering semantics.
  - Reminder-first processing: rejected because it conflicts with user requirement.

## Decision 4: Define reminder eligibility based on successful initial post timestamp

- Decision: Reminder eligibility requires `InitialPostSucceededAtUtc` to exist, reminder toggle enabled, no prior reminder success, and elapsed time >= configured reminder hours.
- Rationale: Prevents false reminders and guarantees reminders are tied to confirmed initial posts.
- Alternatives considered:
  - Use feed publish timestamp for reminder timing: rejected because it can trigger reminders even if initial posting failed.
  - Allow multiple reminders: rejected; out of current feature scope.

## Decision 5: Continue-on-error with per-entry outcomes in run summary

- Decision: If one entry/platform fails, continue processing remaining eligible entries and return aggregate + per-entry failures in summary.
- Rationale: Maximizes useful work in each invocation and aligns with existing social posting behavior patterns.
- Alternatives considered:
  - Fail-fast behavior: rejected because one transient failure would block all remaining entries.

## Decision 6: Use feed GUID/canonical link as deduplication identity with fallback normalization

- Decision: Prefer RSS GUID when stable; otherwise use canonical link; normalize identity before storage key mapping.
- Rationale: Supports common RSS feed variations while preserving durable deduplication.
- Alternatives considered:
  - Title-based identity: rejected due to edits and collisions.
  - Publish date + title composite: rejected due to mutation risk and instability.

## Decision 7: Add strongly-typed options for feed and promotion timing

- Decision: Add options for `FeedUrl`, `RecentDaysWindow`, `EnableReminderPosts`, and `ReminderDelayHours` validated at startup.
- Rationale: Keeps behavior configurable per environment and aligns with project configuration conventions.
- Alternatives considered:
  - Hardcoded constants: rejected due to operational inflexibility.
  - Endpoint query parameters only: rejected because stable operational defaults are required.

## Decision 8: Treat endpoint as trigger-only orchestration, not scheduler

- Decision: Keep scheduling out of scope; endpoint performs one deterministic run when invoked.
- Rationale: Matches current feature scope and allows external scheduler choice later without redesigning core logic.
- Alternatives considered:
  - Built-in background scheduler in API process: rejected as scope expansion and operational coupling.

## Troubleshooting Notes

- If endpoint responses include `RSS_FEED_READ_FAILED`, validate `BlogPromotion:FeedUrl`, outbound network access, TLS trust chain, and RSS XML validity.
- If Azure Table operations fail, validate `BlogPromotion:TableStorage:AccountEndpoint`, table name, and managed identity role assignments.
- For local development, ensure developer credentials can access the storage account (for example via Azure CLI login).
- If reminders are not posting, verify `EnableReminderPosts=true`, `ReminderDelayHours` value, and that initial posts succeeded before the current run.

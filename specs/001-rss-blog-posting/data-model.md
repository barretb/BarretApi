# Data Model: RSS Blog Post Promotion

## 1) BlogFeedEntry (transient input model)

Represents one item read from the RSS feed during a run.

### BlogFeedEntry Fields

- `EntryIdentity` (string, required): Stable deduplication identity derived from feed GUID or canonical link.
- `Guid` (string, optional): Raw feed GUID when present.
- `CanonicalUrl` (string, required): Public URL for the blog post.
- `Title` (string, required): Entry title text.
- `PublishedAtUtc` (datetime, required): Publish timestamp normalized to UTC.
- `Summary` (string, optional): Feed summary/description text.

### BlogFeedEntry Validation Rules

- `EntryIdentity` must be non-empty and deterministic for the same blog entry across runs.
- `CanonicalUrl` must be absolute HTTP/HTTPS URL.
- `PublishedAtUtc` must be parseable and not default/empty.

## 2) BlogPostPromotionRecord (persistent tracking entity)

Stores promotion state for one blog entry in Azure Table Storage.

### BlogPostPromotionRecord Fields

- `PartitionKey` (string, required): Logical grouping key (e.g., feed host or configured feed key).
- `RowKey` (string, required): Deterministic key derived from `EntryIdentity`.
- `EntryIdentity` (string, required): Original stable identity value.
- `CanonicalUrl` (string, required): Latest known canonical URL.
- `Title` (string, required): Latest known title at processing time.
- `PublishedAtUtc` (datetime, required): Blog publish timestamp.
- `InitialPostStatus` (enum, required): `NotAttempted | Succeeded | Failed`.
- `InitialPostAttemptedAtUtc` (datetime, optional): Last attempt timestamp.
- `InitialPostSucceededAtUtc` (datetime, optional): Success timestamp.
- `InitialPostResultCode` (string, optional): Last result code for initial post.
- `ReminderPostStatus` (enum, required): `NotAttempted | Succeeded | Failed`.
- `ReminderPostAttemptedAtUtc` (datetime, optional): Last reminder attempt timestamp.
- `ReminderPostSucceededAtUtc` (datetime, optional): Reminder success timestamp.
- `ReminderPostResultCode` (string, optional): Last result code for reminder post.
- `LastProcessedAtUtc` (datetime, required): Timestamp of latest processing pass.
- `ETag` (string, required by storage provider): Concurrency token.

### BlogPostPromotionRecord Validation Rules

- `PartitionKey` + `RowKey` must be unique.
- `InitialPostSucceededAtUtc` must exist only when `InitialPostStatus = Succeeded`.
- `ReminderPostSucceededAtUtc` must exist only when `ReminderPostStatus = Succeeded`.
- `ReminderPostStatus = Succeeded` requires `InitialPostStatus = Succeeded`.

## 3) PromotionRunSummary (endpoint response aggregate)

Represents the outcome of a single endpoint invocation.

### PromotionRunSummary Fields

- `RunId` (string, required): Correlation identifier for the run.
- `StartedAtUtc` (datetime, required)
- `CompletedAtUtc` (datetime, required)
- `EntriesEvaluated` (int, required)
- `NewPostsAttempted` (int, required)
- `NewPostsSucceeded` (int, required)
- `ReminderPostsAttempted` (int, required)
- `ReminderPostsSucceeded` (int, required)
- `EntriesSkippedAlreadyPosted` (int, required)
- `EntriesSkippedOutsideWindow` (int, required)
- `Failures` (array of `PromotionEntryFailure`, required)

## 4) PromotionEntryFailure (response detail)

Represents one failed posting attempt for an entry and phase.

### PromotionEntryFailure Fields

- `EntryIdentity` (string, required)
- `CanonicalUrl` (string, required)
- `Phase` (enum, required): `Initial | Reminder`
- `Platform` (string, required)
- `ErrorCode` (string, required)
- `ErrorMessage` (string, required)

## Relationships

- One `BlogFeedEntry` maps to zero or one `BlogPostPromotionRecord` by `EntryIdentity`.
- One `PromotionRunSummary` may include many `PromotionEntryFailure` records.
- One `BlogPostPromotionRecord` can progress through two ordered lifecycle phases: initial post then reminder post.

## State Transitions

### Initial post lifecycle

- `NotAttempted -> Succeeded` when eligible new entry is posted successfully.
- `NotAttempted -> Failed` when posting attempt fails.
- `Failed -> Succeeded` on later successful retry run.
- `Succeeded -> Succeeded` for subsequent runs (no duplicate post attempt).

### Reminder post lifecycle

- `NotAttempted -> Succeeded` when reminder is enabled, delay elapsed, and reminder post succeeds.
- `NotAttempted -> Failed` when reminder attempt fails.
- `Failed -> Succeeded` on later successful retry run.
- `Succeeded -> Succeeded` for subsequent runs (no duplicate reminder post attempt).

### Ordering invariant

For each endpoint run, no reminder transition is evaluated until new-entry initial-post evaluation pass is complete for the run.

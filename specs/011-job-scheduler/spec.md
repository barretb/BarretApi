# Feature Specification: Job Scheduler

**Feature Branch**: `011-job-scheduler`
**Created**: 2026-08-20
**Status**: Draft
**Input**: User description: "I want to start migrating jobs from Power Automate into BarretAPI. To do this we will first need to set up a schedule service to run jobs as scheduled from the API instead of relying on Power Automate."

## Overview

Recurring work in BarretApi is currently triggered by Power Automate flows that call API endpoints on a schedule. This feature moves that timing into the API itself: an in-process scheduler that stores job definitions in Azure Table Storage, fires them on cron schedules, and records run history.

The flows being migrated carry no logic of their own — each is a cron trigger plus an HTTP POST to an existing BarretApi endpoint. The scheduler therefore needs to own timing and invocation only; no flow logic is rebuilt.

## Goals

- Run recurring jobs from inside the API with no external scheduler.
- Manage schedules at runtime through the API — no redeploy to change a cron expression.
- Give visibility comparable to Power Automate's run history.
- Allow a per-flow cutover that can be reversed without a code change.

## Non-Goals

- Replacing the existing one-off scheduled social post feature (`ScheduledSocialPostRecord`). That table and its processor stay as they are; the scheduler simply owns the recurring trigger that drains it.
- A generic HTTP-calling job type. Jobs dispatch to named in-process handlers.
- Distributed scheduling across multiple instances. The `barretapi` Web App runs Always On with a single instance.
- A UI. Management is via API.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Recurring jobs fire on schedule (Priority: P1)

As the API owner, I want jobs to run automatically on their cron schedules, so that Power Automate is no longer required for recurring work.

**Why this priority**: This is the feature. Everything else supports it.

**Independent Test**: Create a job with a cron expression a minute or two out, wait, and confirm the underlying work happened and a successful run was recorded.

### User Story 2 - Manage schedules through the API (Priority: P1)

As the API owner, I want to create, edit, pause, and delete job definitions over HTTP, so that changing a schedule does not require a deploy.

**Why this priority**: Runtime management is the reason schedules live in storage rather than config. Without it the migration trades one deploy-free system for one that needs a deploy per change.

**Independent Test**: Create a job, confirm the returned next-run time, change its cron expression, confirm the next-run time is recomputed, disable it, and confirm it stops firing.

### User Story 3 - See what ran and what failed (Priority: P1)

As the API owner, I want a durable history of job runs and an email when a job finally fails, so that a broken job does not go unnoticed.

**Why this priority**: Losing Power Automate's run history without a replacement makes silent failure likely.

**Independent Test**: Force a handler failure, confirm the configured retries occur, confirm the run is recorded as failed with the error message, and confirm one notification email is sent.

### User Story 4 - Trigger a job by hand (Priority: P2)

As the API owner, I want to run a job immediately without disturbing its schedule, so that I can test a job or re-run one that failed.

**Independent Test**: Call the manual run endpoint, confirm the work happened and a run tagged `Manual` was recorded, and confirm `NextRunUtc` is unchanged.

### User Story 5 - Migrate one flow at a time (Priority: P2)

As the API owner, I want to move flows individually and reverse a move without deploying, so that a bad migration is low-risk.

**Independent Test**: With the scheduler enabled and one job active, pause that job and confirm its Power Automate counterpart can drive the same endpoint unchanged.

## Design

### Architecture

The scheduler is a vertical slice following the layering already in place: contracts and logic in `Core`, Azure Table persistence in `Infrastructure`, endpoints in `Api`.

| Component | Project | Responsibility |
|---|---|---|
| `JobSchedulerHostedService` | Api | `BackgroundService` tick loop; holds no state |
| `JobDispatcher` | Core | Due selection, claim, execute, retry, record, reschedule |
| `IScheduledJobHandler` | Core | Extension point — one implementation per migrated flow |
| `JobHandlerRegistry` | Core | Resolves handler by `JobType`; validates registrations at startup |
| `CronSchedule` | Core | Cronos wrapper — next occurrence in a given time zone |
| `IScheduledJobRepository` / `AzureTableScheduledJobRepository` | Core / Infrastructure | Job definitions |
| `IJobRunRepository` / `AzureTableJobRunRepository` | Core / Infrastructure | Run history |
| `Features/Jobs/*` | Api | FastEndpoints CRUD + manual run |

Splitting `JobDispatcher` from the hosted service is what makes the behavior testable: the dispatcher takes `TimeProvider`, so a fake clock can drive a week of schedule behavior in fast unit tests.

Endpoints and jobs both call the same underlying services, so no code path forks. `POST /api/social-posts/rss-promotion` and the `rss-promotion` job are two triggers on one implementation.

### Handler contract

```csharp
public interface IScheduledJobHandler
{
	string JobType { get; }
	Task<JobExecutionResult> ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken);
}
```

`JobExecutionContext` carries the job's name, run id, scheduled time, and its stored `ArgumentsJson`. Arguments are how one handler serves several schedules — for example two `tip-of-day` jobs posting different categories at different times.

Handlers to implement, each wrapping the service its endpoint already calls:

| `JobType` | Wraps |
|---|---|
| `process-scheduled-posts` | `IScheduledSocialPostProcessor` |
| `rss-promotion` | `IBlogPromotionOrchestrator` |
| `rss-random` | `RssRandomPostService` |
| `tip-of-day` | `TipOfDayService` |
| `nasa-apod` | `NasaApodPostService` |
| `satellite` | `NasaGibsPostService` |
| `purge-job-runs` | `IJobRunRepository` (retention purge) |

### Job definition

One Azure Table (default `scheduledjobs`). `RowKey` is the job's slug name, making names unique by construction and giving every endpoint a readable identifier.

| Field | Notes |
|---|---|
| `Name` | slug, RowKey — e.g. `daily-tip-dotnet` |
| `DisplayName` | human-readable label |
| `JobType` | handler key; must be registered |
| `CronExpression` | 5- or 6-field cron |
| `TimeZoneId` | default `UTC`; e.g. `America/Chicago` |
| `ArgumentsJson` | optional JSON passed to the handler |
| `IsEnabled` | pause without deleting |
| `NextRunUtc` | computed; the tick loop queries on it |
| `LastRunUtc`, `LastRunStatus`, `LastRunError`, `LastRunDurationMs` | at-a-glance state |
| `ConsecutiveFailureCount` | surfaced in the API |
| `MaxRetryCount` | default 2 |
| `RetryBaseDelaySeconds` | default 30 |
| `RunState`, `ClaimedAtUtc` | overlap guard |
| `CreatedAtUtc`, `UpdatedAtUtc` | audit |

### Run history

One Azure Table (default `jobruns`), one row per run: run id, job name, trigger type (`Scheduled` or `Manual`), scheduled time, start and end times, duration, status (`Succeeded`, `Failed`, `Aborted`), attempt count, and error message on failure. Rows are partitioned by job name so a job's history is a single-partition query returned newest-first.

Runs older than the retention window (default 30 days) are deleted by the built-in `purge-job-runs` job.

### API surface

All endpoints require the `X-Api-Key` header.

| Endpoint | Purpose |
|---|---|
| `GET /api/jobs` | List jobs with schedule and last-run state |
| `GET /api/jobs/{name}` | One job |
| `POST /api/jobs` | Create |
| `PUT /api/jobs/{name}` | Update cron, time zone, arguments, enabled flag, retry settings |
| `DELETE /api/jobs/{name}` | Delete |
| `POST /api/jobs/{name}/run` | Run immediately, out of band |
| `GET /api/jobs/{name}/runs` | Run history, newest first, paged |
| `GET /api/jobs/types` | Registered handler types and their expected arguments |

Create and update validate before writing: the cron expression is parsed, the time zone id resolved, the `JobType` checked against the registry, and `ArgumentsJson` parsed. A malformed schedule fails with `400` at edit time rather than silently never firing. Both responses echo the computed `NextRunUtc` so the caller can confirm the schedule resolved to the intended instant.

The CRUD endpoints remain available when the scheduler is disabled; only the tick loop is off.

### Execution semantics

**Tick.** Every 30 seconds (configurable) the dispatcher queries enabled jobs with `NextRunUtc <= now` and processes them sequentially. The table holds a handful of rows, so a partition scan is cheap and needs no secondary index.

**Claim and overlap.** A job is claimed by an ETag-conditional update setting `RunState = Running`, the same optimistic-concurrency technique as the existing `TryMarkProcessingAsync`. An ETag conflict, or a job already `Running`, is skipped and logged but not recorded, so a long-running job does not write a skip row every tick. A claim older than the claim timeout (default 30 minutes) is treated as stale and reclaimed, which recovers a job left `Running` by a process kill.

**Retry.** On failure the handler is retried up to `MaxRetryCount` times with exponential backoff (`RetryBaseDelaySeconds * 2^attempt`), within the same run. `MaxRetryCount` counts retries, not total attempts, so the default of 2 means at most three executions: the initial attempt plus two retries, at +30s and +60s. All attempts share one run-history row carrying an `AttemptCount`, so a run that succeeds on its second attempt reads as one successful run. Only when every attempt fails is the run recorded `Failed` and a notification sent via the existing `IEmailNotificationService`. That service does not currently rate-limit these emails — `IEmailRateLimiter` is injected but never consulted — so a job that is scheduled frequently and stays broken will send one email per failed run.

**Catch-up.** Missed occurrences run late, once. After a run completes, `NextRunUtc` is the next cron occurrence **after now** — not after the missed slot — which collapses any number of missed occurrences into a single catch-up run. On startup, any job whose `NextRunUtc` is already past fires on the first tick.

The same rule guards two related cases: re-enabling a paused job recomputes `NextRunUtc` from the moment of enabling, and editing the cron expression or time zone recomputes it from now. A job paused for a month does not fire immediately on resume.

**Manual runs.** `POST /api/jobs/{name}/run` executes the handler and records a run tagged `Manual`, but leaves `NextRunUtc` untouched. Testing a job by hand never disturbs its schedule. A manual run respects the claim: if the job is already running, the endpoint returns `409 Conflict` rather than starting a second execution. A disabled job can still be run manually.

**Shutdown.** On app stop, in-flight work receives the cancellation token, the run is recorded `Aborted`, and the claim is released with `NextRunUtc` left in the past so the next startup catches it up.

**Time zones and DST.** Cronos resolves cron expressions against the stored time zone, handling the skipped hour in spring and the ambiguous hour in autumn so a job fires exactly once on each transition day.

### Configuration

All configuration is supplied by the AppHost per the project's Aspire rule. `JobSchedulerOptions` follows the existing `Validate()` + `OptionsValidatorAdapter` + `ValidateOnStart()` pattern.

| Config key | Aspire parameter | Default |
|---|---|---|
| `JobScheduler:Enabled` | `job-scheduler-enabled` | `false` |
| `JobScheduler:TickIntervalSeconds` | `job-scheduler-tick-interval-seconds` | `30` |
| `JobScheduler:ClaimTimeoutMinutes` | `job-scheduler-claim-timeout-minutes` | `30` |
| `JobScheduler:RunRetentionDays` | `job-scheduler-run-retention-days` | `30` |
| `JobScheduler:TableStorage:ConnectionString` | `job-scheduler-table-storage-connection-string` | — |
| `JobScheduler:TableStorage:AccountEndpoint` | `job-scheduler-table-storage-account-endpoint` | — |
| `JobScheduler:TableStorage:JobsTableName` | `job-scheduler-jobs-table-name` | `scheduledjobs` |
| `JobScheduler:TableStorage:RunsTableName` | `job-scheduler-runs-table-name` | `jobruns` |
| `JobScheduler:TableStorage:PartitionKey` | `job-scheduler-partition-key` | `scheduled-job` |

`Enabled` defaults to `false` and stays `false` in local development. Without that default, every local `dotnet run` would begin firing real posts to Bluesky, Mastodon, and LinkedIn.

Storage validation matches the other features: one of `ConnectionString` or `AccountEndpoint` is required, and table names must be valid Azure Table names.

### New packages

| Package | Purpose |
|---|---|
| `Cronos` | Cron parsing with time zone and DST handling |
| `Microsoft.Extensions.TimeProvider.Testing` | Fake clock for dispatcher tests |

Versions are pinned in `Directory.Packages.props` during implementation.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The scheduler MUST execute enabled jobs whose next run time has passed, dispatching each to the handler registered for its `JobType`.
- **FR-002**: Job definitions MUST be stored in Azure Table Storage and be creatable, readable, updatable, and deletable through authenticated API endpoints without a redeploy.
- **FR-003**: Create and update MUST reject an unparseable cron expression, an unresolvable time zone id, an unregistered `JobType`, or malformed arguments JSON with `400` before persisting.
- **FR-004**: A job MUST NOT be executed concurrently with itself; a job already running MUST be skipped and the skip logged rather than recorded.
- **FR-005**: A claim older than the configured timeout MUST be reclaimable so a job left running by an abnormal termination recovers automatically.
- **FR-006**: A failed execution MUST be retried up to the job's `MaxRetryCount` with exponential backoff, all attempts recorded as a single run.
- **FR-007**: When every attempt fails, the run MUST be recorded as failed with the error message and a notification email sent through the existing notification service. This email is currently not rate-limited: a job scheduled frequently that stays broken will send one email per failed run.
- **FR-008**: Every scheduled and manual run MUST be recorded in the run history table and be retrievable per job, newest first.
- **FR-009**: Run history rows older than the configured retention period MUST be deleted by the built-in `purge-job-runs` job.
- **FR-010**: Missed occurrences MUST result in exactly one catch-up run; the next run time MUST be computed from the current time after every execution.
- **FR-011**: Enabling a disabled job, or editing its cron expression or time zone, MUST recompute the next run time from the current time.
- **FR-012**: A manual run MUST NOT alter the job's next run time.
- **FR-013**: Cron expressions MUST be evaluated in the job's stored time zone, firing exactly once per occurrence across DST transitions.
- **FR-014**: When `JobScheduler:Enabled` is false, the tick loop MUST NOT run while the management endpoints remain available.
- **FR-015**: All job management endpoints MUST require the `X-Api-Key` header.
- **FR-016**: On shutdown, an in-flight run MUST be recorded as aborted and its claim released with the next run time left in the past.

### Non-Functional Requirements

- **NFR-001**: `JobDispatcher` MUST take `TimeProvider` so schedule behavior is unit-testable without real waiting.
- **NFR-002**: The implementation MUST compile with zero warnings under `TreatWarningsAsErrors`.
- **NFR-003**: Job handlers MUST delegate to existing services rather than duplicating their logic.
- **NFR-004**: Error messages returned to clients MUST NOT expose stack traces.

## Deployment Prerequisite

The `barretapi` Azure Web App MUST have **Always On** enabled. Without it App Service unloads the process when idle and the tick loop stops. This is a portal setting, not code, and must be confirmed before any job is enabled in production.

## Migration Plan

Cutover happens one flow at a time; nothing flips at once.

1. Deploy with `JobScheduler:Enabled = false`. Behavior is unchanged and Power Automate continues to drive everything.
2. Create the job definitions through the API, disabled, mirroring the current Power Automate schedules. Verify the `NextRunUtc` returned for each.
3. Enable the scheduler. Enable one job and turn off its Power Automate counterpart. Watch a full cycle in the run history.
4. Repeat per flow. Delete each Power Automate flow once its job has run clean.

Because both triggers invoke the same services, a misbehaving job can be paused and its Power Automate flow re-enabled with no code change.

## Testing Strategy

Tests are written before implementation, using xUnit, NSubstitute, and Shouldly.

**`CronSchedule`** — next occurrence across time zones; both DST transitions; invalid expressions rejected.

**`JobDispatcher`** (fake `TimeProvider`, substituted repositories) — due selection; claim conflict skipped; stale claim reclaimed; retry then success recorded as one run; retries exhausted producing a failed run and one email; multiple missed occurrences collapsing into one catch-up run; manual run leaving `NextRunUtc` unchanged; enabling and cron edits recomputing `NextRunUtc`; abort on cancellation.

**Handlers** — each delegates to the correct service and maps its result, including argument binding from `ArgumentsJson`.

**Validators** — bad cron, bad time zone, unknown job type, malformed arguments JSON.

**Repositories** — Azure Table round-trips against Azurite in the integration test project, including ETag-conditional claim behavior.

## Open Questions

None.

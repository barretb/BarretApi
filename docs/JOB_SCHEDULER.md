# Job Scheduler

## What It Is

The job scheduler is an in-process background service that replaces the Power Automate flows previously used to trigger BarretApi's recurring work (RSS promotion, tip-of-day posts, NASA APOD, satellite snapshots, and draining scheduled social posts). Job definitions are stored in Azure Table Storage and are fully managed over the API — creating a job, changing its cron schedule, pausing it, or deleting it never requires a redeploy.

A background tick loop (`JobSchedulerHostedService`) polls for jobs whose next run time has passed, dispatches each to a named handler (`JobDispatcher` + `IScheduledJobHandler`), retries on failure with backoff, records the outcome in a run-history table, and reschedules. Handlers wrap the same services the existing `/api/social-posts/*` endpoints call — there is one implementation per piece of work, with two triggers (HTTP request and scheduled job) on top of it.

> **Prerequisite — Always On**
> The `barretapi` Azure Web App **must have Always On enabled**. Without it, App Service unloads the process when idle, which stops the tick loop entirely. Management endpoints and manual runs still work over HTTP (they wake the app like any request), but nothing fires on schedule until Always On is turned on. This is a portal setting, not code, and must be confirmed before any job is enabled in production.

## Enabling It

`JobScheduler:Enabled` defaults to **`false`**, including in local development. This means a plain `dotnet run` never starts posting to real Bluesky, Mastodon, or LinkedIn accounts by accident — the tick loop simply never runs until the setting is turned on.

Everything else works regardless of `Enabled`:

- The CRUD endpoints (`GET`/`POST`/`PUT`/`DELETE /api/jobs*`) work with the scheduler disabled — you can create, inspect, and pause job definitions freely.
- `POST /api/jobs/{name}/run` (manual run) also works with the scheduler disabled, since it bypasses the tick loop entirely.

Only the automatic, cron-driven firing of jobs is gated by `JobScheduler:Enabled`.

## Endpoint Reference

All endpoints require the `X-Api-Key` header, consistent with the rest of the API.

---

### GET /api/jobs — List Scheduled Jobs

Returns every job definition with its schedule and last-run state.

| Detail | Value |
|---|---|
| **Auth** | `X-Api-Key` header |
| **Content-Type** | — (no request body) |

#### Example

```http
GET /api/jobs
X-Api-Key: YOUR_API_KEY
```

#### Response — 200 OK

```json
{
  "jobs": [
    {
      "name": "purge-job-runs",
      "displayName": "Purge job run history",
      "jobType": "purge-job-runs",
      "cronExpression": "0 3 * * *",
      "timeZoneId": "UTC",
      "argumentsJson": null,
      "isEnabled": true,
      "isRunning": false,
      "nextRunUtc": "2026-08-22T03:00:00+00:00",
      "lastRunUtc": "2026-08-21T03:00:00+00:00",
      "lastRunStatus": "Succeeded",
      "lastRunError": null,
      "lastRunDurationMs": 214,
      "consecutiveFailureCount": 0,
      "maxRetryCount": 1,
      "retryBaseDelaySeconds": 30,
      "createdAtUtc": "2026-07-01T00:00:00+00:00",
      "updatedAtUtc": "2026-08-21T03:00:00+00:00"
    },
    {
      "name": "daily-tip-dotnet",
      "displayName": "Daily .NET Tip",
      "jobType": "tip-of-day",
      "cronExpression": "0 8 * * *",
      "timeZoneId": "America/Chicago",
      "argumentsJson": "{\"category\":\"dotnet\",\"platforms\":[\"bluesky\"]}",
      "isEnabled": true,
      "isRunning": false,
      "nextRunUtc": "2026-08-22T13:00:00+00:00",
      "lastRunUtc": null,
      "lastRunStatus": null,
      "lastRunError": null,
      "lastRunDurationMs": null,
      "consecutiveFailureCount": 0,
      "maxRetryCount": 2,
      "retryBaseDelaySeconds": 30,
      "createdAtUtc": "2026-08-21T15:00:00+00:00",
      "updatedAtUtc": "2026-08-21T15:00:00+00:00"
    }
  ]
}
```

The scheduler ships with `purge-job-runs` pre-seeded (see [Job Types and Arguments](#job-types-and-arguments)), so a fresh install always shows at least this one job.

#### Status Codes

| Code | Meaning |
|---|---|
| **200** | The job definitions. |
| **401** | Missing or invalid `X-Api-Key`. |

---

### GET /api/jobs/{name} — Get a Scheduled Job

Returns a single job definition by its slug name.

| Detail | Value |
|---|---|
| **Auth** | `X-Api-Key` header |
| **Content-Type** | — (no request body) |

#### Example

```http
GET /api/jobs/daily-tip-dotnet
X-Api-Key: YOUR_API_KEY
```

#### Response — 200 OK

Same shape as one entry in the [`GET /api/jobs`](#get-apijobs--list-scheduled-jobs) response above.

#### Status Codes

| Code | Meaning |
|---|---|
| **200** | The job definition. |
| **401** | Missing or invalid `X-Api-Key`. |
| **404** | No job with that name exists. |

---

### POST /api/jobs — Create a Scheduled Job

Validates the schedule before storing it and returns the computed next run time.

| Detail | Value |
|---|---|
| **Auth** | `X-Api-Key` header |
| **Content-Type** | `application/json` |

#### Request Body

| Field | Type | Required | Default | Description |
|---|---|---|---|---|
| `name` | `string` | Yes | — | Slug identity (lowercase letters, digits, single hyphens between them, e.g. `daily-tip-dotnet`). Max 100 chars. This becomes the row key and cannot be changed later. |
| `displayName` | `string` | No | `name` | Human-readable label. Max 200 chars. |
| `jobType` | `string` | Yes | — | Must be one of the registered types from [`GET /api/jobs/types`](#get-apijobstypes--list-registered-job-types). |
| `cronExpression` | `string` | Yes | — | 5- or 6-field cron expression. See [Cron and Time Zones](#cron-and-time-zones). Max 200 chars. |
| `timeZoneId` | `string` | No | `UTC` | IANA or Windows time zone id (e.g. `America/Chicago`). |
| `argumentsJson` | `string` | No | — | JSON object (as a string) passed to the handler. See [Job Types and Arguments](#job-types-and-arguments). |
| `isEnabled` | `bool` | No | `false` | Whether the job is active. A disabled job has `nextRunUtc: null` and never fires (but can still be run manually). |
| `maxRetryCount` | `int` | No | `2` | Retries after the first attempt, 0–10. See [Behaviour Worth Knowing](#behaviour-worth-knowing). |
| `retryBaseDelaySeconds` | `int` | No | `30` | Base for exponential backoff between attempts, 0–3600. |

Validation runs in this order and rejects with `400` before anything is written: `jobType` must be a registered handler, and `cronExpression`/`timeZoneId` together must parse into a valid schedule.

#### Example

```http
POST /api/jobs
X-Api-Key: YOUR_API_KEY
Content-Type: application/json
```

```json
{
  "name": "daily-tip-dotnet",
  "displayName": "Daily .NET Tip",
  "jobType": "tip-of-day",
  "cronExpression": "0 8 * * *",
  "timeZoneId": "America/Chicago",
  "argumentsJson": "{\"category\":\"dotnet\",\"platforms\":[\"bluesky\"]}",
  "isEnabled": true
}
```

#### Response — 200 OK

```json
{
  "name": "daily-tip-dotnet",
  "displayName": "Daily .NET Tip",
  "jobType": "tip-of-day",
  "cronExpression": "0 8 * * *",
  "timeZoneId": "America/Chicago",
  "argumentsJson": "{\"category\":\"dotnet\",\"platforms\":[\"bluesky\"]}",
  "isEnabled": true,
  "isRunning": false,
  "nextRunUtc": "2026-08-22T13:00:00+00:00",
  "lastRunUtc": null,
  "lastRunStatus": null,
  "lastRunError": null,
  "lastRunDurationMs": null,
  "consecutiveFailureCount": 0,
  "maxRetryCount": 2,
  "retryBaseDelaySeconds": 30,
  "createdAtUtc": "2026-08-21T15:00:00+00:00",
  "updatedAtUtc": "2026-08-21T15:00:00+00:00"
}
```

`nextRunUtc` is 13:00 UTC because 08:00 America/Chicago is UTC-5 during Central Daylight Time; it would be 14:00 UTC in the winter (UTC-6).

#### Response — 409 Conflict (Duplicate Name)

Unlike most `409` responses in this API, creating a job whose name already exists does **not** return a FastEndpoints error payload — it returns the existing job's full `JobResponse` body with status `409`, so the caller can see what is already there.

```json
{
  "name": "daily-tip-dotnet",
  "displayName": "Daily .NET Tip",
  "jobType": "tip-of-day",
  "cronExpression": "0 8 * * *",
  "timeZoneId": "America/Chicago",
  "argumentsJson": "{\"category\":\"dotnet\",\"platforms\":[\"bluesky\"]}",
  "isEnabled": true,
  "isRunning": false,
  "nextRunUtc": "2026-08-22T13:00:00+00:00",
  "lastRunUtc": null,
  "lastRunStatus": null,
  "lastRunError": null,
  "lastRunDurationMs": null,
  "consecutiveFailureCount": 0,
  "maxRetryCount": 2,
  "retryBaseDelaySeconds": 30,
  "createdAtUtc": "2026-08-21T15:00:00+00:00",
  "updatedAtUtc": "2026-08-21T15:00:00+00:00"
}
```

#### Status Codes

| Code | Meaning |
|---|---|
| **200** | The job was created. |
| **400** | Request validation failed (bad cron, bad time zone, unregistered `jobType`, or malformed `argumentsJson`). |
| **401** | Missing or invalid `X-Api-Key`. |
| **409** | A job with that name already exists (body is the existing job, not an error payload). |

---

### PUT /api/jobs/{name} — Update a Scheduled Job

Updates cron expression, time zone, arguments, the enabled flag, and retry settings. The job's name comes from the route and cannot be changed — any `name` in the request body is ignored.

| Detail | Value |
|---|---|
| **Auth** | `X-Api-Key` header |
| **Content-Type** | `application/json` |

#### Request Body

Same shape as [`POST /api/jobs`](#post-apijobs--create-a-scheduled-job). `name` in the body is ignored; the route segment is authoritative.

Changing the cron expression or time zone, or enabling a job that was previously disabled, recomputes `nextRunUtc` **from the moment of the update** — not from the job's old schedule. See [Behaviour Worth Knowing](#behaviour-worth-knowing) for why this matters.

#### Example — Change the Schedule

```http
PUT /api/jobs/daily-tip-dotnet
X-Api-Key: YOUR_API_KEY
Content-Type: application/json
```

```json
{
  "name": "daily-tip-dotnet",
  "displayName": "Daily .NET Tip",
  "jobType": "tip-of-day",
  "cronExpression": "0 9 * * *",
  "timeZoneId": "America/Chicago",
  "argumentsJson": "{\"category\":\"dotnet\",\"platforms\":[\"bluesky\"]}",
  "isEnabled": true
}
```

#### Response — 200 OK

Same shape as the [create response](#response--200-ok-1), with `nextRunUtc` recomputed for the new 09:00 Chicago time and `updatedAtUtc` bumped to now.

#### Status Codes

| Code | Meaning |
|---|---|
| **200** | The job was updated. |
| **400** | Request validation failed. |
| **401** | Missing or invalid `X-Api-Key`. |
| **404** | No job with that name exists. |

---

### DELETE /api/jobs/{name} — Delete a Scheduled Job

Removes the job definition. Its run history is left in place and ages out with the retention window (see [Configuration](#configuration-table)).

| Detail | Value |
|---|---|
| **Auth** | `X-Api-Key` header |
| **Content-Type** | — (no request body) |

#### Example

```http
DELETE /api/jobs/daily-tip-dotnet
X-Api-Key: YOUR_API_KEY
```

#### Status Codes

| Code | Meaning |
|---|---|
| **204** | The job was deleted. |
| **401** | Missing or invalid `X-Api-Key`. |
| **404** | No job with that name exists. |

---

### POST /api/jobs/{name}/run — Run a Job Immediately

Runs the job out of band, without changing its schedule. Works on a disabled job. If a run for this job is already in progress, the request is rejected with `409` rather than starting a second concurrent execution.

| Detail | Value |
|---|---|
| **Auth** | `X-Api-Key` header |
| **Content-Type** | — (no request body) |

#### Example

```http
POST /api/jobs/daily-tip-dotnet/run
X-Api-Key: YOUR_API_KEY
```

#### Response — 200 OK (Succeeded)

```json
{
  "runId": "run-20260821150512-a1b2c3",
  "jobName": "daily-tip-dotnet",
  "jobType": "tip-of-day",
  "triggerType": "Manual",
  "scheduledForUtc": null,
  "startedAtUtc": "2026-08-21T15:05:12+00:00",
  "completedAtUtc": "2026-08-21T15:05:13+00:00",
  "durationMs": 842,
  "status": "Succeeded",
  "attemptCount": 1,
  "summary": "Posted \"Prefer file-scoped namespaces for new C# files.\" to bluesky.",
  "errorMessage": null
}
```

#### Response — 502 Bad Gateway (Failed)

Every attempt failed. The response is still a full `JobRunResponse`, just with a non-`Succeeded` status and status code `502` instead of `200`.

```json
{
  "runId": "run-20260821150900-d4e5f6",
  "jobName": "daily-tip-dotnet",
  "jobType": "tip-of-day",
  "triggerType": "Manual",
  "scheduledForUtc": null,
  "startedAtUtc": "2026-08-21T15:09:00+00:00",
  "completedAtUtc": "2026-08-21T15:09:03+00:00",
  "durationMs": 3021,
  "status": "Failed",
  "attemptCount": 3,
  "summary": null,
  "errorMessage": "Failed to post \"Prefer file-scoped namespaces for new C# files.\" — bluesky: Rate limit exceeded"
}
```

#### Response — 409 Conflict (Already Running)

Unlike the create endpoint's `409`, this one returns the standard FastEndpoints validation-error payload, not a run object:

```json
{
  "statusCode": 409,
  "message": "One or more errors occurred.",
  "errors": {
    "name": ["A run is already in progress for this job."]
  }
}
```

#### Status Codes

| Code | Meaning |
|---|---|
| **200** | The run completed successfully. |
| **401** | Missing or invalid `X-Api-Key`. |
| **404** | No job with that name exists. |
| **409** | A run is already in progress for this job (error payload, not a run object). |
| **502** | The run completed but the job failed. |

---

### GET /api/jobs/{name}/runs — List a Job's Run History

Returns runs newest first. Runs age out after the configured retention window.

| Detail | Value |
|---|---|
| **Auth** | `X-Api-Key` header |
| **Content-Type** | — (no request body) |

#### Query Parameters

| Parameter | Type | Required | Default | Description |
|---|---|---|---|---|
| `maxCount` | `int` | No | `50` | How many runs to return, newest first. Clamped to the range 1–500. |

#### Example

```http
GET /api/jobs/daily-tip-dotnet/runs?maxCount=10
X-Api-Key: YOUR_API_KEY
```

#### Response — 200 OK

```json
{
  "runs": [
    {
      "runId": "run-20260821150512-a1b2c3",
      "jobName": "daily-tip-dotnet",
      "jobType": "tip-of-day",
      "triggerType": "Manual",
      "scheduledForUtc": null,
      "startedAtUtc": "2026-08-21T15:05:12+00:00",
      "completedAtUtc": "2026-08-21T15:05:13+00:00",
      "durationMs": 842,
      "status": "Succeeded",
      "attemptCount": 1,
      "summary": "Posted \"Prefer file-scoped namespaces for new C# files.\" to bluesky.",
      "errorMessage": null
    },
    {
      "runId": "run-20260821130000-b2c3d4",
      "jobName": "daily-tip-dotnet",
      "jobType": "tip-of-day",
      "triggerType": "Scheduled",
      "scheduledForUtc": "2026-08-21T13:00:00+00:00",
      "startedAtUtc": "2026-08-21T13:00:01+00:00",
      "completedAtUtc": "2026-08-21T13:00:34+00:00",
      "durationMs": 33210,
      "status": "Succeeded",
      "attemptCount": 2,
      "summary": "Posted \"Use primary constructors when they make dependencies obvious.\" to bluesky.",
      "errorMessage": null
    }
  ]
}
```

Note the second run: `attemptCount: 2` with status `Succeeded` and no `errorMessage` — the first attempt failed and was retried, but because the retry succeeded, only one run row exists and no failure email was sent. See [Behaviour Worth Knowing](#behaviour-worth-knowing).

#### Status Codes

| Code | Meaning |
|---|---|
| **200** | The run history. |
| **401** | Missing or invalid `X-Api-Key`. |
| **404** | No job with that name exists. |

---

### GET /api/jobs/types — List Registered Job Types

Returns the values accepted for a job definition's `jobType`. Adding a new one requires a code change and a deploy.

> **Route-ordering note:** `types` is a static segment competing with the `{Name}` route parameter on `GET /api/jobs/{name}`. FastEndpoints resolves the more specific static route first, so `GET /api/jobs/types` correctly returns the type list rather than a 404 for a job literally named `types`.

| Detail | Value |
|---|---|
| **Auth** | `X-Api-Key` header |
| **Content-Type** | — (no request body) |

#### Example

```http
GET /api/jobs/types
X-Api-Key: YOUR_API_KEY
```

#### Response — 200 OK

```json
{
  "jobTypes": [
    "nasa-apod",
    "process-scheduled-posts",
    "purge-job-runs",
    "rss-promotion",
    "rss-random",
    "satellite",
    "tip-of-day"
  ]
}
```

The list is sorted alphabetically, not by registration order.

#### Status Codes

| Code | Meaning |
|---|---|
| **200** | The registered job types. |
| **401** | Missing or invalid `X-Api-Key`. |

---

## Job Types and Arguments

`argumentsJson` is an optional JSON object (stored and transmitted as a **string**, so it must be JSON-escaped inside the request body). Field names deserialize case-insensitively but are shown here in their camelCase JSON form, matching each handler's `Arguments` record.

| `jobType` | Wraps | Arguments |
|---|---|---|
| `process-scheduled-posts` | `IScheduledSocialPostProcessor` | `{"maxCount":100}` |
| `rss-promotion` | `IBlogPromotionOrchestrator` | `{"feedUrl":"https://example.com/feed.xml","header":"Check this out!","recentDaysWindow":7}` |
| `rss-random` | `RssRandomPostService` | `{"feedUrl":"https://example.com/feed.xml","platforms":["bluesky"],"excludeTags":["draft"],"maxAgeDays":90,"header":"ICYMI"}` |
| `tip-of-day` | `TipOfDayService` | `{"category":"dotnet","platforms":["bluesky"],"leader":"Tip:"}` — `category` is **required**; the job fails if it is missing. |
| `nasa-apod` | `NasaApodPostService` | `{"platforms":["bluesky","mastodon"]}` — always posts *today's* APOD; there is no way to backfill an older date from a scheduled job. |
| `satellite` | `NasaGibsPostService` | `{"layer":"MODIS_Terra_CorrectedReflectance_TrueColor","title":"Satellite view of Ohio","description":"...","platforms":["bluesky"],"imageWidth":1200,"imageHeight":900}` — also accepts `bboxSouth`, `bboxWest`, `bboxNorth`, `bboxEast` (all `number`) to override the default bounding box per job; anything omitted falls back to the `NasaGibs` configuration defaults. Always posts *yesterday's* imagery. |
| `purge-job-runs` | `IJobRunRepository` (retention purge) | No arguments. Seeded automatically at `03:00 UTC` daily — see below. |

Every field on every `Arguments` record is optional except `tip-of-day`'s `category`. Omitting all of them (`argumentsJson: null`) is valid for every type except `tip-of-day`, and for `rss-random` falls back to `BlogPromotion:FeedUrl` if `feedUrl` is not supplied (the job fails if neither is set).

> **Empty `platforms` does not mean "post nowhere."** Passing `"platforms": []` (or omitting the field) means **use every configured platform** — the same "resolve to all configured platforms when the list is empty" behavior the `/api/social-posts/*` endpoints already use. This is easy to misread as "disable posting," but it is the opposite.

### The Built-In `purge-job-runs` Job

The scheduler seeds a `purge-job-runs` job at startup if one does not already exist (`03:00 UTC` daily, `maxRetryCount: 1`). It is a normal job in every respect — visible in `GET /api/jobs`, pausable, editable, and runnable by hand — the only thing "built-in" about it is that the API creates it once automatically. If you pause it or change its cron expression, that change survives a restart; the seeder only acts when no row named `purge-job-runs` exists yet.

## Cron and Time Zones

Both 5- and 6-field cron expressions are accepted:

- **5 fields**: `minute hour day month day-of-week` (standard cron).
- **6 fields**: `second minute hour day month day-of-week` — note that the extra field is the **leading** field (seconds), not a trailing sixth field. For example, `30 0 8 * * *` fires at `08:00:30`.

Schedules resolve against the job's `timeZoneId` using [Cronos](https://github.com/HangfireIO/Cronos), which correctly handles the skipped hour in spring and the ambiguous hour in autumn, so a job fires exactly once across both DST transitions.

`timeZoneId` **defaults to `UTC`** if omitted. This is a common trap when migrating from Power Automate: most existing flows were almost certainly scheduled in wall-clock local time, not UTC. Set `timeZoneId` explicitly (e.g. `America/Chicago`) when recreating a flow's schedule, or the job will fire at the wrong wall-clock time.

`timeZoneId` accepts either IANA (`America/Chicago`) or Windows (`Central Standard Time`) identifiers — whatever `TimeZoneInfo.FindSystemTimeZoneById` resolves on the host.

## Behaviour Worth Knowing

- **Missed occurrences run late, exactly once.** After every run, the next run time is computed as the next cron occurrence **after the current moment** — not after the slot that was missed. If the app is down for three days, the job does not fire three times when it comes back; it fires once, on the next tick, and then resumes its normal schedule.
- **Enabling a paused job, or editing its cron expression or time zone, recomputes `nextRunUtc` from now.** A job paused for a month does not fire immediately on resume — it picks up its schedule from the moment it is re-enabled or edited, exactly like the catch-up rule above.
- **A manual run never touches the schedule.** `POST /api/jobs/{name}/run` leaves `nextRunUtc` exactly as it was, whether the manual run succeeds, fails, or is aborted. It also works on a disabled job, and returns `409` (not a second execution) if the job is already running.
- **Retries share one run-history row.** `maxRetryCount` counts *retries*, not total attempts — the default of `2` means up to **three** executions per run: the initial attempt, then retries at `+30s` and `+60s` (base delay doubling each time). All three attempts are recorded as a single `JobRunRecord` with `attemptCount` reflecting how many were actually made. If any attempt succeeds, the run is `Succeeded` and no further attempts happen.
- **Only a fully-failed run sends email.** A run that succeeds on retry never triggers a notification — only when every configured attempt fails is the run marked `Failed` and a notification sent through the existing `IEmailNotificationService`. These emails are **not currently rate-limited**: a job that is scheduled frequently and stays broken will send one email per failed run.
- **A stale claim recovers automatically.** A job is claimed with an ETag-conditional write when a run starts. If the process dies mid-run, the claim is left `Running` — but a claim older than `ClaimTimeoutMinutes` (default 30) is treated as stale and reclaimed on the next tick, so a killed process does not permanently wedge a job.
- **Shutdown aborts in-flight work cleanly.** On app stop, an in-flight run is recorded as `Aborted`, its claim is released, and `nextRunUtc` is left unchanged (still in the past, since the run was due) — so the next startup's first tick catches it up.

## Configuration Table

All configuration is supplied by the AppHost, per this repository's Aspire configuration rule. `JobSchedulerOptions` follows the same `Validate()` + `ValidateOnStart()` pattern as the other feature options classes.

| Config Key | Aspire Parameter | Environment Variable | Default | Description |
|---|---|---|---|---|
| `JobScheduler:Enabled` | `job-scheduler-enabled` | `JobScheduler__Enabled` | `false` | Turns the tick loop on. Management endpoints work regardless. |
| `JobScheduler:TickIntervalSeconds` | `job-scheduler-tick-interval-seconds` | `JobScheduler__TickIntervalSeconds` | `30` | How often the dispatcher checks for due jobs. Range 1–3600. |
| `JobScheduler:ClaimTimeoutMinutes` | `job-scheduler-claim-timeout-minutes` | `JobScheduler__ClaimTimeoutMinutes` | `30` | How long a `Running` claim is honored before it is considered stale and reclaimed. |
| `JobScheduler:RunRetentionDays` | `job-scheduler-run-retention-days` | `JobScheduler__RunRetentionDays` | `30` | Age at which run-history rows become eligible for purge by the built-in `purge-job-runs` job. |
| `JobScheduler:TableStorage:ConnectionString` | — | `JobScheduler__TableStorage__ConnectionString` | — | Azure Table Storage connection string. Hardcoded to Azurite in the AppHost for local development, same as the other table-storage features. |
| `JobScheduler:TableStorage:AccountEndpoint` | — | `JobScheduler__TableStorage__AccountEndpoint` | — | Azure Table Storage account endpoint (for managed identity). Not currently wired to an AppHost parameter — set it directly as an Azure App Service application setting in production if you use this option instead of a connection string. |
| `JobScheduler:TableStorage:JobsTableName` | `job-scheduler-jobs-table-name` | `JobScheduler__TableStorage__JobsTableName` | `scheduledjobs` | Table holding job definitions. |
| `JobScheduler:TableStorage:RunsTableName` | `job-scheduler-runs-table-name` | `JobScheduler__TableStorage__RunsTableName` | `jobruns` | Table holding run history. |
| `JobScheduler:TableStorage:PartitionKey` | `job-scheduler-partition-key` | `JobScheduler__TableStorage__PartitionKey` | `scheduled-job` | Partition key used for both tables. |

Either `ConnectionString` or `AccountEndpoint` must be set — **before deploying this build at all**, not just before turning the scheduler on. `JobSchedulerOptions` is bound with `.ValidateOnStart()`, so a missing value fails application startup regardless of whether `JobScheduler:Enabled` is `true` or `false`, taking down the entire API rather than just the scheduler. Table names must be 3–63 characters, start with a letter, and contain only letters and numbers.

## Migrating from Power Automate

Cutover happens one flow at a time; nothing flips at once.

1. Deploy with `JobScheduler:Enabled = false`. Behavior is unchanged — Power Automate continues to drive everything.
2. Create the job definitions through the API, **disabled**, mirroring the current Power Automate schedules. Verify the `nextRunUtc` returned for each matches the intended local time (remember: `timeZoneId` defaults to `UTC`, so set it explicitly).
3. Enable the scheduler (`JobScheduler:Enabled = true`). Enable one job and turn off its Power Automate counterpart. Watch a full cycle in the run history (`GET /api/jobs/{name}/runs`).
4. Repeat per flow. Delete each Power Automate flow once its job has run clean.

Because both triggers invoke the same underlying services, a misbehaving job can be paused (`isEnabled: false`) at any point and its Power Automate flow re-enabled, with no code change and no redeploy.

## Troubleshooting

**A job never fires on schedule.**
Check, in order: is `JobScheduler:Enabled` set to `true`? Is Always On enabled on the Web App (see the callout at the top of this document)? Is the job itself `isEnabled: true` (`GET /api/jobs/{name}`)? Is `nextRunUtc` in the past — if so the tick loop should pick it up on its next pass (default every 30 seconds).

**A job disabled itself.**
If `isEnabled` flips to `false` on its own and `lastRunStatus` is `Failed`, check `lastRunError` — this happens when the job's `jobType` is no longer a registered handler, or its `cronExpression`/`timeZoneId` no longer parses (e.g. a typo introduced by a `PUT` that somehow bypassed validation, or a handler removed in a later deploy). This only auto-disables on the **scheduled** path — a manual run (`POST /api/jobs/{name}/run`) of a misconfigured job records a `Failed` run and leaves `isEnabled` alone, so you can safely use a manual run to check whether a job still works after a deploy. Either way a failure notification email is sent. Re-enable the job after fixing the underlying `jobType` or schedule.

**A job appears stuck with `isRunning: true`.**
A stale claim (the process that held it died) clears itself automatically after `ClaimTimeoutMinutes` (default 30). If it has been longer than that and the job is still shown running, check the application logs for a claim-related warning; the next tick after the timeout reclaims it.

**No failure emails are arriving.**
Confirm `Email:Enabled` is `true` and the SMTP settings are configured — email notifications no-op silently when disabled (see [Email Notifications](../README.md#email-notifications) in the README). Also remember only a **fully failed** run sends email; a run that succeeds on retry does not. These emails are **not currently rate-limited** — a job that is scheduled frequently and stays broken will send one email per failed run, not just once a day.

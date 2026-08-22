# Job Scheduler Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Run BarretApi's recurring work from an in-process scheduler with Table-stored, API-managed job definitions, replacing the Power Automate flows that currently call the API on a schedule.

**Architecture:** A `BackgroundService` in the Api project ticks every 30 seconds and delegates to `JobDispatcher` (Core), which selects due jobs, claims each with an ETag-conditional update, dispatches to a named `IScheduledJobHandler`, retries on failure, records a run, and recomputes the next run time. Handlers are thin wrappers over the services the existing endpoints already call, so scheduled and manual triggers share one code path. Persistence is two Azure Tables behind `IScheduledJobRepository` and `IJobRunRepository`.

**Tech Stack:** .NET 10, FastEndpoints 8.2, Azure.Data.Tables, Cronos (new), xUnit + NSubstitute + Shouldly, `Microsoft.Extensions.TimeProvider.Testing` (new).

**Spec:** [spec.md](spec.md)

## Global Constraints

- Central Package Management: every new package version goes in `Directory.Packages.props`; `.csproj` files reference packages **without** versions.
- `TreatWarningsAsErrors=true` — code must compile with zero warnings.
- C# files: file-scoped namespaces, **tabs** for indentation, CRLF line endings, UTF-8 BOM, Allman braces.
- Primary constructors assigned to `readonly` fields; `Async` suffix on async methods; `CancellationToken` last with `= default`.
- Interfaces and business logic in `BarretApi.Core`; Azure Table implementations in `BarretApi.Infrastructure`; endpoints in `BarretApi.Api`.
- New code MUST take `TimeProvider` rather than calling `DateTimeOffset.UtcNow` (NFR-001). Existing code is not changed to match.
- Tests: xUnit, NSubstitute, Shouldly. Class naming `ClassName_MethodName_Tests`, method naming `DoesSomething_GivenSomeCondition`. Arrange-Act-Assert separated by blank lines only — no `// Arrange` comments.
- Azure Table stores follow the existing two-constructor pattern (see `AzureTableGitHubRepositoryStore`): a public constructor building a `TableClient` from `IOptions<T>`, and an `internal` constructor taking a `TableClient` for tests. `BarretApi.Infrastructure` already has `InternalsVisibleTo` for `BarretApi.Infrastructure.UnitTests`.
- Do **not** add Azurite/Testcontainers integration tests. `tests/BarretApi.Integration.Tests` is an empty project and the CI runner has no Docker socket. Table repositories are tested with a substituted `TableClient`, matching `AzureTableGitHubRepositoryStore_Tests`.
- Default configuration values, verbatim from the spec: `Enabled` = `false`, `TickIntervalSeconds` = `30`, `ClaimTimeoutMinutes` = `30`, `RunRetentionDays` = `30`, `JobsTableName` = `scheduledjobs`, `RunsTableName` = `jobruns`, `PartitionKey` = `scheduled-job`, `MaxRetryCount` = `2`, `RetryBaseDelaySeconds` = `30`.
- `MaxRetryCount` counts retries, not total attempts: the default of 2 means at most three executions.
- Run all commands from repo root `C:\projects\BarretApi`. Branch is `011-job-scheduler`.
- `dotnet format` before every commit; stage what it changes.
- Note for verification: `dotnet test` has ~25 pre-existing failures in the Nasa test classes on `main`. They are unrelated to this work. Judge each task by the tests it adds, and do not "fix" those.

## File Structure

**Core** (`src/BarretApi.Core`)

| File | Responsibility |
|---|---|
| `Configuration/JobSchedulerOptions.cs` | Options + validation |
| `Models/ScheduledJobRecord.cs` | Job definition |
| `Models/JobRunRecord.cs` | One run |
| `Models/JobRunStatus.cs`, `Models/JobTriggerType.cs`, `Models/JobRunState.cs` | Enums |
| `Models/JobExecutionContext.cs`, `Models/JobExecutionResult.cs` | Handler contract data |
| `Models/ManualRunResult.cs` | Manual-run outcome |
| `Services/CronSchedule.cs` | Cronos wrapper |
| `Interfaces/IScheduledJobHandler.cs` | Handler extension point |
| `Services/JobHandlerRegistry.cs` | Handler lookup by `JobType` |
| `Interfaces/IScheduledJobRepository.cs`, `Interfaces/IJobRunRepository.cs` | Persistence contracts |
| `Services/JobDispatcher.cs` | Due selection, claim, execute, retry, record, reschedule |
| `Services/Jobs/*JobHandler.cs` | Seven handlers |

**Infrastructure** (`src/BarretApi.Infrastructure/Services`)

| File | Responsibility |
|---|---|
| `AzureTableScheduledJobRepository.cs` | Job definitions table |
| `AzureTableJobRunRepository.cs` | Run history table + purge |

**Api** (`src/BarretApi.Api`)

| File | Responsibility |
|---|---|
| `Scheduling/JobSchedulerHostedService.cs` | Tick loop |
| `Scheduling/BuiltInJobSeeder.cs` | Seeds `purge-job-runs` on startup |
| `Features/Jobs/*` | CRUD, manual run, run history, types |

---

### Task 1: Packages and `JobSchedulerOptions`

**Files:**
- Modify: `Directory.Packages.props`
- Modify: `src/BarretApi.Core/BarretApi.Core.csproj`
- Modify: `tests/BarretApi.Core.UnitTests/BarretApi.Core.UnitTests.csproj`
- Create: `src/BarretApi.Core/Configuration/JobSchedulerOptions.cs`
- Test: `tests/BarretApi.Core.UnitTests/Configuration/JobSchedulerOptions_Validate_Tests.cs`

**Interfaces:**
- Consumes: nothing (foundation task).
- Produces:
  - `JobSchedulerOptions` with `const string SectionName = "JobScheduler"`, properties `Enabled` (bool), `TickIntervalSeconds` (int), `ClaimTimeoutMinutes` (int), `RunRetentionDays` (int), `TableStorage` (`JobSchedulerTableStorageOptions`), and methods `string? Validate()` and `void ThrowIfInvalid()`.
  - `JobSchedulerTableStorageOptions` with `ConnectionString` (string?), `AccountEndpoint` (string), `JobsTableName` (string), `RunsTableName` (string), `PartitionKey` (string).

- [ ] **Step 1: Add the packages**

In `Directory.Packages.props`, add to the `<ItemGroup>` after the `System.ServiceModel.Syndication` line:

```xml
    <!-- Job scheduling -->
    <PackageVersion Include="Cronos" Version="0.11.0" />
```

and after the `Shouldly` line:

```xml
    <PackageVersion Include="Microsoft.Extensions.TimeProvider.Testing" Version="9.10.0" />
```

In `src/BarretApi.Core/BarretApi.Core.csproj`, add to the existing `<ItemGroup>`:

```xml
		<PackageReference Include="Cronos" />
```

In `tests/BarretApi.Core.UnitTests/BarretApi.Core.UnitTests.csproj`, add to the package `<ItemGroup>`:

```xml
		<PackageReference Include="Microsoft.Extensions.TimeProvider.Testing" />
```

- [ ] **Step 2: Verify the packages restore**

Run: `dotnet restore`
Expected: succeeds. If either version does not exist, run `dotnet add src/BarretApi.Core package Cronos` / `dotnet add tests/BarretApi.Core.UnitTests package Microsoft.Extensions.TimeProvider.Testing` to discover the current version, move the version into `Directory.Packages.props`, and strip it from the `.csproj`.

- [ ] **Step 3: Write the failing tests**

Create `tests/BarretApi.Core.UnitTests/Configuration/JobSchedulerOptions_Validate_Tests.cs`:

```csharp
using BarretApi.Core.Configuration;
using Shouldly;

namespace BarretApi.Core.UnitTests.Configuration;

public sealed class JobSchedulerOptions_Validate_Tests
{
	private static JobSchedulerOptions CreateValid() => new()
	{
		TableStorage = new JobSchedulerTableStorageOptions
		{
			ConnectionString = "UseDevelopmentStorage=true"
		}
	};

	[Fact]
	public void ReturnsNull_GivenValidOptions()
	{
		var options = CreateValid();

		options.Validate().ShouldBeNull();
	}

	[Fact]
	public void DefaultsSchedulerToDisabled()
	{
		var options = CreateValid();

		options.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void DefaultsMatchTheSpec()
	{
		var options = CreateValid();

		options.TickIntervalSeconds.ShouldBe(30);
		options.ClaimTimeoutMinutes.ShouldBe(30);
		options.RunRetentionDays.ShouldBe(30);
		options.TableStorage.JobsTableName.ShouldBe("scheduledjobs");
		options.TableStorage.RunsTableName.ShouldBe("jobruns");
		options.TableStorage.PartitionKey.ShouldBe("scheduled-job");
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(3_601)]
	public void ReturnsError_GivenTickIntervalOutOfRange(int seconds)
	{
		var options = new JobSchedulerOptions
		{
			TickIntervalSeconds = seconds,
			TableStorage = new JobSchedulerTableStorageOptions
			{
				ConnectionString = "UseDevelopmentStorage=true"
			}
		};

		options.Validate().ShouldContain("TickIntervalSeconds");
	}

	[Fact]
	public void ReturnsError_GivenClaimTimeoutNotPositive()
	{
		var options = new JobSchedulerOptions
		{
			ClaimTimeoutMinutes = 0,
			TableStorage = new JobSchedulerTableStorageOptions
			{
				ConnectionString = "UseDevelopmentStorage=true"
			}
		};

		options.Validate().ShouldContain("ClaimTimeoutMinutes");
	}

	[Fact]
	public void ReturnsError_GivenRetentionDaysNotPositive()
	{
		var options = new JobSchedulerOptions
		{
			RunRetentionDays = 0,
			TableStorage = new JobSchedulerTableStorageOptions
			{
				ConnectionString = "UseDevelopmentStorage=true"
			}
		};

		options.Validate().ShouldContain("RunRetentionDays");
	}

	[Fact]
	public void ReturnsError_GivenNeitherConnectionStringNorAccountEndpoint()
	{
		var options = new JobSchedulerOptions
		{
			TableStorage = new JobSchedulerTableStorageOptions()
		};

		options.Validate().ShouldContain("ConnectionString or AccountEndpoint");
	}

	[Fact]
	public void ReturnsError_GivenAccountEndpointIsNotHttps()
	{
		var options = new JobSchedulerOptions
		{
			TableStorage = new JobSchedulerTableStorageOptions
			{
				AccountEndpoint = "http://example.table.core.windows.net"
			}
		};

		options.Validate().ShouldContain("https");
	}

	[Theory]
	[InlineData("ab")]
	[InlineData("1jobs")]
	[InlineData("job-runs")]
	public void ReturnsError_GivenInvalidRunsTableName(string tableName)
	{
		var options = new JobSchedulerOptions
		{
			TableStorage = new JobSchedulerTableStorageOptions
			{
				ConnectionString = "UseDevelopmentStorage=true",
				RunsTableName = tableName
			}
		};

		options.Validate().ShouldContain("RunsTableName");
	}

	[Fact]
	public void ReturnsError_GivenEmptyPartitionKey()
	{
		var options = new JobSchedulerOptions
		{
			TableStorage = new JobSchedulerTableStorageOptions
			{
				ConnectionString = "UseDevelopmentStorage=true",
				PartitionKey = "   "
			}
		};

		options.Validate().ShouldContain("PartitionKey");
	}

	[Fact]
	public void ThrowIfInvalid_ThrowsWithTheValidationMessage()
	{
		var options = new JobSchedulerOptions
		{
			TableStorage = new JobSchedulerTableStorageOptions()
		};

		Should.Throw<InvalidOperationException>(options.ThrowIfInvalid)
			.Message.ShouldContain("ConnectionString or AccountEndpoint");
	}
}
```

- [ ] **Step 4: Run the tests to verify they fail**

Run: `dotnet test tests/BarretApi.Core.UnitTests --filter "FullyQualifiedName~JobSchedulerOptions_Validate_Tests"`
Expected: build failure — `JobSchedulerOptions` does not exist.

- [ ] **Step 5: Write the implementation**

Create `src/BarretApi.Core/Configuration/JobSchedulerOptions.cs`:

```csharp
namespace BarretApi.Core.Configuration;

public sealed class JobSchedulerOptions
{
	public const string SectionName = "JobScheduler";

	public bool Enabled { get; init; }
	public int TickIntervalSeconds { get; init; } = 30;
	public int ClaimTimeoutMinutes { get; init; } = 30;
	public int RunRetentionDays { get; init; } = 30;
	public JobSchedulerTableStorageOptions TableStorage { get; init; } = new();

	public string? Validate()
	{
		if (TickIntervalSeconds is < 1 or > 3_600)
		{
			return "JobScheduler:TickIntervalSeconds must be between 1 and 3600.";
		}

		if (ClaimTimeoutMinutes <= 0)
		{
			return "JobScheduler:ClaimTimeoutMinutes must be greater than zero.";
		}

		if (RunRetentionDays <= 0)
		{
			return "JobScheduler:RunRetentionDays must be greater than zero.";
		}

		if (!string.IsNullOrWhiteSpace(TableStorage.AccountEndpoint))
		{
			if (!Uri.TryCreate(TableStorage.AccountEndpoint, UriKind.Absolute, out var endpointUri))
			{
				return "JobScheduler:TableStorage:AccountEndpoint must be a valid absolute URL if provided.";
			}

			if (endpointUri.Scheme is not "https")
			{
				return "JobScheduler:TableStorage:AccountEndpoint must use https.";
			}
		}
		else if (string.IsNullOrWhiteSpace(TableStorage.ConnectionString))
		{
			return "JobScheduler:TableStorage:ConnectionString or AccountEndpoint must be configured.";
		}

		if (!IsValidTableName(TableStorage.JobsTableName))
		{
			return "JobScheduler:TableStorage:JobsTableName must be a valid Azure Table name (3-63 characters, start with a letter, letters and numbers only).";
		}

		if (!IsValidTableName(TableStorage.RunsTableName))
		{
			return "JobScheduler:TableStorage:RunsTableName must be a valid Azure Table name (3-63 characters, start with a letter, letters and numbers only).";
		}

		if (string.IsNullOrWhiteSpace(TableStorage.PartitionKey))
		{
			return "JobScheduler:TableStorage:PartitionKey is required.";
		}

		return null;
	}

	public void ThrowIfInvalid()
	{
		var error = Validate();
		if (!string.IsNullOrWhiteSpace(error))
		{
			throw new InvalidOperationException(error);
		}
	}

	private static bool IsValidTableName(string tableName)
	{
		if (string.IsNullOrWhiteSpace(tableName) || tableName.Length is < 3 or > 63)
		{
			return false;
		}

		if (!char.IsLetter(tableName[0]))
		{
			return false;
		}

		foreach (var character in tableName)
		{
			if (!char.IsLetterOrDigit(character))
			{
				return false;
			}
		}

		return true;
	}
}

public sealed class JobSchedulerTableStorageOptions
{
	public string? ConnectionString { get; init; }
	public string AccountEndpoint { get; init; } = string.Empty;
	public string JobsTableName { get; init; } = "scheduledjobs";
	public string RunsTableName { get; init; } = "jobruns";
	public string PartitionKey { get; init; } = "scheduled-job";
}
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test tests/BarretApi.Core.UnitTests --filter "FullyQualifiedName~JobSchedulerOptions_Validate_Tests"`
Expected: PASS, 13 tests.

- [ ] **Step 7: Format and commit**

```bash
dotnet format --include Directory.Packages.props src/BarretApi.Core/Configuration/JobSchedulerOptions.cs tests/BarretApi.Core.UnitTests/Configuration/JobSchedulerOptions_Validate_Tests.cs
git add Directory.Packages.props src/BarretApi.Core/BarretApi.Core.csproj tests/BarretApi.Core.UnitTests/BarretApi.Core.UnitTests.csproj src/BarretApi.Core/Configuration/JobSchedulerOptions.cs tests/BarretApi.Core.UnitTests/Configuration/JobSchedulerOptions_Validate_Tests.cs
git commit -m "feat: add job scheduler options"
```

---

### Task 2: Domain models and `CronSchedule`

**Files:**
- Create: `src/BarretApi.Core/Models/JobRunState.cs`
- Create: `src/BarretApi.Core/Models/JobRunStatus.cs`
- Create: `src/BarretApi.Core/Models/JobTriggerType.cs`
- Create: `src/BarretApi.Core/Models/ScheduledJobRecord.cs`
- Create: `src/BarretApi.Core/Models/JobRunRecord.cs`
- Create: `src/BarretApi.Core/Services/CronSchedule.cs`
- Test: `tests/BarretApi.Core.UnitTests/Services/CronSchedule_GetNextOccurrence_Tests.cs`

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces:
  - Enums `JobRunState { Idle, Running }`, `JobRunStatus { Succeeded, Failed, Aborted }`, `JobTriggerType { Scheduled, Manual }`.
  - `ScheduledJobRecord` — mutable class; properties are listed in the implementation step below.
  - `JobRunRecord` — mutable class; properties are listed below.
  - `CronSchedule` with `static bool TryParse(string expression, string timeZoneId, out CronSchedule? schedule, out string? error)` and `DateTimeOffset? GetNextOccurrence(DateTimeOffset afterUtc)`.

- [ ] **Step 1: Write the failing tests**

Create `tests/BarretApi.Core.UnitTests/Services/CronSchedule_GetNextOccurrence_Tests.cs`:

```csharp
using BarretApi.Core.Services;
using Shouldly;

namespace BarretApi.Core.UnitTests.Services;

public sealed class CronSchedule_GetNextOccurrence_Tests
{
	private static CronSchedule Parse(string expression, string timeZoneId)
	{
		CronSchedule.TryParse(expression, timeZoneId, out var schedule, out var error).ShouldBeTrue(error);
		return schedule!;
	}

	[Fact]
	public void ReturnsNextDailyOccurrence_GivenUtcSchedule()
	{
		var schedule = Parse("0 8 * * *", "UTC");

		var next = schedule.GetNextOccurrence(new DateTimeOffset(2026, 3, 10, 9, 0, 0, TimeSpan.Zero));

		next.ShouldBe(new DateTimeOffset(2026, 3, 11, 8, 0, 0, TimeSpan.Zero));
	}

	[Fact]
	public void ResolvesAgainstTheJobTimeZone_NotUtc()
	{
		var schedule = Parse("0 8 * * *", "America/Chicago");

		var next = schedule.GetNextOccurrence(new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero));

		next.ShouldBe(new DateTimeOffset(2026, 7, 1, 13, 0, 0, TimeSpan.Zero));
	}

	[Fact]
	public void AcceptsSixFieldExpressionsWithSeconds()
	{
		var schedule = Parse("30 0 8 * * *", "UTC");

		var next = schedule.GetNextOccurrence(new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero));

		next.ShouldBe(new DateTimeOffset(2026, 7, 1, 8, 0, 30, TimeSpan.Zero));
	}

	[Fact]
	public void FiresOnce_GivenSpringForwardTransition()
	{
		var schedule = Parse("0 2 * * *", "America/Chicago");

		var first = schedule.GetNextOccurrence(new DateTimeOffset(2026, 3, 7, 12, 0, 0, TimeSpan.Zero));
		var second = schedule.GetNextOccurrence(first!.Value);

		first.ShouldBe(new DateTimeOffset(2026, 3, 8, 8, 0, 0, TimeSpan.Zero));
		second.ShouldBe(new DateTimeOffset(2026, 3, 9, 7, 0, 0, TimeSpan.Zero));
	}

	[Fact]
	public void FiresOnce_GivenFallBackTransition()
	{
		var schedule = Parse("0 1 * * *", "America/Chicago");

		var first = schedule.GetNextOccurrence(new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero));
		var second = schedule.GetNextOccurrence(first!.Value);

		first.ShouldBe(new DateTimeOffset(2026, 11, 1, 6, 0, 0, TimeSpan.Zero));
		second.ShouldBe(new DateTimeOffset(2026, 11, 2, 7, 0, 0, TimeSpan.Zero));
	}

	[Fact]
	public void ReturnsUtcOffset_ForEveryOccurrence()
	{
		var schedule = Parse("0 8 * * *", "America/Chicago");

		var next = schedule.GetNextOccurrence(new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero));

		next!.Value.Offset.ShouldBe(TimeSpan.Zero);
	}

	[Theory]
	[InlineData("not a cron")]
	[InlineData("* * * *")]
	[InlineData("99 * * * *")]
	public void TryParse_ReturnsFalseWithAnError_GivenAnInvalidExpression(string expression)
	{
		var parsed = CronSchedule.TryParse(expression, "UTC", out var schedule, out var error);

		parsed.ShouldBeFalse();
		schedule.ShouldBeNull();
		error.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void TryParse_ReturnsFalseWithAnError_GivenAnUnknownTimeZone()
	{
		var parsed = CronSchedule.TryParse("0 8 * * *", "Mars/Olympus_Mons", out var schedule, out var error);

		parsed.ShouldBeFalse();
		schedule.ShouldBeNull();
		error.ShouldContain("time zone");
	}

	[Fact]
	public void TryParse_TreatsEmptyTimeZoneAsUtc()
	{
		var parsed = CronSchedule.TryParse("0 8 * * *", string.Empty, out var schedule, out var error);

		parsed.ShouldBeTrue(error);
		schedule!.GetNextOccurrence(new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero))
			.ShouldBe(new DateTimeOffset(2026, 7, 1, 8, 0, 0, TimeSpan.Zero));
	}
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/BarretApi.Core.UnitTests --filter "FullyQualifiedName~CronSchedule_GetNextOccurrence_Tests"`
Expected: build failure — `CronSchedule` does not exist.

- [ ] **Step 3: Write the enums and records**

Create `src/BarretApi.Core/Models/JobRunState.cs`:

```csharp
namespace BarretApi.Core.Models;

public enum JobRunState
{
	Idle = 0,
	Running = 1
}
```

Create `src/BarretApi.Core/Models/JobRunStatus.cs`:

```csharp
namespace BarretApi.Core.Models;

public enum JobRunStatus
{
	Succeeded = 0,
	Failed = 1,
	Aborted = 2
}
```

Create `src/BarretApi.Core/Models/JobTriggerType.cs`:

```csharp
namespace BarretApi.Core.Models;

public enum JobTriggerType
{
	Scheduled = 0,
	Manual = 1
}
```

Create `src/BarretApi.Core/Models/ScheduledJobRecord.cs`:

```csharp
namespace BarretApi.Core.Models;

/// <summary>
/// A recurring job definition. <see cref="Name"/> is the storage row key and is unique.
/// </summary>
public sealed class ScheduledJobRecord
{
	public required string Name { get; set; }
	public required string DisplayName { get; set; }
	public required string JobType { get; set; }
	public required string CronExpression { get; set; }
	public string TimeZoneId { get; set; } = "UTC";
	public string? ArgumentsJson { get; set; }
	public bool IsEnabled { get; set; }
	public DateTimeOffset? NextRunUtc { get; set; }
	public DateTimeOffset? LastRunUtc { get; set; }
	public JobRunStatus? LastRunStatus { get; set; }
	public string? LastRunError { get; set; }
	public long? LastRunDurationMs { get; set; }
	public int ConsecutiveFailureCount { get; set; }
	public int MaxRetryCount { get; set; } = 2;
	public int RetryBaseDelaySeconds { get; set; } = 30;
	public JobRunState RunState { get; set; } = JobRunState.Idle;
	public DateTimeOffset? ClaimedAtUtc { get; set; }
	public DateTimeOffset CreatedAtUtc { get; set; }
	public DateTimeOffset UpdatedAtUtc { get; set; }

	/// <summary>
	/// Storage concurrency token. Empty for records that have not been persisted yet.
	/// </summary>
	public string ETag { get; set; } = string.Empty;
}
```

Create `src/BarretApi.Core/Models/JobRunRecord.cs`:

```csharp
namespace BarretApi.Core.Models;

/// <summary>
/// One execution of a job, successful or not. Retries within an execution share a single record.
/// </summary>
public sealed class JobRunRecord
{
	public required string RunId { get; set; }
	public required string JobName { get; set; }
	public required string JobType { get; set; }
	public required JobTriggerType TriggerType { get; set; }
	public DateTimeOffset? ScheduledForUtc { get; set; }
	public required DateTimeOffset StartedAtUtc { get; set; }
	public DateTimeOffset? CompletedAtUtc { get; set; }
	public long DurationMs { get; set; }
	public required JobRunStatus Status { get; set; }
	public int AttemptCount { get; set; }
	public string? Summary { get; set; }
	public string? ErrorMessage { get; set; }
}
```

- [ ] **Step 4: Write `CronSchedule`**

Create `src/BarretApi.Core/Services/CronSchedule.cs`:

```csharp
using Cronos;

namespace BarretApi.Core.Services;

/// <summary>
/// A parsed cron expression bound to a time zone. Wrapping Cronos keeps the
/// time zone and DST rules in one testable place.
/// </summary>
public sealed class CronSchedule
{
	private readonly CronExpression _expression;
	private readonly TimeZoneInfo _timeZone;

	private CronSchedule(CronExpression expression, TimeZoneInfo timeZone)
	{
		_expression = expression;
		_timeZone = timeZone;
	}

	public static bool TryParse(
		string expression,
		string timeZoneId,
		out CronSchedule? schedule,
		out string? error)
	{
		schedule = null;
		error = null;

		if (string.IsNullOrWhiteSpace(expression))
		{
			error = "Cron expression is required.";
			return false;
		}

		var trimmed = expression.Trim();
		var fieldCount = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
		var format = fieldCount == 6 ? CronFormat.IncludeSeconds : CronFormat.Standard;

		CronExpression parsed;
		try
		{
			parsed = CronExpression.Parse(trimmed, format);
		}
		catch (CronFormatException ex)
		{
			error = $"Cron expression is not valid: {ex.Message}";
			return false;
		}

		TimeZoneInfo timeZone;
		if (string.IsNullOrWhiteSpace(timeZoneId))
		{
			timeZone = TimeZoneInfo.Utc;
		}
		else
		{
			try
			{
				timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId.Trim());
			}
			catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
			{
				error = $"Unknown time zone '{timeZoneId}'.";
				return false;
			}
		}

		schedule = new CronSchedule(parsed, timeZone);
		return true;
	}

	/// <summary>
	/// The first occurrence strictly after <paramref name="afterUtc"/>, expressed in UTC.
	/// Returns null when the expression has no further occurrences.
	/// </summary>
	public DateTimeOffset? GetNextOccurrence(DateTimeOffset afterUtc)
	{
		return _expression.GetNextOccurrence(afterUtc.ToUniversalTime(), _timeZone, inclusive: false);
	}
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/BarretApi.Core.UnitTests --filter "FullyQualifiedName~CronSchedule_GetNextOccurrence_Tests"`
Expected: PASS, 11 tests.

If a DST test fails, do not edit the assertion to match the output. Confirm the arithmetic first: America/Chicago is UTC-6 in winter and UTC-5 in summer, so 02:00 local on 2026-03-08 is 08:00Z and 02:00 local on 2026-03-09 is 07:00Z.

- [ ] **Step 6: Format and commit**

```bash
dotnet format --include src/BarretApi.Core/Models/JobRunState.cs src/BarretApi.Core/Models/JobRunStatus.cs src/BarretApi.Core/Models/JobTriggerType.cs src/BarretApi.Core/Models/ScheduledJobRecord.cs src/BarretApi.Core/Models/JobRunRecord.cs src/BarretApi.Core/Services/CronSchedule.cs tests/BarretApi.Core.UnitTests/Services/CronSchedule_GetNextOccurrence_Tests.cs
git add src/BarretApi.Core/Models src/BarretApi.Core/Services/CronSchedule.cs tests/BarretApi.Core.UnitTests/Services/CronSchedule_GetNextOccurrence_Tests.cs
git commit -m "feat: add job scheduler domain models and cron schedule"
```

---

### Task 3: Handler contract and `JobHandlerRegistry`

**Files:**
- Create: `src/BarretApi.Core/Models/JobExecutionContext.cs`
- Create: `src/BarretApi.Core/Models/JobExecutionResult.cs`
- Create: `src/BarretApi.Core/Interfaces/IScheduledJobHandler.cs`
- Create: `src/BarretApi.Core/Services/JobHandlerRegistry.cs`
- Test: `tests/BarretApi.Core.UnitTests/Services/JobHandlerRegistry_Tests.cs`

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces:
  - `JobExecutionContext(string JobName, string JobType, string RunId, DateTimeOffset? ScheduledForUtc, string? ArgumentsJson)` — positional record.
  - `JobExecutionResult` with `static JobExecutionResult Ok(string? summary = null)` and `static JobExecutionResult Fail(string errorMessage, string? summary = null)`, properties `bool Success`, `string? Summary`, `string? ErrorMessage`.
  - `IScheduledJobHandler` with `string JobType { get; }` and `Task<JobExecutionResult> ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken = default)`.
  - `JobHandlerRegistry(IEnumerable<IScheduledJobHandler> handlers)` with `IReadOnlyList<string> RegisteredTypes { get; }`, `bool IsRegistered(string? jobType)`, `IScheduledJobHandler Resolve(string jobType)`.

- [ ] **Step 1: Write the failing tests**

Create `tests/BarretApi.Core.UnitTests/Services/JobHandlerRegistry_Tests.cs`:

```csharp
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Core.Services;
using Shouldly;

namespace BarretApi.Core.UnitTests.Services;

public sealed class JobHandlerRegistry_Tests
{
	private sealed class StubHandler(string jobType) : IScheduledJobHandler
	{
		public string JobType { get; } = jobType;

		public Task<JobExecutionResult> ExecuteAsync(
			JobExecutionContext context,
			CancellationToken cancellationToken = default)
			=> Task.FromResult(JobExecutionResult.Ok("stub"));
	}

	[Fact]
	public void Resolve_ReturnsTheHandler_GivenARegisteredType()
	{
		var handler = new StubHandler("tip-of-day");
		var registry = new JobHandlerRegistry([handler]);

		registry.Resolve("tip-of-day").ShouldBeSameAs(handler);
	}

	[Fact]
	public void Resolve_IsCaseInsensitive()
	{
		var handler = new StubHandler("tip-of-day");
		var registry = new JobHandlerRegistry([handler]);

		registry.Resolve("Tip-Of-Day").ShouldBeSameAs(handler);
	}

	[Fact]
	public void Resolve_Throws_GivenAnUnregisteredType()
	{
		var registry = new JobHandlerRegistry([new StubHandler("tip-of-day")]);

		Should.Throw<InvalidOperationException>(() => registry.Resolve("nope"))
			.Message.ShouldContain("nope");
	}

	[Fact]
	public void IsRegistered_ReturnsFalse_GivenAnUnregisteredType()
	{
		var registry = new JobHandlerRegistry([new StubHandler("tip-of-day")]);

		registry.IsRegistered("nope").ShouldBeFalse();
	}

	[Fact]
	public void IsRegistered_ReturnsFalse_GivenNullOrWhitespace()
	{
		var registry = new JobHandlerRegistry([new StubHandler("tip-of-day")]);

		registry.IsRegistered("   ").ShouldBeFalse();
	}

	[Fact]
	public void Constructor_Throws_GivenDuplicateJobTypes()
	{
		Should.Throw<InvalidOperationException>(() =>
				new JobHandlerRegistry([new StubHandler("tip-of-day"), new StubHandler("TIP-OF-DAY")]))
			.Message.ShouldContain("tip-of-day");
	}

	[Fact]
	public void Constructor_Throws_GivenAHandlerWithABlankJobType()
	{
		Should.Throw<InvalidOperationException>(() => new JobHandlerRegistry([new StubHandler("  ")]));
	}

	[Fact]
	public void RegisteredTypes_ReturnsEveryTypeSorted()
	{
		var registry = new JobHandlerRegistry(
			[new StubHandler("tip-of-day"), new StubHandler("nasa-apod")]);

		registry.RegisteredTypes.ShouldBe(["nasa-apod", "tip-of-day"]);
	}
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/BarretApi.Core.UnitTests --filter "FullyQualifiedName~JobHandlerRegistry_Tests"`
Expected: build failure — `IScheduledJobHandler` does not exist.

- [ ] **Step 3: Write the contract types**

Create `src/BarretApi.Core/Models/JobExecutionContext.cs`:

```csharp
namespace BarretApi.Core.Models;

/// <summary>
/// Everything a handler is told about the run it is performing.
/// <paramref name="ScheduledForUtc"/> is null for manual runs.
/// </summary>
public sealed record JobExecutionContext(
	string JobName,
	string JobType,
	string RunId,
	DateTimeOffset? ScheduledForUtc,
	string? ArgumentsJson);
```

Create `src/BarretApi.Core/Models/JobExecutionResult.cs`:

```csharp
namespace BarretApi.Core.Models;

/// <summary>
/// The outcome of a single handler execution. Handlers report failure by
/// returning <see cref="Fail"/> or by throwing; the dispatcher treats both alike.
/// </summary>
public sealed class JobExecutionResult
{
	private JobExecutionResult(bool success, string? summary, string? errorMessage)
	{
		Success = success;
		Summary = summary;
		ErrorMessage = errorMessage;
	}

	public bool Success { get; }
	public string? Summary { get; }
	public string? ErrorMessage { get; }

	public static JobExecutionResult Ok(string? summary = null) => new(true, summary, null);

	public static JobExecutionResult Fail(string errorMessage, string? summary = null)
		=> new(false, summary, errorMessage);
}
```

Create `src/BarretApi.Core/Interfaces/IScheduledJobHandler.cs`:

```csharp
using BarretApi.Core.Models;

namespace BarretApi.Core.Interfaces;

/// <summary>
/// Performs the work for one job type. Implementations delegate to existing
/// services rather than duplicating their logic.
/// </summary>
public interface IScheduledJobHandler
{
	/// <summary>
	/// The stable key stored on a job definition, e.g. "tip-of-day". Matched case-insensitively.
	/// </summary>
	string JobType { get; }

	Task<JobExecutionResult> ExecuteAsync(
		JobExecutionContext context,
		CancellationToken cancellationToken = default);
}
```

- [ ] **Step 4: Write the registry**

Create `src/BarretApi.Core/Services/JobHandlerRegistry.cs`:

```csharp
using BarretApi.Core.Interfaces;

namespace BarretApi.Core.Services;

/// <summary>
/// Maps a job definition's JobType to the handler that runs it. Constructed at
/// startup so a duplicate or blank job type fails fast rather than at tick time.
/// </summary>
public sealed class JobHandlerRegistry
{
	private readonly Dictionary<string, IScheduledJobHandler> _handlers;

	public JobHandlerRegistry(IEnumerable<IScheduledJobHandler> handlers)
	{
		ArgumentNullException.ThrowIfNull(handlers);

		_handlers = new Dictionary<string, IScheduledJobHandler>(StringComparer.OrdinalIgnoreCase);

		foreach (var handler in handlers)
		{
			if (string.IsNullOrWhiteSpace(handler.JobType))
			{
				throw new InvalidOperationException(
					$"Job handler {handler.GetType().Name} has a blank JobType.");
			}

			var jobType = handler.JobType.Trim();
			if (!_handlers.TryAdd(jobType, handler))
			{
				throw new InvalidOperationException(
					$"More than one job handler is registered for job type '{jobType}'.");
			}
		}

		RegisteredTypes = [.. _handlers.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase)];
	}

	public IReadOnlyList<string> RegisteredTypes { get; }

	public bool IsRegistered(string? jobType)
		=> !string.IsNullOrWhiteSpace(jobType) && _handlers.ContainsKey(jobType.Trim());

	public IScheduledJobHandler Resolve(string jobType)
	{
		if (string.IsNullOrWhiteSpace(jobType) || !_handlers.TryGetValue(jobType.Trim(), out var handler))
		{
			throw new InvalidOperationException($"No job handler is registered for job type '{jobType}'.");
		}

		return handler;
	}
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/BarretApi.Core.UnitTests --filter "FullyQualifiedName~JobHandlerRegistry_Tests"`
Expected: PASS, 8 tests.

- [ ] **Step 6: Format and commit**

```bash
dotnet format --include src/BarretApi.Core/Models/JobExecutionContext.cs src/BarretApi.Core/Models/JobExecutionResult.cs src/BarretApi.Core/Interfaces/IScheduledJobHandler.cs src/BarretApi.Core/Services/JobHandlerRegistry.cs tests/BarretApi.Core.UnitTests/Services/JobHandlerRegistry_Tests.cs
git add src/BarretApi.Core/Models/JobExecutionContext.cs src/BarretApi.Core/Models/JobExecutionResult.cs src/BarretApi.Core/Interfaces/IScheduledJobHandler.cs src/BarretApi.Core/Services/JobHandlerRegistry.cs tests/BarretApi.Core.UnitTests/Services/JobHandlerRegistry_Tests.cs
git commit -m "feat: add job handler contract and registry"
```

---

### Task 4: Repository contracts and `JobDispatcher` scheduled runs

This task builds the dispatcher's main path: select due jobs, claim, execute once, record the run, reschedule. Retry, notification, manual runs, and stale-claim recovery arrive in Tasks 5 and 6.

**Files:**
- Create: `src/BarretApi.Core/Interfaces/IScheduledJobRepository.cs`
- Create: `src/BarretApi.Core/Interfaces/IJobRunRepository.cs`
- Create: `src/BarretApi.Core/Services/JobDispatcher.cs`
- Test: `tests/BarretApi.Core.UnitTests/Services/JobDispatcher_RunDueJobsAsync_Tests.cs`

**Interfaces:**
- Consumes: `ScheduledJobRecord`, `JobRunRecord`, `JobRunState`, `JobRunStatus`, `JobTriggerType`, `JobExecutionContext`, `JobExecutionResult` (Task 2/3); `CronSchedule.TryParse` / `GetNextOccurrence` (Task 2); `JobHandlerRegistry.IsRegistered` / `Resolve` (Task 3); `JobSchedulerOptions` (Task 1).
- Produces:
  - `IScheduledJobRepository` with `GetAllAsync`, `GetByNameAsync`, `GetDueAsync`, `CreateAsync`, `UpdateAsync`, `TryClaimAsync`, `DeleteAsync` — exact signatures below.
  - `IJobRunRepository` with `AddAsync`, `GetByJobAsync`, `PurgeOlderThanAsync` — exact signatures below.
  - `JobDispatcher` with `Task<int> RunDueJobsAsync(CancellationToken cancellationToken = default)`.

- [ ] **Step 1: Write the repository interfaces**

These are contracts only — no tests of their own; Task 4's tests substitute them, and Tasks 7 and 8 implement them.

Create `src/BarretApi.Core/Interfaces/IScheduledJobRepository.cs`:

```csharp
using BarretApi.Core.Models;

namespace BarretApi.Core.Interfaces;

public interface IScheduledJobRepository
{
	Task<IReadOnlyList<ScheduledJobRecord>> GetAllAsync(CancellationToken cancellationToken = default);

	Task<ScheduledJobRecord?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

	/// <summary>
	/// Enabled jobs whose <see cref="ScheduledJobRecord.NextRunUtc"/> is at or before
	/// <paramref name="nowUtc"/>. Jobs already marked running are included so the caller
	/// can decide whether the claim is stale.
	/// </summary>
	Task<IReadOnlyList<ScheduledJobRecord>> GetDueAsync(
		DateTimeOffset nowUtc,
		CancellationToken cancellationToken = default);

	Task CreateAsync(ScheduledJobRecord job, CancellationToken cancellationToken = default);

	/// <summary>
	/// Unconditional update. Refreshes <see cref="ScheduledJobRecord.ETag"/> in place.
	/// </summary>
	Task UpdateAsync(ScheduledJobRecord job, CancellationToken cancellationToken = default);

	/// <summary>
	/// Marks the job running, conditional on its current ETag. Returns false when another
	/// writer won the race. On success the record's RunState, ClaimedAtUtc and ETag are updated in place.
	/// </summary>
	Task<bool> TryClaimAsync(
		ScheduledJobRecord job,
		DateTimeOffset nowUtc,
		CancellationToken cancellationToken = default);

	Task DeleteAsync(string name, CancellationToken cancellationToken = default);
}
```

Create `src/BarretApi.Core/Interfaces/IJobRunRepository.cs`:

```csharp
using BarretApi.Core.Models;

namespace BarretApi.Core.Interfaces;

public interface IJobRunRepository
{
	Task AddAsync(JobRunRecord run, CancellationToken cancellationToken = default);

	/// <summary>
	/// Runs for one job, newest first, capped at <paramref name="maxCount"/>.
	/// </summary>
	Task<IReadOnlyList<JobRunRecord>> GetByJobAsync(
		string jobName,
		int maxCount,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Deletes runs that started before <paramref name="cutoffUtc"/>. Returns the number deleted.
	/// </summary>
	Task<int> PurgeOlderThanAsync(
		DateTimeOffset cutoffUtc,
		CancellationToken cancellationToken = default);
}
```

- [ ] **Step 2: Write the failing tests**

Create `tests/BarretApi.Core.UnitTests/Services/JobDispatcher_RunDueJobsAsync_Tests.cs`:

```csharp
using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;

namespace BarretApi.Core.UnitTests.Services;

public sealed class JobDispatcher_RunDueJobsAsync_Tests
{
	private static readonly DateTimeOffset Now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

	private readonly IScheduledJobRepository _jobRepository = Substitute.For<IScheduledJobRepository>();
	private readonly IJobRunRepository _runRepository = Substitute.For<IJobRunRepository>();
	private readonly FakeTimeProvider _timeProvider = new(Now);
	private readonly List<JobRunRecord> _recordedRuns = [];

	public JobDispatcher_RunDueJobsAsync_Tests()
	{
		_jobRepository.TryClaimAsync(Arg.Any<ScheduledJobRecord>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
			.Returns(callInfo =>
			{
				var job = callInfo.Arg<ScheduledJobRecord>();
				job.RunState = JobRunState.Running;
				job.ClaimedAtUtc = callInfo.ArgAt<DateTimeOffset>(1);
				return true;
			});

		_runRepository.AddAsync(Arg.Any<JobRunRecord>(), Arg.Any<CancellationToken>())
			.Returns(callInfo =>
			{
				_recordedRuns.Add(callInfo.Arg<JobRunRecord>());
				return Task.CompletedTask;
			});
	}

	private sealed class StubHandler(string jobType, Func<JobExecutionContext, JobExecutionResult> behavior)
		: IScheduledJobHandler
	{
		public string JobType { get; } = jobType;
		public int CallCount { get; private set; }
		public JobExecutionContext? LastContext { get; private set; }

		public Task<JobExecutionResult> ExecuteAsync(
			JobExecutionContext context,
			CancellationToken cancellationToken = default)
		{
			CallCount++;
			LastContext = context;
			return Task.FromResult(behavior(context));
		}
	}

	private static ScheduledJobRecord CreateJob(
		string name = "daily-tip",
		string cron = "0 12 * * *",
		DateTimeOffset? nextRunUtc = null)
		=> new()
		{
			Name = name,
			DisplayName = name,
			JobType = "tip-of-day",
			CronExpression = cron,
			TimeZoneId = "UTC",
			IsEnabled = true,
			NextRunUtc = nextRunUtc ?? Now.AddMinutes(-1),
			MaxRetryCount = 0,
			RetryBaseDelaySeconds = 0,
			CreatedAtUtc = Now.AddDays(-1),
			UpdatedAtUtc = Now.AddDays(-1),
			ETag = "etag-1"
		};

	private JobDispatcher CreateSut(params IScheduledJobHandler[] handlers)
		=> new(
			_jobRepository,
			_runRepository,
			new JobHandlerRegistry(handlers),
			Options.Create(new JobSchedulerOptions
			{
				TableStorage = new JobSchedulerTableStorageOptions { ConnectionString = "UseDevelopmentStorage=true" }
			}),
			_timeProvider,
			NullLogger<JobDispatcher>.Instance);

	private void SetDue(params ScheduledJobRecord[] jobs)
		=> _jobRepository.GetDueAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
			.Returns(jobs);

	[Fact]
	public async Task ExecutesTheRegisteredHandler_GivenADueJob()
	{
		var handler = new StubHandler("tip-of-day", _ => JobExecutionResult.Ok("posted"));
		SetDue(CreateJob());

		var executed = await CreateSut(handler).RunDueJobsAsync();

		executed.ShouldBe(1);
		handler.CallCount.ShouldBe(1);
	}

	[Fact]
	public async Task PassesTheStoredArgumentsToTheHandler()
	{
		var handler = new StubHandler("tip-of-day", _ => JobExecutionResult.Ok());
		var job = CreateJob();
		job.ArgumentsJson = """{"category":"dotnet"}""";
		SetDue(job);

		await CreateSut(handler).RunDueJobsAsync();

		handler.LastContext!.ArgumentsJson.ShouldBe("""{"category":"dotnet"}""");
		handler.LastContext.JobName.ShouldBe("daily-tip");
		handler.LastContext.RunId.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public async Task RecordsASucceededRun()
	{
		var handler = new StubHandler("tip-of-day", _ => JobExecutionResult.Ok("posted 1 tip"));
		SetDue(CreateJob());

		await CreateSut(handler).RunDueJobsAsync();

		var run = _recordedRuns.ShouldHaveSingleItem();
		run.Status.ShouldBe(JobRunStatus.Succeeded);
		run.TriggerType.ShouldBe(JobTriggerType.Scheduled);
		run.JobName.ShouldBe("daily-tip");
		run.AttemptCount.ShouldBe(1);
		run.Summary.ShouldBe("posted 1 tip");
		run.CompletedAtUtc.ShouldNotBeNull();
	}

	[Fact]
	public async Task ClearsTheClaimAndRecordsLastRunState_OnSuccess()
	{
		var handler = new StubHandler("tip-of-day", _ => JobExecutionResult.Ok());
		var job = CreateJob();
		job.ConsecutiveFailureCount = 3;
		SetDue(job);

		await CreateSut(handler).RunDueJobsAsync();

		job.RunState.ShouldBe(JobRunState.Idle);
		job.ClaimedAtUtc.ShouldBeNull();
		job.LastRunStatus.ShouldBe(JobRunStatus.Succeeded);
		job.LastRunUtc.ShouldBe(Now);
		job.LastRunError.ShouldBeNull();
		job.ConsecutiveFailureCount.ShouldBe(0);
		await _jobRepository.Received().UpdateAsync(job, Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task CollapsesMissedOccurrencesIntoOneCatchUpRun()
	{
		var handler = new StubHandler("tip-of-day", _ => JobExecutionResult.Ok());
		var job = CreateJob(cron: "0 12 * * *", nextRunUtc: Now.AddDays(-5));
		SetDue(job);

		await CreateSut(handler).RunDueJobsAsync();

		handler.CallCount.ShouldBe(1);
		job.NextRunUtc.ShouldBe(new DateTimeOffset(2026, 8, 21, 12, 0, 0, TimeSpan.Zero));
	}

	[Fact]
	public async Task SkipsAJobThatIsAlreadyRunning()
	{
		var handler = new StubHandler("tip-of-day", _ => JobExecutionResult.Ok());
		var job = CreateJob();
		job.RunState = JobRunState.Running;
		job.ClaimedAtUtc = Now.AddMinutes(-1);
		SetDue(job);

		var executed = await CreateSut(handler).RunDueJobsAsync();

		executed.ShouldBe(0);
		handler.CallCount.ShouldBe(0);
		_recordedRuns.ShouldBeEmpty();
	}

	[Fact]
	public async Task ReclaimsAJobWhoseClaimIsOlderThanTheTimeout()
	{
		var handler = new StubHandler("tip-of-day", _ => JobExecutionResult.Ok());
		var job = CreateJob();
		job.RunState = JobRunState.Running;
		job.ClaimedAtUtc = Now.AddHours(-2);
		SetDue(job);

		var executed = await CreateSut(handler).RunDueJobsAsync();

		executed.ShouldBe(1);
		handler.CallCount.ShouldBe(1);
	}

	[Fact]
	public async Task TreatsAMissingClaimTimestampAsStale()
	{
		var handler = new StubHandler("tip-of-day", _ => JobExecutionResult.Ok());
		var job = CreateJob();
		job.RunState = JobRunState.Running;
		job.ClaimedAtUtc = null;
		SetDue(job);

		var executed = await CreateSut(handler).RunDueJobsAsync();

		executed.ShouldBe(1);
	}

	[Fact]
	public async Task SkipsAJobWhoseClaimIsLostToAnotherWriter()
	{
		var handler = new StubHandler("tip-of-day", _ => JobExecutionResult.Ok());
		_jobRepository.TryClaimAsync(Arg.Any<ScheduledJobRecord>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
			.Returns(false);
		SetDue(CreateJob());

		var executed = await CreateSut(handler).RunDueJobsAsync();

		executed.ShouldBe(0);
		handler.CallCount.ShouldBe(0);
		_recordedRuns.ShouldBeEmpty();
	}

	[Fact]
	public async Task ContinuesToTheNextJob_GivenOneJobThrows()
	{
		var failing = new StubHandler("tip-of-day", _ => throw new InvalidOperationException("boom"));
		var succeeding = new StubHandler("nasa-apod", _ => JobExecutionResult.Ok());
		var second = CreateJob(name: "apod");
		second.JobType = "nasa-apod";
		SetDue(CreateJob(), second);

		await CreateSut(failing, succeeding).RunDueJobsAsync();

		succeeding.CallCount.ShouldBe(1);
		_recordedRuns.Count.ShouldBe(2);
	}

	[Fact]
	public async Task RecordsAFailedRun_GivenTheHandlerThrows()
	{
		var handler = new StubHandler("tip-of-day", _ => throw new InvalidOperationException("boom"));
		var job = CreateJob();
		SetDue(job);

		await CreateSut(handler).RunDueJobsAsync();

		var run = _recordedRuns.ShouldHaveSingleItem();
		run.Status.ShouldBe(JobRunStatus.Failed);
		run.ErrorMessage.ShouldContain("boom");
		job.ConsecutiveFailureCount.ShouldBe(1);
		job.LastRunStatus.ShouldBe(JobRunStatus.Failed);
		job.RunState.ShouldBe(JobRunState.Idle);
	}

	[Fact]
	public async Task ReschedulesEvenWhenTheRunFails()
	{
		var handler = new StubHandler("tip-of-day", _ => JobExecutionResult.Fail("nope"));
		var job = CreateJob();
		SetDue(job);

		await CreateSut(handler).RunDueJobsAsync();

		job.NextRunUtc.ShouldBe(new DateTimeOffset(2026, 8, 21, 12, 0, 0, TimeSpan.Zero));
	}

	[Fact]
	public async Task DisablesTheJob_GivenNoHandlerIsRegisteredForItsType()
	{
		var job = CreateJob();
		job.JobType = "gone-missing";
		SetDue(job);

		await CreateSut(new StubHandler("tip-of-day", _ => JobExecutionResult.Ok())).RunDueJobsAsync();

		job.IsEnabled.ShouldBeFalse();
		job.RunState.ShouldBe(JobRunState.Idle);
		_recordedRuns.ShouldHaveSingleItem().Status.ShouldBe(JobRunStatus.Failed);
	}

	[Fact]
	public async Task DisablesTheJob_GivenItsCronExpressionCannotBeParsed()
	{
		var handler = new StubHandler("tip-of-day", _ => JobExecutionResult.Ok());
		var job = CreateJob(cron: "not a cron");
		SetDue(job);

		await CreateSut(handler).RunDueJobsAsync();

		job.IsEnabled.ShouldBeFalse();
		handler.CallCount.ShouldBe(0);
		_recordedRuns.ShouldHaveSingleItem().ErrorMessage.ShouldContain("Cron");
	}

	[Fact]
	public async Task ReturnsZero_GivenNothingIsDue()
	{
		SetDue();

		var executed = await CreateSut(new StubHandler("tip-of-day", _ => JobExecutionResult.Ok()))
			.RunDueJobsAsync();

		executed.ShouldBe(0);
	}
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/BarretApi.Core.UnitTests --filter "FullyQualifiedName~JobDispatcher_RunDueJobsAsync_Tests"`
Expected: build failure — `JobDispatcher` does not exist.

- [ ] **Step 4: Write the dispatcher**

Create `src/BarretApi.Core/Services/JobDispatcher.cs`:

```csharp
using System.Diagnostics;
using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BarretApi.Core.Services;

/// <summary>
/// Selects due jobs, claims them, runs their handlers, records the outcome, and
/// reschedules. Takes <see cref="TimeProvider"/> so schedule behaviour is testable
/// without waiting on the wall clock.
/// </summary>
public sealed class JobDispatcher(
	IScheduledJobRepository jobRepository,
	IJobRunRepository runRepository,
	JobHandlerRegistry handlerRegistry,
	IOptions<JobSchedulerOptions> options,
	TimeProvider timeProvider,
	ILogger<JobDispatcher> logger)
{
	private readonly IScheduledJobRepository _jobRepository = jobRepository;
	private readonly IJobRunRepository _runRepository = runRepository;
	private readonly JobHandlerRegistry _handlerRegistry = handlerRegistry;
	private readonly JobSchedulerOptions _options = options.Value;
	private readonly TimeProvider _timeProvider = timeProvider;
	private readonly ILogger<JobDispatcher> _logger = logger;

	/// <summary>
	/// Runs every job that is due. Returns how many were executed.
	/// </summary>
	public async Task<int> RunDueJobsAsync(CancellationToken cancellationToken = default)
	{
		var now = _timeProvider.GetUtcNow();
		var dueJobs = await _jobRepository.GetDueAsync(now, cancellationToken);
		var executedCount = 0;

		foreach (var job in dueJobs)
		{
			cancellationToken.ThrowIfCancellationRequested();

			if (job.RunState == JobRunState.Running && !IsClaimStale(job, now))
			{
				_logger.LogInformation(
					"Skipping job {JobName}: a run claimed at {ClaimedAt} is still in progress.",
					job.Name,
					job.ClaimedAtUtc);
				continue;
			}

			if (job.RunState == JobRunState.Running)
			{
				_logger.LogWarning(
					"Reclaiming job {JobName}: its claim from {ClaimedAt} is older than the {TimeoutMinutes} minute timeout.",
					job.Name,
					job.ClaimedAtUtc,
					_options.ClaimTimeoutMinutes);
			}

			var scheduledForUtc = job.NextRunUtc;

			if (!await _jobRepository.TryClaimAsync(job, _timeProvider.GetUtcNow(), cancellationToken))
			{
				_logger.LogInformation("Skipping job {JobName}: the claim was taken by another writer.", job.Name);
				continue;
			}

			executedCount++;
			await ExecuteClaimedJobAsync(job, JobTriggerType.Scheduled, scheduledForUtc, cancellationToken);
		}

		return executedCount;
	}

	private async Task<JobRunRecord> ExecuteClaimedJobAsync(
		ScheduledJobRecord job,
		JobTriggerType triggerType,
		DateTimeOffset? scheduledForUtc,
		CancellationToken cancellationToken)
	{
		var startedAt = _timeProvider.GetUtcNow();
		var runId = $"run-{startedAt:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6]}";
		var stopwatch = Stopwatch.StartNew();

		var run = new JobRunRecord
		{
			RunId = runId,
			JobName = job.Name,
			JobType = job.JobType,
			TriggerType = triggerType,
			ScheduledForUtc = scheduledForUtc,
			StartedAtUtc = startedAt,
			Status = JobRunStatus.Succeeded,
			AttemptCount = 0
		};

		var fatalConfigurationError = ValidateJobConfiguration(job);
		if (fatalConfigurationError is not null)
		{
			stopwatch.Stop();
			job.IsEnabled = false;
			await CompleteRunAsync(job, run, JobRunStatus.Failed, null, fatalConfigurationError, stopwatch, cancellationToken);
			_logger.LogError(
				"Job {JobName} has been disabled because it is misconfigured: {Error}",
				job.Name,
				fatalConfigurationError);
			return run;
		}

		var context = new JobExecutionContext(
			job.Name,
			job.JobType,
			runId,
			scheduledForUtc,
			job.ArgumentsJson);

		var handler = _handlerRegistry.Resolve(job.JobType);

		run.AttemptCount = 1;
		JobExecutionResult result;
		try
		{
			result = await handler.ExecuteAsync(context, cancellationToken);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Job {JobName} threw during execution.", job.Name);
			result = JobExecutionResult.Fail(ex.Message);
		}

		stopwatch.Stop();

		var status = result.Success ? JobRunStatus.Succeeded : JobRunStatus.Failed;
		await CompleteRunAsync(job, run, status, result.Summary, result.ErrorMessage, stopwatch, cancellationToken);
		return run;
	}

	/// <summary>
	/// A claim older than the configured timeout is assumed to belong to a process that
	/// died mid-run. A running job with no claim timestamp is stale by definition.
	/// </summary>
	private bool IsClaimStale(ScheduledJobRecord job, DateTimeOffset nowUtc)
	{
		if (job.ClaimedAtUtc is null)
		{
			return true;
		}

		return nowUtc - job.ClaimedAtUtc.Value >= TimeSpan.FromMinutes(_options.ClaimTimeoutMinutes);
	}

	/// <summary>
	/// Returns a message when the job cannot be run at all — an unknown handler or an
	/// unparseable schedule. Both disable the job rather than looping on every tick.
	/// </summary>
	private string? ValidateJobConfiguration(ScheduledJobRecord job)
	{
		if (!_handlerRegistry.IsRegistered(job.JobType))
		{
			return $"No job handler is registered for job type '{job.JobType}'.";
		}

		if (!CronSchedule.TryParse(job.CronExpression, job.TimeZoneId, out _, out var cronError))
		{
			return cronError;
		}

		return null;
	}

	private async Task CompleteRunAsync(
		ScheduledJobRecord job,
		JobRunRecord run,
		JobRunStatus status,
		string? summary,
		string? errorMessage,
		Stopwatch stopwatch,
		CancellationToken cancellationToken)
	{
		var completedAt = _timeProvider.GetUtcNow();

		run.Status = status;
		run.Summary = summary;
		run.ErrorMessage = errorMessage;
		run.CompletedAtUtc = completedAt;
		run.DurationMs = stopwatch.ElapsedMilliseconds;

		job.RunState = JobRunState.Idle;
		job.ClaimedAtUtc = null;
		job.LastRunUtc = completedAt;
		job.LastRunStatus = status;
		job.LastRunError = errorMessage;
		job.LastRunDurationMs = run.DurationMs;
		job.UpdatedAtUtc = completedAt;

		if (status == JobRunStatus.Succeeded)
		{
			job.ConsecutiveFailureCount = 0;
		}
		else
		{
			job.ConsecutiveFailureCount++;
		}

		if (run.TriggerType == JobTriggerType.Scheduled)
		{
			job.NextRunUtc = ComputeNextRun(job, completedAt);
		}

		await _runRepository.AddAsync(run, cancellationToken);
		await _jobRepository.UpdateAsync(job, cancellationToken);
	}

	/// <summary>
	/// The next occurrence after <paramref name="fromUtc"/> — deliberately not after the
	/// slot that was missed, so any number of missed occurrences collapse into one catch-up run.
	/// </summary>
	private static DateTimeOffset? ComputeNextRun(ScheduledJobRecord job, DateTimeOffset fromUtc)
	{
		return CronSchedule.TryParse(job.CronExpression, job.TimeZoneId, out var schedule, out _)
			? schedule!.GetNextOccurrence(fromUtc)
			: null;
	}
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/BarretApi.Core.UnitTests --filter "FullyQualifiedName~JobDispatcher_RunDueJobsAsync_Tests"`
Expected: PASS, 15 tests.

- [ ] **Step 6: Format and commit**

```bash
dotnet format --include src/BarretApi.Core/Interfaces/IScheduledJobRepository.cs src/BarretApi.Core/Interfaces/IJobRunRepository.cs src/BarretApi.Core/Services/JobDispatcher.cs tests/BarretApi.Core.UnitTests/Services/JobDispatcher_RunDueJobsAsync_Tests.cs
git add src/BarretApi.Core/Interfaces/IScheduledJobRepository.cs src/BarretApi.Core/Interfaces/IJobRunRepository.cs src/BarretApi.Core/Services/JobDispatcher.cs tests/BarretApi.Core.UnitTests/Services/JobDispatcher_RunDueJobsAsync_Tests.cs
git commit -m "feat: add job dispatcher scheduled run path"
```

---

### Task 5: Retry with backoff, failure notification, and abort on shutdown

**Files:**
- Modify: `src/BarretApi.Core/Services/JobDispatcher.cs`
- Test: `tests/BarretApi.Core.UnitTests/Services/JobDispatcher_Retry_Tests.cs`

**Interfaces:**
- Consumes: everything from Task 4, plus the existing `IEmailNotificationService.SendPostFailureNotificationAsync(string postType, string errorDetails, IDictionary<string, string>? additionalContext = null, CancellationToken cancellationToken = default)`.
- Produces: `JobDispatcher`'s primary constructor gains a final optional parameter `IEmailNotificationService? emailNotificationService = null`. No new public methods.

Rate limiting is already applied inside `SmtpEmailNotificationService`, so the dispatcher calls the notification service directly and does not touch `IEmailRateLimiter`.

- [ ] **Step 1: Write the failing tests**

Create `tests/BarretApi.Core.UnitTests/Services/JobDispatcher_Retry_Tests.cs`:

```csharp
using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;

namespace BarretApi.Core.UnitTests.Services;

public sealed class JobDispatcher_Retry_Tests
{
	private static readonly DateTimeOffset Now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

	private readonly IScheduledJobRepository _jobRepository = Substitute.For<IScheduledJobRepository>();
	private readonly IJobRunRepository _runRepository = Substitute.For<IJobRunRepository>();
	private readonly IEmailNotificationService _emailNotificationService =
		Substitute.For<IEmailNotificationService>();
	private readonly FakeTimeProvider _timeProvider = new(Now);
	private readonly List<JobRunRecord> _recordedRuns = [];

	public JobDispatcher_Retry_Tests()
	{
		_jobRepository.TryClaimAsync(Arg.Any<ScheduledJobRecord>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
			.Returns(callInfo =>
			{
				var job = callInfo.Arg<ScheduledJobRecord>();
				job.RunState = JobRunState.Running;
				job.ClaimedAtUtc = callInfo.ArgAt<DateTimeOffset>(1);
				return true;
			});

		_runRepository.AddAsync(Arg.Any<JobRunRecord>(), Arg.Any<CancellationToken>())
			.Returns(callInfo =>
			{
				_recordedRuns.Add(callInfo.Arg<JobRunRecord>());
				return Task.CompletedTask;
			});
	}

	private sealed class SequenceHandler(params Func<JobExecutionResult>[] behaviors) : IScheduledJobHandler
	{
		private int _index;

		public string JobType => "tip-of-day";
		public int CallCount { get; private set; }

		public Task<JobExecutionResult> ExecuteAsync(
			JobExecutionContext context,
			CancellationToken cancellationToken = default)
		{
			CallCount++;
			var behavior = behaviors[Math.Min(_index++, behaviors.Length - 1)];
			return Task.FromResult(behavior());
		}
	}

	private sealed class CancellingHandler : IScheduledJobHandler
	{
		public string JobType => "tip-of-day";
		public int CallCount { get; private set; }

		public Task<JobExecutionResult> ExecuteAsync(
			JobExecutionContext context,
			CancellationToken cancellationToken = default)
		{
			CallCount++;
			throw new OperationCanceledException();
		}
	}

	private static ScheduledJobRecord CreateJob(int maxRetryCount)
		=> new()
		{
			Name = "daily-tip",
			DisplayName = "Daily tip",
			JobType = "tip-of-day",
			CronExpression = "0 12 * * *",
			TimeZoneId = "UTC",
			IsEnabled = true,
			NextRunUtc = Now.AddMinutes(-1),
			MaxRetryCount = maxRetryCount,
			RetryBaseDelaySeconds = 0,
			CreatedAtUtc = Now.AddDays(-1),
			UpdatedAtUtc = Now.AddDays(-1),
			ETag = "etag-1"
		};

	private JobDispatcher CreateSut(IScheduledJobHandler handler)
		=> new(
			_jobRepository,
			_runRepository,
			new JobHandlerRegistry([handler]),
			Options.Create(new JobSchedulerOptions
			{
				TableStorage = new JobSchedulerTableStorageOptions { ConnectionString = "UseDevelopmentStorage=true" }
			}),
			_timeProvider,
			NullLogger<JobDispatcher>.Instance,
			_emailNotificationService);

	private void SetDue(ScheduledJobRecord job)
		=> _jobRepository.GetDueAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
			.Returns([job]);

	[Fact]
	public async Task RetriesUntilTheHandlerSucceeds()
	{
		var handler = new SequenceHandler(
			() => JobExecutionResult.Fail("transient"),
			() => JobExecutionResult.Ok("recovered"));
		SetDue(CreateJob(maxRetryCount: 2));

		await CreateSut(handler).RunDueJobsAsync();

		handler.CallCount.ShouldBe(2);
	}

	[Fact]
	public async Task RecordsASingleSucceededRun_GivenASuccessfulRetry()
	{
		var handler = new SequenceHandler(
			() => JobExecutionResult.Fail("transient"),
			() => JobExecutionResult.Ok("recovered"));
		SetDue(CreateJob(maxRetryCount: 2));

		await CreateSut(handler).RunDueJobsAsync();

		var run = _recordedRuns.ShouldHaveSingleItem();
		run.Status.ShouldBe(JobRunStatus.Succeeded);
		run.AttemptCount.ShouldBe(2);
		run.ErrorMessage.ShouldBeNull();
	}

	[Fact]
	public async Task StopsAfterMaxRetryCountRetries()
	{
		var handler = new SequenceHandler(() => JobExecutionResult.Fail("always"));
		SetDue(CreateJob(maxRetryCount: 2));

		await CreateSut(handler).RunDueJobsAsync();

		handler.CallCount.ShouldBe(3);
		_recordedRuns.ShouldHaveSingleItem().AttemptCount.ShouldBe(3);
	}

	[Fact]
	public async Task DoesNotRetry_GivenMaxRetryCountIsZero()
	{
		var handler = new SequenceHandler(() => JobExecutionResult.Fail("always"));
		SetDue(CreateJob(maxRetryCount: 0));

		await CreateSut(handler).RunDueJobsAsync();

		handler.CallCount.ShouldBe(1);
	}

	[Fact]
	public async Task RetriesWhenTheHandlerThrows_NotOnlyWhenItReturnsFailure()
	{
		var handler = new SequenceHandler(
			() => throw new InvalidOperationException("boom"),
			() => JobExecutionResult.Ok());
		SetDue(CreateJob(maxRetryCount: 1));

		await CreateSut(handler).RunDueJobsAsync();

		handler.CallCount.ShouldBe(2);
		_recordedRuns.ShouldHaveSingleItem().Status.ShouldBe(JobRunStatus.Succeeded);
	}

	[Fact]
	public async Task SendsOneNotification_GivenEveryAttemptFails()
	{
		var handler = new SequenceHandler(() => JobExecutionResult.Fail("always"));
		SetDue(CreateJob(maxRetryCount: 2));

		await CreateSut(handler).RunDueJobsAsync();

		await _emailNotificationService.Received(1).SendPostFailureNotificationAsync(
			"job:daily-tip",
			Arg.Is<string>(details => details.Contains("always")),
			Arg.Any<IDictionary<string, string>>(),
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task DoesNotNotify_GivenARetrySucceeds()
	{
		var handler = new SequenceHandler(
			() => JobExecutionResult.Fail("transient"),
			() => JobExecutionResult.Ok());
		SetDue(CreateJob(maxRetryCount: 2));

		await CreateSut(handler).RunDueJobsAsync();

		await _emailNotificationService.DidNotReceiveWithAnyArgs().SendPostFailureNotificationAsync(
			default!, default!, default, default);
	}

	[Fact]
	public async Task StillRecordsTheFailedRun_GivenNotificationThrows()
	{
		_emailNotificationService.SendPostFailureNotificationAsync(
				Arg.Any<string>(),
				Arg.Any<string>(),
				Arg.Any<IDictionary<string, string>>(),
				Arg.Any<CancellationToken>())
			.Returns(Task.FromException(new InvalidOperationException("smtp down")));
		var handler = new SequenceHandler(() => JobExecutionResult.Fail("always"));
		SetDue(CreateJob(maxRetryCount: 0));

		await CreateSut(handler).RunDueJobsAsync();

		_recordedRuns.ShouldHaveSingleItem().Status.ShouldBe(JobRunStatus.Failed);
	}

	[Fact]
	public async Task RecordsAnAbortedRun_GivenExecutionIsCancelled()
	{
		var handler = new CancellingHandler();
		var job = CreateJob(maxRetryCount: 2);
		SetDue(job);

		await CreateSut(handler).RunDueJobsAsync();

		handler.CallCount.ShouldBe(1);
		_recordedRuns.ShouldHaveSingleItem().Status.ShouldBe(JobRunStatus.Aborted);
		job.RunState.ShouldBe(JobRunState.Idle);
	}

	[Fact]
	public async Task LeavesNextRunInThePast_GivenExecutionIsCancelled()
	{
		var job = CreateJob(maxRetryCount: 0);
		var originalNextRun = job.NextRunUtc;
		SetDue(job);

		await CreateSut(new CancellingHandler()).RunDueJobsAsync();

		job.NextRunUtc.ShouldBe(originalNextRun);
	}

	[Fact]
	public async Task DoesNotNotify_GivenExecutionIsCancelled()
	{
		SetDue(CreateJob(maxRetryCount: 0));

		await CreateSut(new CancellingHandler()).RunDueJobsAsync();

		await _emailNotificationService.DidNotReceiveWithAnyArgs().SendPostFailureNotificationAsync(
			default!, default!, default, default);
	}
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/BarretApi.Core.UnitTests --filter "FullyQualifiedName~JobDispatcher_Retry_Tests"`
Expected: build failure — `JobDispatcher` has no seven-argument constructor.

- [ ] **Step 3: Add the notification dependency**

In `src/BarretApi.Core/Services/JobDispatcher.cs`, add the parameter to the primary constructor after `logger`:

```csharp
	ILogger<JobDispatcher> logger,
	IEmailNotificationService? emailNotificationService = null)
```

and the backing field after `_logger`:

```csharp
	private readonly IEmailNotificationService? _emailNotificationService = emailNotificationService;
```

- [ ] **Step 4: Replace the single attempt with a retry loop**

In `ExecuteClaimedJobAsync`, replace this block:

```csharp
		run.AttemptCount = 1;
		JobExecutionResult result;
		try
		{
			result = await handler.ExecuteAsync(context, cancellationToken);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Job {JobName} threw during execution.", job.Name);
			result = JobExecutionResult.Fail(ex.Message);
		}

		stopwatch.Stop();

		var status = result.Success ? JobRunStatus.Succeeded : JobRunStatus.Failed;
		await CompleteRunAsync(job, run, status, result.Summary, result.ErrorMessage, stopwatch, cancellationToken);
		return run;
```

with:

```csharp
		var maxAttempts = Math.Max(0, job.MaxRetryCount) + 1;
		JobExecutionResult? result = null;

		for (var attempt = 1; attempt <= maxAttempts; attempt++)
		{
			run.AttemptCount = attempt;

			try
			{
				result = await handler.ExecuteAsync(context, cancellationToken);
			}
			catch (OperationCanceledException)
			{
				stopwatch.Stop();
				_logger.LogWarning("Job {JobName} was cancelled during execution.", job.Name);
				await CompleteRunAsync(
					job,
					run,
					JobRunStatus.Aborted,
					null,
					"Execution was cancelled before it completed.",
					stopwatch,
					CancellationToken.None);
				return run;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Job {JobName} threw on attempt {Attempt}.", job.Name, attempt);
				result = JobExecutionResult.Fail(ex.Message);
			}

			if (result.Success)
			{
				break;
			}

			if (attempt < maxAttempts)
			{
				await DelayBeforeRetryAsync(job, attempt, cancellationToken);
			}
		}

		stopwatch.Stop();

		var status = result!.Success ? JobRunStatus.Succeeded : JobRunStatus.Failed;
		await CompleteRunAsync(job, run, status, result.Summary, result.ErrorMessage, stopwatch, cancellationToken);

		if (status == JobRunStatus.Failed)
		{
			await NotifyFailureAsync(job, run, cancellationToken);
		}

		return run;
```

- [ ] **Step 5: Add the backoff and notification helpers**

Add these private methods to `JobDispatcher`, after `CompleteRunAsync`:

```csharp
	/// <summary>
	/// Exponential backoff between attempts. A base delay of zero disables waiting,
	/// which is what keeps the dispatcher tests fast.
	/// </summary>
	private async Task DelayBeforeRetryAsync(
		ScheduledJobRecord job,
		int attempt,
		CancellationToken cancellationToken)
	{
		if (job.RetryBaseDelaySeconds <= 0)
		{
			return;
		}

		var seconds = job.RetryBaseDelaySeconds * Math.Pow(2, attempt - 1);
		var delay = TimeSpan.FromSeconds(Math.Min(seconds, TimeSpan.FromMinutes(15).TotalSeconds));

		_logger.LogInformation(
			"Retrying job {JobName} in {DelaySeconds}s after attempt {Attempt}.",
			job.Name,
			delay.TotalSeconds,
			attempt);

		await Task.Delay(delay, _timeProvider, cancellationToken);
	}

	/// <summary>
	/// Sent only once every attempt has failed. Rate limiting lives inside the
	/// notification service, so a permanently broken job cannot flood the inbox.
	/// </summary>
	private async Task NotifyFailureAsync(
		ScheduledJobRecord job,
		JobRunRecord run,
		CancellationToken cancellationToken)
	{
		if (_emailNotificationService is null)
		{
			return;
		}

		var context = new Dictionary<string, string>
		{
			["JobName"] = job.Name,
			["JobType"] = job.JobType,
			["RunId"] = run.RunId,
			["Attempts"] = run.AttemptCount.ToString(),
			["ConsecutiveFailures"] = job.ConsecutiveFailureCount.ToString(),
			["NextRunUtc"] = job.NextRunUtc?.ToString("O") ?? "none"
		};

		try
		{
			await _emailNotificationService.SendPostFailureNotificationAsync(
				$"job:{job.Name}",
				run.ErrorMessage ?? "The job failed without reporting an error message.",
				context,
				cancellationToken);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to send a failure notification for job {JobName}.", job.Name);
		}
	}
```

- [ ] **Step 6: Skip rescheduling for aborted runs**

In `CompleteRunAsync`, replace:

```csharp
		if (run.TriggerType == JobTriggerType.Scheduled)
```

with:

```csharp
		if (run.TriggerType == JobTriggerType.Scheduled && status != JobRunStatus.Aborted)
```

An aborted run leaves `NextRunUtc` in the past so the next startup catches it up (FR-016).

- [ ] **Step 7: Run both dispatcher test classes**

Run: `dotnet test tests/BarretApi.Core.UnitTests --filter "FullyQualifiedName~JobDispatcher"`
Expected: PASS — 15 from Task 4 plus 11 new, 26 total. Task 4's tests must still pass; they set `MaxRetryCount = 0`, so a single attempt is still a single attempt.

- [ ] **Step 8: Format and commit**

```bash
dotnet format --include src/BarretApi.Core/Services/JobDispatcher.cs tests/BarretApi.Core.UnitTests/Services/JobDispatcher_Retry_Tests.cs
git add src/BarretApi.Core/Services/JobDispatcher.cs tests/BarretApi.Core.UnitTests/Services/JobDispatcher_Retry_Tests.cs
git commit -m "feat: add job retry backoff and failure notification"
```

---

### Task 6: Manual runs and stale-claim recovery

**Files:**
- Create: `src/BarretApi.Core/Models/ManualRunResult.cs`
- Modify: `src/BarretApi.Core/Services/JobDispatcher.cs`
- Test: `tests/BarretApi.Core.UnitTests/Services/JobDispatcher_RunManuallyAsync_Tests.cs`

**Interfaces:**
- Consumes: everything from Tasks 4 and 5, plus `JobSchedulerOptions.ClaimTimeoutMinutes` (Task 1).
- Produces:
  - `enum ManualRunOutcome { Completed, NotFound, Busy }`.
  - `sealed record ManualRunResult(ManualRunOutcome Outcome, JobRunRecord? Run)`.
  - `JobDispatcher.RunManuallyAsync(string jobName, CancellationToken cancellationToken = default)` returning `Task<ManualRunResult>`.

- [ ] **Step 1: Write the failing tests**

Create `tests/BarretApi.Core.UnitTests/Services/JobDispatcher_RunManuallyAsync_Tests.cs`:

```csharp
using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;

namespace BarretApi.Core.UnitTests.Services;

public sealed class JobDispatcher_RunManuallyAsync_Tests
{
	private static readonly DateTimeOffset Now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

	private readonly IScheduledJobRepository _jobRepository = Substitute.For<IScheduledJobRepository>();
	private readonly IJobRunRepository _runRepository = Substitute.For<IJobRunRepository>();
	private readonly FakeTimeProvider _timeProvider = new(Now);
	private readonly List<JobRunRecord> _recordedRuns = [];

	public JobDispatcher_RunManuallyAsync_Tests()
	{
		_jobRepository.TryClaimAsync(Arg.Any<ScheduledJobRecord>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
			.Returns(callInfo =>
			{
				var job = callInfo.Arg<ScheduledJobRecord>();
				job.RunState = JobRunState.Running;
				job.ClaimedAtUtc = callInfo.ArgAt<DateTimeOffset>(1);
				return true;
			});

		_runRepository.AddAsync(Arg.Any<JobRunRecord>(), Arg.Any<CancellationToken>())
			.Returns(callInfo =>
			{
				_recordedRuns.Add(callInfo.Arg<JobRunRecord>());
				return Task.CompletedTask;
			});
	}

	private sealed class StubHandler : IScheduledJobHandler
	{
		public string JobType => "tip-of-day";
		public int CallCount { get; private set; }

		public Task<JobExecutionResult> ExecuteAsync(
			JobExecutionContext context,
			CancellationToken cancellationToken = default)
		{
			CallCount++;
			return Task.FromResult(JobExecutionResult.Ok("posted"));
		}
	}

	private static ScheduledJobRecord CreateJob()
		=> new()
		{
			Name = "daily-tip",
			DisplayName = "Daily tip",
			JobType = "tip-of-day",
			CronExpression = "0 12 * * *",
			TimeZoneId = "UTC",
			IsEnabled = true,
			NextRunUtc = Now.AddHours(6),
			MaxRetryCount = 0,
			RetryBaseDelaySeconds = 0,
			CreatedAtUtc = Now.AddDays(-1),
			UpdatedAtUtc = Now.AddDays(-1),
			ETag = "etag-1"
		};

	private JobDispatcher CreateSut(IScheduledJobHandler handler, int claimTimeoutMinutes = 30)
		=> new(
			_jobRepository,
			_runRepository,
			new JobHandlerRegistry([handler]),
			Options.Create(new JobSchedulerOptions
			{
				ClaimTimeoutMinutes = claimTimeoutMinutes,
				TableStorage = new JobSchedulerTableStorageOptions { ConnectionString = "UseDevelopmentStorage=true" }
			}),
			_timeProvider,
			NullLogger<JobDispatcher>.Instance);

	[Fact]
	public async Task ReturnsNotFound_GivenNoSuchJob()
	{
		_jobRepository.GetByNameAsync("missing", Arg.Any<CancellationToken>())
			.Returns((ScheduledJobRecord?)null);

		var result = await CreateSut(new StubHandler()).RunManuallyAsync("missing");

		result.Outcome.ShouldBe(ManualRunOutcome.NotFound);
		result.Run.ShouldBeNull();
	}

	[Fact]
	public async Task RunsTheHandler_AndRecordsAManualRun()
	{
		var handler = new StubHandler();
		var job = CreateJob();
		_jobRepository.GetByNameAsync("daily-tip", Arg.Any<CancellationToken>()).Returns(job);

		var result = await CreateSut(handler).RunManuallyAsync("daily-tip");

		result.Outcome.ShouldBe(ManualRunOutcome.Completed);
		handler.CallCount.ShouldBe(1);
		var run = _recordedRuns.ShouldHaveSingleItem();
		run.TriggerType.ShouldBe(JobTriggerType.Manual);
		run.ScheduledForUtc.ShouldBeNull();
		run.Status.ShouldBe(JobRunStatus.Succeeded);
	}

	[Fact]
	public async Task LeavesNextRunUnchanged()
	{
		var job = CreateJob();
		var originalNextRun = job.NextRunUtc;
		_jobRepository.GetByNameAsync("daily-tip", Arg.Any<CancellationToken>()).Returns(job);

		await CreateSut(new StubHandler()).RunManuallyAsync("daily-tip");

		job.NextRunUtc.ShouldBe(originalNextRun);
	}

	[Fact]
	public async Task RunsADisabledJob()
	{
		var handler = new StubHandler();
		var job = CreateJob();
		job.IsEnabled = false;
		_jobRepository.GetByNameAsync("daily-tip", Arg.Any<CancellationToken>()).Returns(job);

		var result = await CreateSut(handler).RunManuallyAsync("daily-tip");

		result.Outcome.ShouldBe(ManualRunOutcome.Completed);
		handler.CallCount.ShouldBe(1);
		job.IsEnabled.ShouldBeFalse();
	}

	[Fact]
	public async Task ReturnsBusy_GivenAFreshClaimIsInProgress()
	{
		var handler = new StubHandler();
		var job = CreateJob();
		job.RunState = JobRunState.Running;
		job.ClaimedAtUtc = Now.AddMinutes(-5);
		_jobRepository.GetByNameAsync("daily-tip", Arg.Any<CancellationToken>()).Returns(job);

		var result = await CreateSut(handler).RunManuallyAsync("daily-tip");

		result.Outcome.ShouldBe(ManualRunOutcome.Busy);
		handler.CallCount.ShouldBe(0);
	}

	[Fact]
	public async Task ReturnsBusy_GivenTheClaimIsLostToAnotherWriter()
	{
		_jobRepository.TryClaimAsync(Arg.Any<ScheduledJobRecord>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
			.Returns(false);
		_jobRepository.GetByNameAsync("daily-tip", Arg.Any<CancellationToken>()).Returns(CreateJob());

		var result = await CreateSut(new StubHandler()).RunManuallyAsync("daily-tip");

		result.Outcome.ShouldBe(ManualRunOutcome.Busy);
	}

	[Fact]
	public async Task RunsAnyway_GivenTheExistingClaimIsStale()
	{
		var handler = new StubHandler();
		var job = CreateJob();
		job.RunState = JobRunState.Running;
		job.ClaimedAtUtc = Now.AddMinutes(-31);
		_jobRepository.GetByNameAsync("daily-tip", Arg.Any<CancellationToken>()).Returns(job);

		var result = await CreateSut(handler, claimTimeoutMinutes: 30).RunManuallyAsync("daily-tip");

		result.Outcome.ShouldBe(ManualRunOutcome.Completed);
		handler.CallCount.ShouldBe(1);
	}

}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/BarretApi.Core.UnitTests --filter "FullyQualifiedName~JobDispatcher_RunManuallyAsync_Tests"`
Expected: build failure — `ManualRunResult` does not exist.

- [ ] **Step 3: Add the result model**

Create `src/BarretApi.Core/Models/ManualRunResult.cs`:

```csharp
namespace BarretApi.Core.Models;

public enum ManualRunOutcome
{
	Completed = 0,
	NotFound = 1,
	Busy = 2
}

/// <summary>
/// The outcome of an out-of-band run request. <see cref="Run"/> is null unless
/// <see cref="Outcome"/> is <see cref="ManualRunOutcome.Completed"/>.
/// </summary>
public sealed record ManualRunResult(ManualRunOutcome Outcome, JobRunRecord? Run);
```

- [ ] **Step 4: Add `RunManuallyAsync`**

`IsClaimStale` already exists — Task 4 added it for the scheduled path. Manual runs reuse it unchanged.

Add this public method to `JobDispatcher` after `RunDueJobsAsync`:

```csharp
	/// <summary>
	/// Runs a job out of band. Does not change the job's next run time, and works on a
	/// disabled job. Returns <see cref="ManualRunOutcome.Busy"/> rather than starting a
	/// second concurrent execution.
	/// </summary>
	public async Task<ManualRunResult> RunManuallyAsync(
		string jobName,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(jobName);

		var job = await _jobRepository.GetByNameAsync(jobName, cancellationToken);
		if (job is null)
		{
			return new ManualRunResult(ManualRunOutcome.NotFound, null);
		}

		var now = _timeProvider.GetUtcNow();
		if (job.RunState == JobRunState.Running && !IsClaimStale(job, now))
		{
			return new ManualRunResult(ManualRunOutcome.Busy, null);
		}

		if (!await _jobRepository.TryClaimAsync(job, now, cancellationToken))
		{
			return new ManualRunResult(ManualRunOutcome.Busy, null);
		}

		var run = await ExecuteClaimedJobAsync(job, JobTriggerType.Manual, null, cancellationToken);
		return new ManualRunResult(ManualRunOutcome.Completed, run);
	}
```

- [ ] **Step 5: Run every dispatcher test**

Run: `dotnet test tests/BarretApi.Core.UnitTests --filter "FullyQualifiedName~JobDispatcher"`
Expected: PASS — 15 from Task 4, 11 from Task 5, and 7 from this task: 33 tests.

- [ ] **Step 6: Format and commit**

```bash
dotnet format --include src/BarretApi.Core/Models/ManualRunResult.cs src/BarretApi.Core/Services/JobDispatcher.cs tests/BarretApi.Core.UnitTests/Services/JobDispatcher_RunManuallyAsync_Tests.cs
git add src/BarretApi.Core/Models/ManualRunResult.cs src/BarretApi.Core/Services/JobDispatcher.cs tests/BarretApi.Core.UnitTests/Services/JobDispatcher_RunManuallyAsync_Tests.cs
git commit -m "feat: add manual job runs and stale claim recovery"
```

---

### Task 7: `AzureTableScheduledJobRepository`

**Files:**
- Create: `src/BarretApi.Infrastructure/Services/AzureTableScheduledJobRepository.cs`
- Test: `tests/BarretApi.Infrastructure.UnitTests/Services/AzureTableScheduledJobRepository_Tests.cs`

**Interfaces:**
- Consumes: `IScheduledJobRepository` (Task 4), `ScheduledJobRecord`, `JobRunState`, `JobRunStatus` (Task 2), `JobSchedulerOptions` (Task 1).
- Produces: `AzureTableScheduledJobRepository`, with a public `(IOptions<JobSchedulerOptions>, ILogger<AzureTableScheduledJobRepository>)` constructor and an `internal (TableClient, IOptions<JobSchedulerOptions>, ILogger<AzureTableScheduledJobRepository>)` constructor used by tests.

Due-job selection queries the partition and filters in memory. The table holds a handful of rows, and this avoids hand-writing OData datetime literals — the same approach `AzureTableTipOfDayRepository` takes.

- [ ] **Step 1: Write the failing tests**

Create `tests/BarretApi.Infrastructure.UnitTests/Services/AzureTableScheduledJobRepository_Tests.cs`:

```csharp
using Azure;
using Azure.Data.Tables;
using BarretApi.Core.Configuration;
using BarretApi.Core.Models;
using BarretApi.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace BarretApi.Infrastructure.UnitTests.Services;

public sealed class AzureTableScheduledJobRepository_Tests
{
	private const string PartitionKey = "scheduled-job";
	private static readonly DateTimeOffset Now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

	private readonly TableClient _tableClient = Substitute.For<TableClient>();

	private AzureTableScheduledJobRepository CreateSut()
		=> new(
			_tableClient,
			Options.Create(new JobSchedulerOptions
			{
				TableStorage = new JobSchedulerTableStorageOptions
				{
					ConnectionString = "UseDevelopmentStorage=true",
					PartitionKey = PartitionKey
				}
			}),
			NullLogger<AzureTableScheduledJobRepository>.Instance);

	private static ScheduledJobRecord CreateJob(string name = "daily-tip")
		=> new()
		{
			Name = name,
			DisplayName = "Daily tip",
			JobType = "tip-of-day",
			CronExpression = "0 12 * * *",
			TimeZoneId = "America/Chicago",
			ArgumentsJson = """{"category":"dotnet"}""",
			IsEnabled = true,
			NextRunUtc = Now.AddHours(1),
			MaxRetryCount = 2,
			RetryBaseDelaySeconds = 30,
			CreatedAtUtc = Now,
			UpdatedAtUtc = Now,
			ETag = "etag-1"
		};

	private static TableEntity CreateEntity(
		string name,
		bool isEnabled = true,
		DateTimeOffset? nextRunUtc = null,
		string runState = "Idle")
		=> new(PartitionKey, name)
		{
			["DisplayName"] = "Daily tip",
			["JobType"] = "tip-of-day",
			["CronExpression"] = "0 12 * * *",
			["TimeZoneId"] = "America/Chicago",
			["ArgumentsJson"] = """{"category":"dotnet"}""",
			["IsEnabled"] = isEnabled,
			["NextRunUtc"] = nextRunUtc ?? Now.AddHours(1),
			["ConsecutiveFailureCount"] = 0,
			["MaxRetryCount"] = 2,
			["RetryBaseDelaySeconds"] = 30,
			["RunState"] = runState,
			["CreatedAtUtc"] = Now,
			["UpdatedAtUtc"] = Now
		};

	private void SetQueryResult(params TableEntity[] entities)
	{
		var pageable = Substitute.For<AsyncPageable<TableEntity>>();
		pageable.GetAsyncEnumerator(Arg.Any<CancellationToken>())
			.Returns(AsyncEnumeratorOf(entities));
		_tableClient.QueryAsync<TableEntity>(
				Arg.Any<string>(),
				cancellationToken: Arg.Any<CancellationToken>())
			.Returns(pageable);
	}

	private static async IAsyncEnumerator<T> AsyncEnumeratorOf<T>(params T[] items)
	{
		foreach (var item in items)
		{
			yield return item;
		}

		await Task.CompletedTask;
	}

	[Fact]
	public async Task GetAllAsync_MapsEveryStoredField()
	{
		SetQueryResult(CreateEntity("daily-tip"));

		var jobs = await CreateSut().GetAllAsync();

		var job = jobs.ShouldHaveSingleItem();
		job.Name.ShouldBe("daily-tip");
		job.JobType.ShouldBe("tip-of-day");
		job.CronExpression.ShouldBe("0 12 * * *");
		job.TimeZoneId.ShouldBe("America/Chicago");
		job.ArgumentsJson.ShouldBe("""{"category":"dotnet"}""");
		job.IsEnabled.ShouldBeTrue();
		job.MaxRetryCount.ShouldBe(2);
		job.RetryBaseDelaySeconds.ShouldBe(30);
		job.RunState.ShouldBe(JobRunState.Idle);
	}

	[Fact]
	public async Task GetDueAsync_ReturnsEnabledJobsWhoseNextRunHasPassed()
	{
		SetQueryResult(
			CreateEntity("due", nextRunUtc: Now.AddMinutes(-1)),
			CreateEntity("not-yet", nextRunUtc: Now.AddMinutes(5)));

		var due = await CreateSut().GetDueAsync(Now);

		due.ShouldHaveSingleItem().Name.ShouldBe("due");
	}

	[Fact]
	public async Task GetDueAsync_ExcludesDisabledJobs()
	{
		SetQueryResult(CreateEntity("paused", isEnabled: false, nextRunUtc: Now.AddMinutes(-1)));

		var due = await CreateSut().GetDueAsync(Now);

		due.ShouldBeEmpty();
	}

	[Fact]
	public async Task GetDueAsync_ExcludesJobsWithNoNextRunTime()
	{
		var entity = CreateEntity("never");
		entity["NextRunUtc"] = null;
		SetQueryResult(entity);

		var due = await CreateSut().GetDueAsync(Now);

		due.ShouldBeEmpty();
	}

	[Fact]
	public async Task GetDueAsync_IncludesRunningJobsSoTheCallerCanJudgeStaleness()
	{
		SetQueryResult(CreateEntity("busy", nextRunUtc: Now.AddMinutes(-1), runState: "Running"));

		var due = await CreateSut().GetDueAsync(Now);

		due.ShouldHaveSingleItem().RunState.ShouldBe(JobRunState.Running);
	}

	[Fact]
	public async Task GetByNameAsync_ReturnsNull_GivenNoSuchJob()
	{
		_tableClient.GetEntityIfExistsAsync<TableEntity>(
				PartitionKey,
				"missing",
				cancellationToken: Arg.Any<CancellationToken>())
			.Returns(Substitute.For<NullableResponse<TableEntity>>());

		var job = await CreateSut().GetByNameAsync("missing");

		job.ShouldBeNull();
	}

	[Fact]
	public async Task CreateAsync_WritesTheEntity()
	{
		await CreateSut().CreateAsync(CreateJob());

		await _tableClient.Received(1).AddEntityAsync(
			Arg.Is<TableEntity>(e => e.RowKey == "daily-tip" && e.PartitionKey == PartitionKey),
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task TryClaimAsync_MarksTheJobRunning_GivenTheUpdateSucceeds()
	{
		var job = CreateJob();

		var claimed = await CreateSut().TryClaimAsync(job, Now);

		claimed.ShouldBeTrue();
		job.RunState.ShouldBe(JobRunState.Running);
		job.ClaimedAtUtc.ShouldBe(Now);
	}

	[Fact]
	public async Task TryClaimAsync_UsesTheRecordsETagForConcurrency()
	{
		await CreateSut().TryClaimAsync(CreateJob(), Now);

		await _tableClient.Received(1).UpdateEntityAsync(
			Arg.Any<TableEntity>(),
			new ETag("etag-1"),
			TableUpdateMode.Replace,
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task TryClaimAsync_ReturnsFalse_GivenAPreconditionFailure()
	{
		_tableClient.UpdateEntityAsync(
				Arg.Any<TableEntity>(),
				Arg.Any<ETag>(),
				Arg.Any<TableUpdateMode>(),
				Arg.Any<CancellationToken>())
			.ThrowsAsync(new RequestFailedException(412, "precondition failed"));
		var job = CreateJob();

		var claimed = await CreateSut().TryClaimAsync(job, Now);

		claimed.ShouldBeFalse();
		job.RunState.ShouldBe(JobRunState.Idle);
	}

	[Fact]
	public async Task TryClaimAsync_Rethrows_GivenAnUnrelatedFailure()
	{
		_tableClient.UpdateEntityAsync(
				Arg.Any<TableEntity>(),
				Arg.Any<ETag>(),
				Arg.Any<TableUpdateMode>(),
				Arg.Any<CancellationToken>())
			.ThrowsAsync(new RequestFailedException(503, "service unavailable"));

		await Should.ThrowAsync<RequestFailedException>(() => CreateSut().TryClaimAsync(CreateJob(), Now));
	}

	[Fact]
	public async Task UpdateAsync_ReplacesUnconditionally()
	{
		await CreateSut().UpdateAsync(CreateJob());

		await _tableClient.Received(1).UpdateEntityAsync(
			Arg.Any<TableEntity>(),
			ETag.All,
			TableUpdateMode.Replace,
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task DeleteAsync_DeletesByName()
	{
		await CreateSut().DeleteAsync("daily-tip");

		await _tableClient.Received(1).DeleteEntityAsync(
			PartitionKey,
			"daily-tip",
			Arg.Any<ETag>(),
			Arg.Any<CancellationToken>());
	}
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/BarretApi.Infrastructure.UnitTests --filter "FullyQualifiedName~AzureTableScheduledJobRepository_Tests"`
Expected: build failure — the repository does not exist. It will also fail because `BarretApi.Infrastructure.UnitTests` does not reference `BarretApi.Core`; fix that in the next step.

- [ ] **Step 3: Reference Core from the Infrastructure test project**

In `tests/BarretApi.Infrastructure.UnitTests/BarretApi.Infrastructure.UnitTests.csproj`, add to the `ProjectReference` item group:

```xml
		<ProjectReference Include="..\..\src\BarretApi.Core\BarretApi.Core.csproj" />
```

(If it is already present because `BarretApi.Infrastructure` flows it transitively, leave the file alone.)

- [ ] **Step 4: Write the repository**

Create `src/BarretApi.Infrastructure/Services/AzureTableScheduledJobRepository.cs`:

```csharp
using Azure;
using Azure.Data.Tables;
using Azure.Identity;
using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BarretApi.Infrastructure.Services;

public sealed class AzureTableScheduledJobRepository : IScheduledJobRepository
{
	private readonly TableClient _tableClient;
	private readonly JobSchedulerOptions _options;
	private readonly ILogger<AzureTableScheduledJobRepository> _logger;
	private readonly SemaphoreSlim _initializationLock = new(1, 1);
	private bool _initialized;

	public AzureTableScheduledJobRepository(
		IOptions<JobSchedulerOptions> options,
		ILogger<AzureTableScheduledJobRepository> logger)
	{
		_options = options.Value;
		_logger = logger;
		_options.ThrowIfInvalid();

		var tableName = _options.TableStorage.JobsTableName.Trim().ToLowerInvariant();

		_tableClient = !string.IsNullOrWhiteSpace(_options.TableStorage.ConnectionString)
			? new TableClient(_options.TableStorage.ConnectionString, tableName)
			: new TableClient(
				new Uri(_options.TableStorage.AccountEndpoint),
				tableName,
				new DefaultAzureCredential());
	}

	internal AzureTableScheduledJobRepository(
		TableClient tableClient,
		IOptions<JobSchedulerOptions> options,
		ILogger<AzureTableScheduledJobRepository> logger)
	{
		_tableClient = tableClient;
		_options = options.Value;
		_logger = logger;
		_initialized = true;
	}

	private string PartitionKey => _options.TableStorage.PartitionKey;

	public async Task<IReadOnlyList<ScheduledJobRecord>> GetAllAsync(
		CancellationToken cancellationToken = default)
	{
		await EnsureInitializedAsync(cancellationToken);

		var jobs = new List<ScheduledJobRecord>();
		var filter = $"PartitionKey eq '{EscapeODataString(PartitionKey)}'";

		await foreach (var entity in _tableClient.QueryAsync<TableEntity>(
			filter,
			cancellationToken: cancellationToken))
		{
			jobs.Add(MapEntityToModel(entity));
		}

		return [.. jobs.OrderBy(j => j.Name, StringComparer.OrdinalIgnoreCase)];
	}

	public async Task<ScheduledJobRecord?> GetByNameAsync(
		string name,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		await EnsureInitializedAsync(cancellationToken);

		var response = await _tableClient.GetEntityIfExistsAsync<TableEntity>(
			PartitionKey,
			name.Trim(),
			cancellationToken: cancellationToken);

		return response.HasValue && response.Value is not null
			? MapEntityToModel(response.Value)
			: null;
	}

	/// <summary>
	/// The partition holds a handful of rows, so it is scanned and filtered in memory
	/// rather than encoding a datetime comparison into an OData filter.
	/// </summary>
	public async Task<IReadOnlyList<ScheduledJobRecord>> GetDueAsync(
		DateTimeOffset nowUtc,
		CancellationToken cancellationToken = default)
	{
		var all = await GetAllAsync(cancellationToken);

		return [.. all
			.Where(j => j.IsEnabled && j.NextRunUtc is not null && j.NextRunUtc.Value <= nowUtc)
			.OrderBy(j => j.NextRunUtc)];
	}

	public async Task CreateAsync(ScheduledJobRecord job, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(job);
		await EnsureInitializedAsync(cancellationToken);

		await _tableClient.AddEntityAsync(MapModelToEntity(job), cancellationToken);
	}

	public async Task UpdateAsync(ScheduledJobRecord job, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(job);
		await EnsureInitializedAsync(cancellationToken);

		var response = await _tableClient.UpdateEntityAsync(
			MapModelToEntity(job),
			ETag.All,
			TableUpdateMode.Replace,
			cancellationToken);

		job.ETag = response.Headers.ETag?.ToString() ?? job.ETag;
	}

	public async Task<bool> TryClaimAsync(
		ScheduledJobRecord job,
		DateTimeOffset nowUtc,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(job);
		await EnsureInitializedAsync(cancellationToken);

		var previousState = job.RunState;
		var previousClaimedAt = job.ClaimedAtUtc;

		job.RunState = JobRunState.Running;
		job.ClaimedAtUtc = nowUtc;
		job.UpdatedAtUtc = nowUtc;

		try
		{
			var response = await _tableClient.UpdateEntityAsync(
				MapModelToEntity(job),
				new ETag(job.ETag),
				TableUpdateMode.Replace,
				cancellationToken);

			job.ETag = response.Headers.ETag?.ToString() ?? job.ETag;
			return true;
		}
		catch (RequestFailedException ex) when (ex.Status == 412)
		{
			_logger.LogInformation("Claim for job {JobName} was lost to a concurrent writer.", job.Name);
			job.RunState = previousState;
			job.ClaimedAtUtc = previousClaimedAt;
			return false;
		}
	}

	public async Task DeleteAsync(string name, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		await EnsureInitializedAsync(cancellationToken);

		await _tableClient.DeleteEntityAsync(PartitionKey, name.Trim(), ETag.All, cancellationToken);
	}

	private TableEntity MapModelToEntity(ScheduledJobRecord job)
	{
		return new TableEntity(PartitionKey, job.Name)
		{
			["DisplayName"] = job.DisplayName,
			["JobType"] = job.JobType,
			["CronExpression"] = job.CronExpression,
			["TimeZoneId"] = job.TimeZoneId,
			["ArgumentsJson"] = job.ArgumentsJson,
			["IsEnabled"] = job.IsEnabled,
			["NextRunUtc"] = job.NextRunUtc,
			["LastRunUtc"] = job.LastRunUtc,
			["LastRunStatus"] = job.LastRunStatus?.ToString(),
			["LastRunError"] = Truncate(job.LastRunError, 4_000),
			["LastRunDurationMs"] = job.LastRunDurationMs,
			["ConsecutiveFailureCount"] = job.ConsecutiveFailureCount,
			["MaxRetryCount"] = job.MaxRetryCount,
			["RetryBaseDelaySeconds"] = job.RetryBaseDelaySeconds,
			["RunState"] = job.RunState.ToString(),
			["ClaimedAtUtc"] = job.ClaimedAtUtc,
			["CreatedAtUtc"] = job.CreatedAtUtc,
			["UpdatedAtUtc"] = job.UpdatedAtUtc
		};
	}

	private static ScheduledJobRecord MapEntityToModel(TableEntity entity)
	{
		return new ScheduledJobRecord
		{
			Name = entity.RowKey,
			DisplayName = entity.GetString("DisplayName") ?? entity.RowKey,
			JobType = entity.GetString("JobType") ?? string.Empty,
			CronExpression = entity.GetString("CronExpression") ?? string.Empty,
			TimeZoneId = entity.GetString("TimeZoneId") ?? "UTC",
			ArgumentsJson = entity.GetString("ArgumentsJson"),
			IsEnabled = entity.GetBoolean("IsEnabled") ?? false,
			NextRunUtc = entity.GetDateTimeOffset("NextRunUtc"),
			LastRunUtc = entity.GetDateTimeOffset("LastRunUtc"),
			LastRunStatus = ParseStatus(entity.GetString("LastRunStatus")),
			LastRunError = entity.GetString("LastRunError"),
			LastRunDurationMs = entity.GetInt64("LastRunDurationMs"),
			ConsecutiveFailureCount = entity.GetInt32("ConsecutiveFailureCount") ?? 0,
			MaxRetryCount = entity.GetInt32("MaxRetryCount") ?? 2,
			RetryBaseDelaySeconds = entity.GetInt32("RetryBaseDelaySeconds") ?? 30,
			RunState = Enum.TryParse<JobRunState>(entity.GetString("RunState"), out var runState)
				? runState
				: JobRunState.Idle,
			ClaimedAtUtc = entity.GetDateTimeOffset("ClaimedAtUtc"),
			CreatedAtUtc = entity.GetDateTimeOffset("CreatedAtUtc") ?? default,
			UpdatedAtUtc = entity.GetDateTimeOffset("UpdatedAtUtc") ?? default,
			ETag = entity.ETag.ToString()
		};
	}

	private static JobRunStatus? ParseStatus(string? value)
		=> Enum.TryParse<JobRunStatus>(value, out var status) ? status : null;

	private static string? Truncate(string? value, int maxLength)
		=> value is not null && value.Length > maxLength ? value[..maxLength] : value;

	private static string EscapeODataString(string value) => value.Replace("'", "''");

	private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
	{
		if (_initialized)
		{
			return;
		}

		await _initializationLock.WaitAsync(cancellationToken);
		try
		{
			if (_initialized)
			{
				return;
			}

			await _tableClient.CreateIfNotExistsAsync(cancellationToken);
			_initialized = true;
		}
		finally
		{
			_initializationLock.Release();
		}
	}
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/BarretApi.Infrastructure.UnitTests --filter "FullyQualifiedName~AzureTableScheduledJobRepository_Tests"`
Expected: PASS, 13 tests.

If `UpdateEntityAsync` returns a null `Response` from the substitute and the ETag assignment throws, guard it with `response?.Headers.ETag` — do not change the test.

- [ ] **Step 6: Format and commit**

```bash
dotnet format --include src/BarretApi.Infrastructure/Services/AzureTableScheduledJobRepository.cs tests/BarretApi.Infrastructure.UnitTests/Services/AzureTableScheduledJobRepository_Tests.cs
git add src/BarretApi.Infrastructure/Services/AzureTableScheduledJobRepository.cs tests/BarretApi.Infrastructure.UnitTests/BarretApi.Infrastructure.UnitTests.csproj tests/BarretApi.Infrastructure.UnitTests/Services/AzureTableScheduledJobRepository_Tests.cs
git commit -m "feat: add azure table scheduled job repository"
```

---

### Task 8: `AzureTableJobRunRepository` and the purge handler

**Files:**
- Create: `src/BarretApi.Infrastructure/Services/AzureTableJobRunRepository.cs`
- Create: `src/BarretApi.Core/Services/Jobs/PurgeJobRunsJobHandler.cs`
- Test: `tests/BarretApi.Infrastructure.UnitTests/Services/AzureTableJobRunRepository_Tests.cs`
- Test: `tests/BarretApi.Core.UnitTests/Services/Jobs/PurgeJobRunsJobHandler_Tests.cs`

**Interfaces:**
- Consumes: `IJobRunRepository` (Task 4), `JobRunRecord`, `JobRunStatus`, `JobTriggerType` (Task 2), `IScheduledJobHandler`, `JobExecutionContext`, `JobExecutionResult` (Task 3), `JobSchedulerOptions` (Task 1).
- Produces:
  - `AzureTableJobRunRepository` with public `(IOptions<JobSchedulerOptions>, ILogger<AzureTableJobRunRepository>)` and internal `(TableClient, IOptions<JobSchedulerOptions>, ILogger<AzureTableJobRunRepository>)` constructors.
  - `PurgeJobRunsJobHandler` with `JobType => "purge-job-runs"`.

Runs are partitioned by job name. The row key is an inverted tick count so a plain partition query returns newest first without sorting in memory.

- [ ] **Step 1: Write the failing repository tests**

Create `tests/BarretApi.Infrastructure.UnitTests/Services/AzureTableJobRunRepository_Tests.cs`:

```csharp
using Azure;
using Azure.Data.Tables;
using BarretApi.Core.Configuration;
using BarretApi.Core.Models;
using BarretApi.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace BarretApi.Infrastructure.UnitTests.Services;

public sealed class AzureTableJobRunRepository_Tests
{
	private static readonly DateTimeOffset Now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

	private readonly TableClient _tableClient = Substitute.For<TableClient>();

	private AzureTableJobRunRepository CreateSut()
		=> new(
			_tableClient,
			Options.Create(new JobSchedulerOptions
			{
				TableStorage = new JobSchedulerTableStorageOptions
				{
					ConnectionString = "UseDevelopmentStorage=true"
				}
			}),
			NullLogger<AzureTableJobRunRepository>.Instance);

	private static JobRunRecord CreateRun(string runId = "run-1", DateTimeOffset? startedAt = null)
		=> new()
		{
			RunId = runId,
			JobName = "daily-tip",
			JobType = "tip-of-day",
			TriggerType = JobTriggerType.Scheduled,
			ScheduledForUtc = startedAt ?? Now,
			StartedAtUtc = startedAt ?? Now,
			CompletedAtUtc = (startedAt ?? Now).AddSeconds(3),
			DurationMs = 3_000,
			Status = JobRunStatus.Succeeded,
			AttemptCount = 1,
			Summary = "posted"
		};

	private static TableEntity CreateEntity(string runId, DateTimeOffset startedAt)
		=> new("daily-tip", AzureTableJobRunRepository.BuildRowKey(startedAt, runId))
		{
			["RunId"] = runId,
			["JobType"] = "tip-of-day",
			["TriggerType"] = "Scheduled",
			["ScheduledForUtc"] = startedAt,
			["StartedAtUtc"] = startedAt,
			["CompletedAtUtc"] = startedAt.AddSeconds(3),
			["DurationMs"] = 3_000L,
			["Status"] = "Succeeded",
			["AttemptCount"] = 1,
			["Summary"] = "posted"
		};

	private void SetQueryResult(params TableEntity[] entities)
	{
		var pageable = Substitute.For<AsyncPageable<TableEntity>>();
		pageable.GetAsyncEnumerator(Arg.Any<CancellationToken>())
			.Returns(AsyncEnumeratorOf(entities));
		_tableClient.QueryAsync<TableEntity>(
				Arg.Any<string>(),
				cancellationToken: Arg.Any<CancellationToken>())
			.Returns(pageable);
	}

	private static async IAsyncEnumerator<T> AsyncEnumeratorOf<T>(params T[] items)
	{
		foreach (var item in items)
		{
			yield return item;
		}

		await Task.CompletedTask;
	}

	[Fact]
	public async Task AddAsync_PartitionsByJobName()
	{
		await CreateSut().AddAsync(CreateRun());

		await _tableClient.Received(1).AddEntityAsync(
			Arg.Is<TableEntity>(e => e.PartitionKey == "daily-tip"),
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public void BuildRowKey_SortsNewestFirst()
	{
		var older = AzureTableJobRunRepository.BuildRowKey(Now.AddHours(-1), "run-old");
		var newer = AzureTableJobRunRepository.BuildRowKey(Now, "run-new");

		string.CompareOrdinal(newer, older).ShouldBeLessThan(0);
	}

	[Fact]
	public async Task GetByJobAsync_MapsEveryStoredField()
	{
		SetQueryResult(CreateEntity("run-1", Now));

		var runs = await CreateSut().GetByJobAsync("daily-tip", 10);

		var run = runs.ShouldHaveSingleItem();
		run.RunId.ShouldBe("run-1");
		run.JobName.ShouldBe("daily-tip");
		run.JobType.ShouldBe("tip-of-day");
		run.TriggerType.ShouldBe(JobTriggerType.Scheduled);
		run.Status.ShouldBe(JobRunStatus.Succeeded);
		run.AttemptCount.ShouldBe(1);
		run.DurationMs.ShouldBe(3_000);
		run.Summary.ShouldBe("posted");
	}

	[Fact]
	public async Task GetByJobAsync_StopsAtMaxCount()
	{
		SetQueryResult(
			CreateEntity("run-3", Now),
			CreateEntity("run-2", Now.AddMinutes(-1)),
			CreateEntity("run-1", Now.AddMinutes(-2)));

		var runs = await CreateSut().GetByJobAsync("daily-tip", 2);

		runs.Count.ShouldBe(2);
		runs[0].RunId.ShouldBe("run-3");
	}

	[Fact]
	public async Task PurgeOlderThanAsync_DeletesOnlyRunsStartedBeforeTheCutoff()
	{
		SetQueryResult(
			CreateEntity("old", Now.AddDays(-40)),
			CreateEntity("recent", Now.AddDays(-1)));

		var deleted = await CreateSut().PurgeOlderThanAsync(Now.AddDays(-30));

		deleted.ShouldBe(1);
		await _tableClient.Received(1).DeleteEntityAsync(
			"daily-tip",
			Arg.Is<string>(rowKey => rowKey.EndsWith("old")),
			Arg.Any<ETag>(),
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task PurgeOlderThanAsync_ReturnsZero_GivenNothingIsOldEnough()
	{
		SetQueryResult(CreateEntity("recent", Now.AddDays(-1)));

		var deleted = await CreateSut().PurgeOlderThanAsync(Now.AddDays(-30));

		deleted.ShouldBe(0);
		await _tableClient.DidNotReceiveWithAnyArgs().DeleteEntityAsync(default!, default!, default, default);
	}
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/BarretApi.Infrastructure.UnitTests --filter "FullyQualifiedName~AzureTableJobRunRepository_Tests"`
Expected: build failure — the repository does not exist.

- [ ] **Step 3: Write the run repository**

Create `src/BarretApi.Infrastructure/Services/AzureTableJobRunRepository.cs`:

```csharp
using Azure;
using Azure.Data.Tables;
using Azure.Identity;
using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BarretApi.Infrastructure.Services;

/// <summary>
/// Run history, partitioned by job name. Row keys invert the start time so a plain
/// partition query already reads newest-first.
/// </summary>
public sealed class AzureTableJobRunRepository : IJobRunRepository
{
	private readonly TableClient _tableClient;
	private readonly ILogger<AzureTableJobRunRepository> _logger;
	private readonly SemaphoreSlim _initializationLock = new(1, 1);
	private bool _initialized;

	public AzureTableJobRunRepository(
		IOptions<JobSchedulerOptions> options,
		ILogger<AzureTableJobRunRepository> logger)
	{
		var schedulerOptions = options.Value;
		_logger = logger;
		schedulerOptions.ThrowIfInvalid();

		var tableName = schedulerOptions.TableStorage.RunsTableName.Trim().ToLowerInvariant();

		_tableClient = !string.IsNullOrWhiteSpace(schedulerOptions.TableStorage.ConnectionString)
			? new TableClient(schedulerOptions.TableStorage.ConnectionString, tableName)
			: new TableClient(
				new Uri(schedulerOptions.TableStorage.AccountEndpoint),
				tableName,
				new DefaultAzureCredential());
	}

	internal AzureTableJobRunRepository(
		TableClient tableClient,
		IOptions<JobSchedulerOptions> options,
		ILogger<AzureTableJobRunRepository> logger)
	{
		_tableClient = tableClient;
		_logger = logger;
		_initialized = true;
	}

	/// <summary>
	/// Descending row key: the further in the future the start time, the smaller the key.
	/// </summary>
	internal static string BuildRowKey(DateTimeOffset startedAtUtc, string runId)
	{
		var inverted = DateTimeOffset.MaxValue.Ticks - startedAtUtc.UtcTicks;
		return $"{inverted:D19}-{runId}";
	}

	public async Task AddAsync(JobRunRecord run, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(run);
		await EnsureInitializedAsync(cancellationToken);

		var entity = new TableEntity(run.JobName, BuildRowKey(run.StartedAtUtc, run.RunId))
		{
			["RunId"] = run.RunId,
			["JobType"] = run.JobType,
			["TriggerType"] = run.TriggerType.ToString(),
			["ScheduledForUtc"] = run.ScheduledForUtc,
			["StartedAtUtc"] = run.StartedAtUtc,
			["CompletedAtUtc"] = run.CompletedAtUtc,
			["DurationMs"] = run.DurationMs,
			["Status"] = run.Status.ToString(),
			["AttemptCount"] = run.AttemptCount,
			["Summary"] = Truncate(run.Summary, 4_000),
			["ErrorMessage"] = Truncate(run.ErrorMessage, 4_000)
		};

		await _tableClient.AddEntityAsync(entity, cancellationToken);
	}

	public async Task<IReadOnlyList<JobRunRecord>> GetByJobAsync(
		string jobName,
		int maxCount,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(jobName);
		await EnsureInitializedAsync(cancellationToken);

		var effectiveMax = Math.Clamp(maxCount, 1, 500);
		var filter = $"PartitionKey eq '{EscapeODataString(jobName.Trim())}'";
		var runs = new List<JobRunRecord>();

		await foreach (var entity in _tableClient.QueryAsync<TableEntity>(
			filter,
			cancellationToken: cancellationToken))
		{
			runs.Add(MapEntityToModel(entity, jobName.Trim()));

			if (runs.Count >= effectiveMax)
			{
				break;
			}
		}

		return runs;
	}

	public async Task<int> PurgeOlderThanAsync(
		DateTimeOffset cutoffUtc,
		CancellationToken cancellationToken = default)
	{
		await EnsureInitializedAsync(cancellationToken);

		var expired = new List<(string PartitionKey, string RowKey)>();

		await foreach (var entity in _tableClient.QueryAsync<TableEntity>(
			string.Empty,
			cancellationToken: cancellationToken))
		{
			var startedAt = entity.GetDateTimeOffset("StartedAtUtc");
			if (startedAt is not null && startedAt.Value < cutoffUtc)
			{
				expired.Add((entity.PartitionKey, entity.RowKey));
			}
		}

		foreach (var (partitionKey, rowKey) in expired)
		{
			await _tableClient.DeleteEntityAsync(partitionKey, rowKey, ETag.All, cancellationToken);
		}

		if (expired.Count > 0)
		{
			_logger.LogInformation(
				"Purged {DeletedCount} job runs started before {Cutoff}.",
				expired.Count,
				cutoffUtc);
		}

		return expired.Count;
	}

	private static JobRunRecord MapEntityToModel(TableEntity entity, string jobName)
	{
		return new JobRunRecord
		{
			RunId = entity.GetString("RunId") ?? entity.RowKey,
			JobName = jobName,
			JobType = entity.GetString("JobType") ?? string.Empty,
			TriggerType = Enum.TryParse<JobTriggerType>(entity.GetString("TriggerType"), out var trigger)
				? trigger
				: JobTriggerType.Scheduled,
			ScheduledForUtc = entity.GetDateTimeOffset("ScheduledForUtc"),
			StartedAtUtc = entity.GetDateTimeOffset("StartedAtUtc") ?? default,
			CompletedAtUtc = entity.GetDateTimeOffset("CompletedAtUtc"),
			DurationMs = entity.GetInt64("DurationMs") ?? 0,
			Status = Enum.TryParse<JobRunStatus>(entity.GetString("Status"), out var status)
				? status
				: JobRunStatus.Failed,
			AttemptCount = entity.GetInt32("AttemptCount") ?? 0,
			Summary = entity.GetString("Summary"),
			ErrorMessage = entity.GetString("ErrorMessage")
		};
	}

	private static string? Truncate(string? value, int maxLength)
		=> value is not null && value.Length > maxLength ? value[..maxLength] : value;

	private static string EscapeODataString(string value) => value.Replace("'", "''");

	private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
	{
		if (_initialized)
		{
			return;
		}

		await _initializationLock.WaitAsync(cancellationToken);
		try
		{
			if (_initialized)
			{
				return;
			}

			await _tableClient.CreateIfNotExistsAsync(cancellationToken);
			_initialized = true;
		}
		finally
		{
			_initializationLock.Release();
		}
	}
}
```

- [ ] **Step 4: Run the repository tests to verify they pass**

Run: `dotnet test tests/BarretApi.Infrastructure.UnitTests --filter "FullyQualifiedName~AzureTableJobRunRepository_Tests"`
Expected: PASS, 6 tests.

- [ ] **Step 5: Write the failing purge handler test**

Create `tests/BarretApi.Core.UnitTests/Services/Jobs/PurgeJobRunsJobHandler_Tests.cs`:

```csharp
using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Core.Services.Jobs;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;

namespace BarretApi.Core.UnitTests.Services.Jobs;

public sealed class PurgeJobRunsJobHandler_Tests
{
	private static readonly DateTimeOffset Now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

	private readonly IJobRunRepository _runRepository = Substitute.For<IJobRunRepository>();
	private readonly FakeTimeProvider _timeProvider = new(Now);

	private PurgeJobRunsJobHandler CreateSut(int retentionDays = 30)
		=> new(
			_runRepository,
			Options.Create(new JobSchedulerOptions
			{
				RunRetentionDays = retentionDays,
				TableStorage = new JobSchedulerTableStorageOptions { ConnectionString = "UseDevelopmentStorage=true" }
			}),
			_timeProvider,
			NullLogger<PurgeJobRunsJobHandler>.Instance);

	private static JobExecutionContext CreateContext()
		=> new("purge-job-runs", "purge-job-runs", "run-1", Now, null);

	[Fact]
	public void UsesTheExpectedJobType()
	{
		CreateSut().JobType.ShouldBe("purge-job-runs");
	}

	[Fact]
	public async Task PurgesUsingTheConfiguredRetentionWindow()
	{
		await CreateSut(retentionDays: 30).ExecuteAsync(CreateContext());

		await _runRepository.Received(1).PurgeOlderThanAsync(
			Now.AddDays(-30),
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task ReportsHowManyRunsWereDeleted()
	{
		_runRepository.PurgeOlderThanAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
			.Returns(7);

		var result = await CreateSut().ExecuteAsync(CreateContext());

		result.Success.ShouldBeTrue();
		result.Summary.ShouldContain("7");
	}
}
```

- [ ] **Step 6: Write the purge handler**

Create `src/BarretApi.Core/Services/Jobs/PurgeJobRunsJobHandler.cs`:

```csharp
using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BarretApi.Core.Services.Jobs;

/// <summary>
/// Deletes run history past the retention window. Built in rather than special-cased,
/// so it appears in the job list and can be paused like any other job.
/// </summary>
public sealed class PurgeJobRunsJobHandler(
	IJobRunRepository runRepository,
	IOptions<JobSchedulerOptions> options,
	TimeProvider timeProvider,
	ILogger<PurgeJobRunsJobHandler> logger)
	: IScheduledJobHandler
{
	private readonly IJobRunRepository _runRepository = runRepository;
	private readonly JobSchedulerOptions _options = options.Value;
	private readonly TimeProvider _timeProvider = timeProvider;
	private readonly ILogger<PurgeJobRunsJobHandler> _logger = logger;

	public string JobType => "purge-job-runs";

	public async Task<JobExecutionResult> ExecuteAsync(
		JobExecutionContext context,
		CancellationToken cancellationToken = default)
	{
		var cutoff = _timeProvider.GetUtcNow().AddDays(-_options.RunRetentionDays);
		var deletedCount = await _runRepository.PurgeOlderThanAsync(cutoff, cancellationToken);

		_logger.LogInformation(
			"Purged {DeletedCount} job runs older than {RetentionDays} days.",
			deletedCount,
			_options.RunRetentionDays);

		return JobExecutionResult.Ok($"Deleted {deletedCount} run(s) started before {cutoff:O}.");
	}
}
```

- [ ] **Step 7: Run the handler tests to verify they pass**

Run: `dotnet test tests/BarretApi.Core.UnitTests --filter "FullyQualifiedName~PurgeJobRunsJobHandler_Tests"`
Expected: PASS, 3 tests.

- [ ] **Step 8: Format and commit**

```bash
dotnet format --include src/BarretApi.Infrastructure/Services/AzureTableJobRunRepository.cs src/BarretApi.Core/Services/Jobs/PurgeJobRunsJobHandler.cs tests/BarretApi.Infrastructure.UnitTests/Services/AzureTableJobRunRepository_Tests.cs tests/BarretApi.Core.UnitTests/Services/Jobs/PurgeJobRunsJobHandler_Tests.cs
git add src/BarretApi.Infrastructure/Services/AzureTableJobRunRepository.cs src/BarretApi.Core/Services/Jobs/PurgeJobRunsJobHandler.cs tests/BarretApi.Infrastructure.UnitTests/Services/AzureTableJobRunRepository_Tests.cs tests/BarretApi.Core.UnitTests/Services/Jobs/PurgeJobRunsJobHandler_Tests.cs
git commit -m "feat: add job run history repository and purge handler"
```

---

### Task 9: Job argument parsing and interfaces for the wrapped services

Handlers need to substitute the services they wrap. `TipOfDayService` and `RssRandomPostService` are `sealed`, so NSubstitute cannot fake them, and `NasaApodPostService` / `NasaGibsPostService` can only be faked by passing their whole dependency graph to `Substitute.For<T>(...)`. Extracting four interfaces — the convention the rest of `Core` already follows — makes Tasks 10 and 11 straightforward. This is additive: the concrete registrations stay, and existing endpoints keep taking concrete types.

**Files:**
- Create: `src/BarretApi.Core/Services/Jobs/JobArguments.cs`
- Create: `src/BarretApi.Core/Interfaces/ITipOfDayService.cs`
- Create: `src/BarretApi.Core/Interfaces/IRssRandomPostService.cs`
- Create: `src/BarretApi.Core/Interfaces/INasaApodPostService.cs`
- Create: `src/BarretApi.Core/Interfaces/INasaGibsPostService.cs`
- Modify: `src/BarretApi.Core/Services/TipOfDayService.cs` (class declaration only)
- Modify: `src/BarretApi.Core/Services/RssRandomPostService.cs` (class declaration only)
- Modify: `src/BarretApi.Core/Services/NasaApodPostService.cs` (class declaration only)
- Modify: `src/BarretApi.Core/Services/NasaGibsPostService.cs` (class declaration only)
- Modify: `src/BarretApi.Api/Program.cs:158-181`
- Test: `tests/BarretApi.Core.UnitTests/Services/Jobs/JobArguments_Tests.cs`

**Interfaces:**
- Consumes: existing `TipOfDayPostCommand`, `TipOfDayPostResult`, `RssRandomPostQuery`, `RssRandomPostResult`, `ApodPostResult`, `SatellitePostResult`.
- Produces:
  - `JobArguments.Deserialize<T>(string? json)` returning `T?`, and `JobArguments.IsValidJson(string? json)` returning `bool`.
  - `ITipOfDayService.SelectAndPostAsync(TipOfDayPostCommand command, CancellationToken cancellationToken = default)` → `Task<TipOfDayPostResult>`.
  - `IRssRandomPostService.SelectAndPostAsync(RssRandomPostQuery query, CancellationToken cancellationToken = default)` → `Task<RssRandomPostResult>`.
  - `INasaApodPostService.PostAsync(DateOnly? date, IReadOnlyList<string> platforms, CancellationToken cancellationToken = default)` → `Task<ApodPostResult>`.
  - `INasaGibsPostService.PostAsync(DateOnly? date, string? layer, string? title, string? description, double? bboxSouth, double? bboxWest, double? bboxNorth, double? bboxEast, int? imageWidth, int? imageHeight, IReadOnlyList<string> platforms, CancellationToken cancellationToken = default)` → `Task<SatellitePostResult>`.

- [ ] **Step 1: Write the failing argument tests**

Create `tests/BarretApi.Core.UnitTests/Services/Jobs/JobArguments_Tests.cs`:

```csharp
using BarretApi.Core.Services.Jobs;
using Shouldly;

namespace BarretApi.Core.UnitTests.Services.Jobs;

public sealed class JobArguments_Tests
{
	private sealed record SampleArguments(string? Category, int? MaxCount, string[]? Platforms);

	[Fact]
	public void Deserialize_ReturnsNull_GivenNullOrWhitespace()
	{
		JobArguments.Deserialize<SampleArguments>(null).ShouldBeNull();
		JobArguments.Deserialize<SampleArguments>("   ").ShouldBeNull();
	}

	[Fact]
	public void Deserialize_ReadsCamelCaseProperties()
	{
		var arguments = JobArguments.Deserialize<SampleArguments>(
			"""{"category":"dotnet","maxCount":25,"platforms":["bluesky"]}""");

		arguments!.Category.ShouldBe("dotnet");
		arguments.MaxCount.ShouldBe(25);
		arguments.Platforms.ShouldBe(["bluesky"]);
	}

	[Fact]
	public void Deserialize_IsCaseInsensitive()
	{
		var arguments = JobArguments.Deserialize<SampleArguments>("""{"Category":"dotnet"}""");

		arguments!.Category.ShouldBe("dotnet");
	}

	[Fact]
	public void Deserialize_LeavesAbsentPropertiesNull()
	{
		var arguments = JobArguments.Deserialize<SampleArguments>("""{"category":"dotnet"}""");

		arguments!.MaxCount.ShouldBeNull();
		arguments.Platforms.ShouldBeNull();
	}

	[Fact]
	public void Deserialize_Throws_GivenMalformedJson()
	{
		Should.Throw<Exception>(() => JobArguments.Deserialize<SampleArguments>("{not json"));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("""{"category":"dotnet"}""")]
	public void IsValidJson_ReturnsTrue_GivenAbsentOrWellFormedJson(string? json)
	{
		JobArguments.IsValidJson(json).ShouldBeTrue();
	}

	[Theory]
	[InlineData("{not json")]
	[InlineData("[1,2")]
	[InlineData("\"unterminated")]
	public void IsValidJson_ReturnsFalse_GivenMalformedJson(string json)
	{
		JobArguments.IsValidJson(json).ShouldBeFalse();
	}

	[Fact]
	public void IsValidJson_ReturnsFalse_GivenAJsonScalar()
	{
		JobArguments.IsValidJson("42").ShouldBeFalse();
	}
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/BarretApi.Core.UnitTests --filter "FullyQualifiedName~JobArguments_Tests"`
Expected: build failure — `JobArguments` does not exist.

- [ ] **Step 3: Write `JobArguments`**

Create `src/BarretApi.Core/Services/Jobs/JobArguments.cs`:

```csharp
using System.Text.Json;

namespace BarretApi.Core.Services.Jobs;

/// <summary>
/// Reads the optional JSON blob stored on a job definition. Arguments are what let one
/// handler serve several schedules — two tip-of-day jobs posting different categories, say.
/// </summary>
public static class JobArguments
{
	private static readonly JsonSerializerOptions SerializerOptions = new()
	{
		PropertyNameCaseInsensitive = true
	};

	public static T? Deserialize<T>(string? json)
		where T : class
	{
		return string.IsNullOrWhiteSpace(json)
			? null
			: JsonSerializer.Deserialize<T>(json, SerializerOptions);
	}

	/// <summary>
	/// True when the value is absent or is a well-formed JSON object. Used by the API
	/// validators so a malformed argument blob is rejected at edit time.
	/// </summary>
	public static bool IsValidJson(string? json)
	{
		if (string.IsNullOrWhiteSpace(json))
		{
			return true;
		}

		try
		{
			using var document = JsonDocument.Parse(json);
			return document.RootElement.ValueKind == JsonValueKind.Object;
		}
		catch (JsonException)
		{
			return false;
		}
	}
}
```

- [ ] **Step 4: Run the argument tests to verify they pass**

Run: `dotnet test tests/BarretApi.Core.UnitTests --filter "FullyQualifiedName~JobArguments_Tests"`
Expected: PASS, 12 tests.

- [ ] **Step 5: Add the four service interfaces**

Create `src/BarretApi.Core/Interfaces/ITipOfDayService.cs`:

```csharp
using BarretApi.Core.Models;

namespace BarretApi.Core.Interfaces;

public interface ITipOfDayService
{
	Task<TipOfDayPostResult> SelectAndPostAsync(
		TipOfDayPostCommand command,
		CancellationToken cancellationToken = default);
}
```

Create `src/BarretApi.Core/Interfaces/IRssRandomPostService.cs`:

```csharp
using BarretApi.Core.Models;

namespace BarretApi.Core.Interfaces;

public interface IRssRandomPostService
{
	Task<RssRandomPostResult> SelectAndPostAsync(
		RssRandomPostQuery query,
		CancellationToken cancellationToken = default);
}
```

Create `src/BarretApi.Core/Interfaces/INasaApodPostService.cs`:

```csharp
using BarretApi.Core.Models;

namespace BarretApi.Core.Interfaces;

public interface INasaApodPostService
{
	Task<ApodPostResult> PostAsync(
		DateOnly? date,
		IReadOnlyList<string> platforms,
		CancellationToken cancellationToken = default);
}
```

Create `src/BarretApi.Core/Interfaces/INasaGibsPostService.cs`:

```csharp
using BarretApi.Core.Models;

namespace BarretApi.Core.Interfaces;

public interface INasaGibsPostService
{
	Task<SatellitePostResult> PostAsync(
		DateOnly? date,
		string? layer,
		string? title,
		string? description,
		double? bboxSouth,
		double? bboxWest,
		double? bboxNorth,
		double? bboxEast,
		int? imageWidth,
		int? imageHeight,
		IReadOnlyList<string> platforms,
		CancellationToken cancellationToken = default);
}
```

- [ ] **Step 6: Implement the interfaces on the existing services**

Change only the class declarations. Do not touch the method bodies — the existing signatures already match.

In `src/BarretApi.Core/Services/TipOfDayService.cs`, add `using BarretApi.Core.Interfaces;` if absent and change:

```csharp
public sealed class TipOfDayService(
```

so the parameter list is followed by `: ITipOfDayService` on the line after the closing parenthesis of the primary constructor — matching the existing style used by `ScheduledSocialPostProcessor`:

```csharp
	ILogger<TipOfDayService> logger)
	: ITipOfDayService
{
```

Apply the same change to:
- `RssRandomPostService` → `: IRssRandomPostService`
- `NasaApodPostService` → `: INasaApodPostService`
- `NasaGibsPostService` → `: INasaGibsPostService`

`NasaApodPostService` and `NasaGibsPostService` declare their methods `public virtual`; leave `virtual` in place so the existing endpoint tests that substitute the concrete classes keep working.

- [ ] **Step 7: Register the interfaces**

In `src/BarretApi.Api/Program.cs`, immediately after each existing concrete registration, add the interface alias:

```csharp
builder.Services.AddSingleton<RssRandomPostService>();
builder.Services.AddSingleton<IRssRandomPostService>(sp => sp.GetRequiredService<RssRandomPostService>());
builder.Services.AddSingleton<TipOfDayService>();
builder.Services.AddSingleton<ITipOfDayService>(sp => sp.GetRequiredService<TipOfDayService>());
```

and likewise after the `NasaApodPostService` registration (line ~170):

```csharp
builder.Services.AddSingleton<INasaApodPostService>(sp => sp.GetRequiredService<NasaApodPostService>());
```

and after the `NasaGibsPostService` registration (line ~181):

```csharp
builder.Services.AddSingleton<INasaGibsPostService>(sp => sp.GetRequiredService<NasaGibsPostService>());
```

- [ ] **Step 8: Build and run the full suite to prove nothing regressed**

Run: `dotnet build`
Expected: succeeds with zero warnings.

Run: `dotnet test tests/BarretApi.Core.UnitTests tests/BarretApi.Api.UnitTests`
Expected: the Nasa failures noted in the Global Constraints remain, and no new failures appear. Compare the failure count against a `git stash`-ed baseline if anything looks different.

- [ ] **Step 9: Format and commit**

```bash
dotnet format --include src/BarretApi.Core/Services/Jobs/JobArguments.cs src/BarretApi.Core/Interfaces/ITipOfDayService.cs src/BarretApi.Core/Interfaces/IRssRandomPostService.cs src/BarretApi.Core/Interfaces/INasaApodPostService.cs src/BarretApi.Core/Interfaces/INasaGibsPostService.cs src/BarretApi.Core/Services/TipOfDayService.cs src/BarretApi.Core/Services/RssRandomPostService.cs src/BarretApi.Core/Services/NasaApodPostService.cs src/BarretApi.Core/Services/NasaGibsPostService.cs src/BarretApi.Api/Program.cs tests/BarretApi.Core.UnitTests/Services/Jobs/JobArguments_Tests.cs
git add src/BarretApi.Core/Services/Jobs/JobArguments.cs src/BarretApi.Core/Interfaces src/BarretApi.Core/Services src/BarretApi.Api/Program.cs tests/BarretApi.Core.UnitTests/Services/Jobs/JobArguments_Tests.cs
git commit -m "refactor: add interfaces for post services and job argument parsing"
```

---

### Task 10: Handlers for scheduled posts, RSS promotion, and RSS random

**Files:**
- Create: `src/BarretApi.Core/Services/Jobs/ProcessScheduledPostsJobHandler.cs`
- Create: `src/BarretApi.Core/Services/Jobs/RssPromotionJobHandler.cs`
- Create: `src/BarretApi.Core/Services/Jobs/RssRandomJobHandler.cs`
- Test: `tests/BarretApi.Core.UnitTests/Services/Jobs/ProcessScheduledPostsJobHandler_Tests.cs`
- Test: `tests/BarretApi.Core.UnitTests/Services/Jobs/RssPromotionJobHandler_Tests.cs`
- Test: `tests/BarretApi.Core.UnitTests/Services/Jobs/RssRandomJobHandler_Tests.cs`

**Interfaces:**
- Consumes: `IScheduledJobHandler`, `JobExecutionContext`, `JobExecutionResult` (Task 3); `JobArguments` (Task 9); existing `IScheduledSocialPostProcessor`, `IBlogPromotionOrchestrator`, `IRssRandomPostService`, `BlogPromotionOptions`.
- Produces: three handlers with `JobType` values `process-scheduled-posts`, `rss-promotion`, and `rss-random`.

Argument shapes, which Task 16 documents:
- `process-scheduled-posts`: `{"maxCount": 100}` — all optional.
- `rss-promotion`: `{"feedUrl": "...", "header": "...", "recentDaysWindow": 7}` — all optional; the orchestrator falls back to `BlogPromotion` config.
- `rss-random`: `{"feedUrl": "...", "platforms": ["bluesky"], "excludeTags": ["draft"], "maxAgeDays": 90, "header": "..."}` — `feedUrl` falls back to `BlogPromotion:FeedUrl`.

- [ ] **Step 1: Write the failing tests**

Create `tests/BarretApi.Core.UnitTests/Services/Jobs/ProcessScheduledPostsJobHandler_Tests.cs`:

```csharp
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Core.Services.Jobs;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace BarretApi.Core.UnitTests.Services.Jobs;

public sealed class ProcessScheduledPostsJobHandler_Tests
{
	private static readonly DateTimeOffset Now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

	private readonly IScheduledSocialPostProcessor _processor =
		Substitute.For<IScheduledSocialPostProcessor>();

	private ProcessScheduledPostsJobHandler CreateSut()
		=> new(_processor, NullLogger<ProcessScheduledPostsJobHandler>.Instance);

	private static JobExecutionContext CreateContext(string? argumentsJson = null)
		=> new("drain-scheduled", "process-scheduled-posts", "run-1", Now, argumentsJson);

	private static ScheduledPostProcessingSummary CreateSummary(
		int attempted = 1,
		int succeeded = 1,
		int failed = 0)
		=> new()
		{
			RunId = "sched-run-1",
			StartedAtUtc = Now,
			CompletedAtUtc = Now.AddSeconds(2),
			DueCount = attempted,
			AttemptedCount = attempted,
			SucceededCount = succeeded,
			FailedCount = failed,
			SkippedCount = 0
		};

	[Fact]
	public void UsesTheExpectedJobType()
	{
		CreateSut().JobType.ShouldBe("process-scheduled-posts");
	}

	[Fact]
	public async Task PassesNullMaxCount_GivenNoArguments()
	{
		_processor.ProcessDueAsync(Arg.Any<int?>(), Arg.Any<CancellationToken>())
			.Returns(CreateSummary());

		await CreateSut().ExecuteAsync(CreateContext());

		await _processor.Received(1).ProcessDueAsync(null, Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task PassesTheConfiguredMaxCount()
	{
		_processor.ProcessDueAsync(Arg.Any<int?>(), Arg.Any<CancellationToken>())
			.Returns(CreateSummary());

		await CreateSut().ExecuteAsync(CreateContext("""{"maxCount":25}"""));

		await _processor.Received(1).ProcessDueAsync(25, Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Succeeds_GivenNothingWasDue()
	{
		_processor.ProcessDueAsync(Arg.Any<int?>(), Arg.Any<CancellationToken>())
			.Returns(CreateSummary(attempted: 0, succeeded: 0));

		var result = await CreateSut().ExecuteAsync(CreateContext());

		result.Success.ShouldBeTrue();
	}

	[Fact]
	public async Task Succeeds_GivenEveryPostPublished()
	{
		_processor.ProcessDueAsync(Arg.Any<int?>(), Arg.Any<CancellationToken>())
			.Returns(CreateSummary(attempted: 3, succeeded: 3));

		var result = await CreateSut().ExecuteAsync(CreateContext());

		result.Success.ShouldBeTrue();
		result.Summary.ShouldContain("3");
	}

	[Fact]
	public async Task Fails_GivenAnyPostFailed()
	{
		_processor.ProcessDueAsync(Arg.Any<int?>(), Arg.Any<CancellationToken>())
			.Returns(CreateSummary(attempted: 3, succeeded: 2, failed: 1));

		var result = await CreateSut().ExecuteAsync(CreateContext());

		result.Success.ShouldBeFalse();
		result.ErrorMessage.ShouldContain("1");
	}
}
```

Create `tests/BarretApi.Core.UnitTests/Services/Jobs/RssPromotionJobHandler_Tests.cs`:

```csharp
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Core.Services.Jobs;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace BarretApi.Core.UnitTests.Services.Jobs;

public sealed class RssPromotionJobHandler_Tests
{
	private static readonly DateTimeOffset Now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

	private readonly IBlogPromotionOrchestrator _orchestrator =
		Substitute.For<IBlogPromotionOrchestrator>();

	private RssPromotionJobHandler CreateSut()
		=> new(_orchestrator, NullLogger<RssPromotionJobHandler>.Instance);

	private static JobExecutionContext CreateContext(string? argumentsJson = null)
		=> new("promote-blog", "rss-promotion", "run-1", Now, argumentsJson);

	private static PromotionRunSummary CreateSummary(params PromotionEntryFailure[] failures)
	{
		var summary = new PromotionRunSummary
		{
			RunId = "promo-run-1",
			StartedAtUtc = Now,
			CompletedAtUtc = Now.AddSeconds(4),
			EntriesEvaluated = 5,
			NewPostsAttempted = 1,
			NewPostsSucceeded = failures.Length == 0 ? 1 : 0
		};

		summary.Failures.AddRange(failures);
		return summary;
	}

	private static PromotionEntryFailure CreateFailure()
		=> new()
		{
			EntryIdentity = "entry-1",
			CanonicalUrl = "https://example.com/post",
			Phase = PromotionPhase.Initial,
			Platform = "bluesky",
			ErrorCode = "PUBLISH_FAILED",
			ErrorMessage = "rate limited"
		};

	[Fact]
	public void UsesTheExpectedJobType()
	{
		CreateSut().JobType.ShouldBe("rss-promotion");
	}

	[Fact]
	public async Task PassesNulls_GivenNoArguments()
	{
		_orchestrator.RunAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
			.Returns(CreateSummary());

		await CreateSut().ExecuteAsync(CreateContext());

		await _orchestrator.Received(1).RunAsync(null, null, null, Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task PassesEveryConfiguredOverride()
	{
		_orchestrator.RunAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
			.Returns(CreateSummary());

		await CreateSut().ExecuteAsync(CreateContext(
			"""{"feedUrl":"https://example.com/feed.xml","header":"New post","recentDaysWindow":14}"""));

		await _orchestrator.Received(1).RunAsync(
			"https://example.com/feed.xml",
			"New post",
			14,
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Succeeds_GivenNoFailures()
	{
		_orchestrator.RunAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
			.Returns(CreateSummary());

		var result = await CreateSut().ExecuteAsync(CreateContext());

		result.Success.ShouldBeTrue();
	}

	[Fact]
	public async Task Fails_GivenTheRunReportedFailures()
	{
		_orchestrator.RunAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
			.Returns(CreateSummary(CreateFailure()));

		var result = await CreateSut().ExecuteAsync(CreateContext());

		result.Success.ShouldBeFalse();
		result.ErrorMessage.ShouldContain("rate limited");
	}
}
```

Create `tests/BarretApi.Core.UnitTests/Services/Jobs/RssRandomJobHandler_Tests.cs`:

```csharp
using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Core.Services.Jobs;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace BarretApi.Core.UnitTests.Services.Jobs;

public sealed class RssRandomJobHandler_Tests
{
	private static readonly DateTimeOffset Now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

	private readonly IRssRandomPostService _service = Substitute.For<IRssRandomPostService>();

	private RssRandomJobHandler CreateSut(string configuredFeedUrl = "https://blog.example.com/index.xml")
		=> new(
			_service,
			Options.Create(new BlogPromotionOptions { FeedUrl = configuredFeedUrl }),
			NullLogger<RssRandomJobHandler>.Instance);

	private static JobExecutionContext CreateContext(string? argumentsJson = null)
		=> new("random-post", "rss-random", "run-1", Now, argumentsJson);

	private static RssRandomPostResult CreateResult(bool success)
		=> new()
		{
			SelectedEntry = new BlogFeedEntry
			{
				Title = "A post",
				CanonicalUrl = "https://example.com/post",
				PublishedAtUtc = Now.AddDays(-3)
			},
			PlatformResults =
			[
				new PlatformPostResult { Platform = "bluesky", Success = success, ErrorMessage = success ? null : "boom" }
			]
		};

	[Fact]
	public void UsesTheExpectedJobType()
	{
		CreateSut().JobType.ShouldBe("rss-random");
	}

	[Fact]
	public async Task FallsBackToTheConfiguredFeedUrl_GivenNoArguments()
	{
		_service.SelectAndPostAsync(Arg.Any<RssRandomPostQuery>(), Arg.Any<CancellationToken>())
			.Returns(CreateResult(success: true));

		await CreateSut().ExecuteAsync(CreateContext());

		await _service.Received(1).SelectAndPostAsync(
			Arg.Is<RssRandomPostQuery>(q => q.FeedUrl == "https://blog.example.com/index.xml"),
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task PassesEveryConfiguredArgument()
	{
		_service.SelectAndPostAsync(Arg.Any<RssRandomPostQuery>(), Arg.Any<CancellationToken>())
			.Returns(CreateResult(success: true));

		await CreateSut().ExecuteAsync(CreateContext(
			"""{"feedUrl":"https://other.example/feed","platforms":["mastodon"],"excludeTags":["draft"],"maxAgeDays":90,"header":"ICYMI"}"""));

		await _service.Received(1).SelectAndPostAsync(
			Arg.Is<RssRandomPostQuery>(q =>
				q.FeedUrl == "https://other.example/feed"
				&& q.Platforms.Count == 1
				&& q.Platforms[0] == "mastodon"
				&& q.ExcludeTags.Count == 1
				&& q.MaxAgeDays == 90
				&& q.Header == "ICYMI"),
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Fails_GivenNoFeedUrlIsAvailable()
	{
		var result = await CreateSut(configuredFeedUrl: string.Empty).ExecuteAsync(CreateContext());

		result.Success.ShouldBeFalse();
		result.ErrorMessage.ShouldContain("feedUrl");
		await _service.DidNotReceiveWithAnyArgs().SelectAndPostAsync(default!, default);
	}

	[Fact]
	public async Task Succeeds_GivenEveryPlatformPublished()
	{
		_service.SelectAndPostAsync(Arg.Any<RssRandomPostQuery>(), Arg.Any<CancellationToken>())
			.Returns(CreateResult(success: true));

		var result = await CreateSut().ExecuteAsync(CreateContext());

		result.Success.ShouldBeTrue();
		result.Summary.ShouldContain("A post");
	}

	[Fact]
	public async Task Fails_GivenAPlatformFailed()
	{
		_service.SelectAndPostAsync(Arg.Any<RssRandomPostQuery>(), Arg.Any<CancellationToken>())
			.Returns(CreateResult(success: false));

		var result = await CreateSut().ExecuteAsync(CreateContext());

		result.Success.ShouldBeFalse();
		result.ErrorMessage.ShouldContain("bluesky");
	}
}
```

Note: if `BlogFeedEntry` or `ScheduledPostProcessingSummary` require properties beyond those set above, add them to the test fixtures — check the model definitions before running.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/BarretApi.Core.UnitTests --filter "FullyQualifiedName~JobHandler_Tests"`
Expected: build failure — none of the three handlers exist.

- [ ] **Step 3: Add a shared platform-result helper**

Append this class to `src/BarretApi.Core/Services/Jobs/JobArguments.cs`'s file? No — create a separate file `src/BarretApi.Core/Services/Jobs/PlatformResultSummary.cs`:

```csharp
using BarretApi.Core.Models;

namespace BarretApi.Core.Services.Jobs;

/// <summary>
/// Turns platform post results into a job outcome. Any platform failure fails the run,
/// so a partial publish is retried and reported rather than silently accepted.
/// </summary>
internal static class PlatformResultSummary
{
	public static JobExecutionResult ToResult(
		IReadOnlyList<PlatformPostResult> results,
		string subject)
	{
		if (results.Count == 0)
		{
			return JobExecutionResult.Fail("No target platforms were configured for this job.");
		}

		var failures = results.Where(r => !r.Success).ToList();
		if (failures.Count == 0)
		{
			var platforms = string.Join(", ", results.Select(r => r.Platform));
			return JobExecutionResult.Ok($"Posted \"{subject}\" to {platforms}.");
		}

		var detail = string.Join(
			"; ",
			failures.Select(f => $"{f.Platform}: {f.ErrorMessage ?? f.ErrorCode ?? "failed"}"));

		return JobExecutionResult.Fail($"Failed to post \"{subject}\" — {detail}", $"Posted \"{subject}\".");
	}
}
```

- [ ] **Step 4: Write `ProcessScheduledPostsJobHandler`**

Create `src/BarretApi.Core/Services/Jobs/ProcessScheduledPostsJobHandler.cs`:

```csharp
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using Microsoft.Extensions.Logging;

namespace BarretApi.Core.Services.Jobs;

/// <summary>
/// Drains one-off scheduled social posts that have come due. Wraps the same processor
/// the POST /api/social-posts/scheduled/process endpoint calls.
/// </summary>
public sealed class ProcessScheduledPostsJobHandler(
	IScheduledSocialPostProcessor processor,
	ILogger<ProcessScheduledPostsJobHandler> logger)
	: IScheduledJobHandler
{
	private readonly IScheduledSocialPostProcessor _processor = processor;
	private readonly ILogger<ProcessScheduledPostsJobHandler> _logger = logger;

	public sealed record Arguments(int? MaxCount);

	public string JobType => "process-scheduled-posts";

	public async Task<JobExecutionResult> ExecuteAsync(
		JobExecutionContext context,
		CancellationToken cancellationToken = default)
	{
		var arguments = JobArguments.Deserialize<Arguments>(context.ArgumentsJson);
		var summary = await _processor.ProcessDueAsync(arguments?.MaxCount, cancellationToken);

		_logger.LogInformation(
			"Scheduled post drain {RunId}: {Succeeded} succeeded, {Failed} failed, {Skipped} skipped.",
			summary.RunId,
			summary.SucceededCount,
			summary.FailedCount,
			summary.SkippedCount);

		var text = $"{summary.SucceededCount} published, {summary.FailedCount} failed, {summary.SkippedCount} skipped.";

		return summary.FailedCount > 0
			? JobExecutionResult.Fail($"{summary.FailedCount} scheduled post(s) failed to publish.", text)
			: JobExecutionResult.Ok(text);
	}
}
```

- [ ] **Step 5: Write `RssPromotionJobHandler`**

Create `src/BarretApi.Core/Services/Jobs/RssPromotionJobHandler.cs`:

```csharp
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using Microsoft.Extensions.Logging;

namespace BarretApi.Core.Services.Jobs;

/// <summary>
/// Promotes new blog entries from the configured feed. Wraps the same orchestrator
/// the POST /api/social-posts/rss-promotion endpoint calls.
/// </summary>
public sealed class RssPromotionJobHandler(
	IBlogPromotionOrchestrator orchestrator,
	ILogger<RssPromotionJobHandler> logger)
	: IScheduledJobHandler
{
	private readonly IBlogPromotionOrchestrator _orchestrator = orchestrator;
	private readonly ILogger<RssPromotionJobHandler> _logger = logger;

	public sealed record Arguments(string? FeedUrl, string? Header, int? RecentDaysWindow);

	public string JobType => "rss-promotion";

	public async Task<JobExecutionResult> ExecuteAsync(
		JobExecutionContext context,
		CancellationToken cancellationToken = default)
	{
		var arguments = JobArguments.Deserialize<Arguments>(context.ArgumentsJson);

		var summary = await _orchestrator.RunAsync(
			arguments?.FeedUrl,
			arguments?.Header,
			arguments?.RecentDaysWindow,
			cancellationToken);

		var text =
			$"{summary.EntriesEvaluated} evaluated, {summary.NewPostsSucceeded}/{summary.NewPostsAttempted} new, " +
			$"{summary.ReminderPostsSucceeded}/{summary.ReminderPostsAttempted} reminders.";

		if (summary.Failures.Count == 0)
		{
			_logger.LogInformation("Blog promotion run {RunId} completed: {Summary}", summary.RunId, text);
			return JobExecutionResult.Ok(text);
		}

		var detail = string.Join(
			"; ",
			summary.Failures.Select(f => $"{f.Platform}/{f.EntryIdentity}: {f.ErrorMessage}"));

		return JobExecutionResult.Fail($"{summary.Failures.Count} promotion failure(s) — {detail}", text);
	}
}
```

- [ ] **Step 6: Write `RssRandomJobHandler`**

Create `src/BarretApi.Core/Services/Jobs/RssRandomJobHandler.cs`:

```csharp
using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BarretApi.Core.Services.Jobs;

/// <summary>
/// Posts a random entry from the blog feed. Wraps the same service the
/// POST /api/social-posts/rss-random endpoint calls.
/// </summary>
public sealed class RssRandomJobHandler(
	IRssRandomPostService rssRandomPostService,
	IOptions<BlogPromotionOptions> blogPromotionOptions,
	ILogger<RssRandomJobHandler> logger)
	: IScheduledJobHandler
{
	private readonly IRssRandomPostService _rssRandomPostService = rssRandomPostService;
	private readonly BlogPromotionOptions _blogPromotionOptions = blogPromotionOptions.Value;
	private readonly ILogger<RssRandomJobHandler> _logger = logger;

	public sealed record Arguments(
		string? FeedUrl,
		string[]? Platforms,
		string[]? ExcludeTags,
		int? MaxAgeDays,
		string? Header);

	public string JobType => "rss-random";

	public async Task<JobExecutionResult> ExecuteAsync(
		JobExecutionContext context,
		CancellationToken cancellationToken = default)
	{
		var arguments = JobArguments.Deserialize<Arguments>(context.ArgumentsJson);

		var feedUrl = !string.IsNullOrWhiteSpace(arguments?.FeedUrl)
			? arguments.FeedUrl
			: _blogPromotionOptions.FeedUrl;

		if (string.IsNullOrWhiteSpace(feedUrl))
		{
			return JobExecutionResult.Fail(
				"No feedUrl was supplied in the job arguments and BlogPromotion:FeedUrl is not configured.");
		}

		var query = new RssRandomPostQuery
		{
			FeedUrl = feedUrl,
			Platforms = arguments?.Platforms ?? [],
			ExcludeTags = arguments?.ExcludeTags ?? [],
			MaxAgeDays = arguments?.MaxAgeDays,
			Header = arguments?.Header
		};

		var result = await _rssRandomPostService.SelectAndPostAsync(query, cancellationToken);

		_logger.LogInformation(
			"Random RSS post selected {Title} for job {JobName}.",
			result.SelectedEntry.Title,
			context.JobName);

		return PlatformResultSummary.ToResult(result.PlatformResults, result.SelectedEntry.Title);
	}
}
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test tests/BarretApi.Core.UnitTests --filter "FullyQualifiedName~JobHandler_Tests"`
Expected: PASS — 6 + 5 + 6 = 17 new tests plus the 3 purge handler tests from Task 8.

- [ ] **Step 8: Format and commit**

```bash
dotnet format --include src/BarretApi.Core/Services/Jobs/PlatformResultSummary.cs src/BarretApi.Core/Services/Jobs/ProcessScheduledPostsJobHandler.cs src/BarretApi.Core/Services/Jobs/RssPromotionJobHandler.cs src/BarretApi.Core/Services/Jobs/RssRandomJobHandler.cs
git add src/BarretApi.Core/Services/Jobs tests/BarretApi.Core.UnitTests/Services/Jobs
git commit -m "feat: add scheduled post, rss promotion, and rss random job handlers"
```

---

### Task 11: Handlers for tip of the day, NASA APOD, and satellite

**Files:**
- Create: `src/BarretApi.Core/Services/Jobs/TipOfDayJobHandler.cs`
- Create: `src/BarretApi.Core/Services/Jobs/NasaApodJobHandler.cs`
- Create: `src/BarretApi.Core/Services/Jobs/SatelliteJobHandler.cs`
- Test: `tests/BarretApi.Core.UnitTests/Services/Jobs/TipOfDayJobHandler_Tests.cs`
- Test: `tests/BarretApi.Core.UnitTests/Services/Jobs/NasaApodJobHandler_Tests.cs`
- Test: `tests/BarretApi.Core.UnitTests/Services/Jobs/SatelliteJobHandler_Tests.cs`

**Interfaces:**
- Consumes: `IScheduledJobHandler`, `JobExecutionContext`, `JobExecutionResult` (Task 3); `JobArguments`, `PlatformResultSummary`, `ITipOfDayService`, `INasaApodPostService`, `INasaGibsPostService` (Tasks 9, 10).
- Produces: handlers with `JobType` values `tip-of-day`, `nasa-apod`, and `satellite`.

Argument shapes:
- `tip-of-day`: `{"category": "dotnet", "platforms": ["bluesky"], "leader": "Tip of the day:"}` — `category` is required.
- `nasa-apod`: `{"platforms": ["bluesky","mastodon"]}` — the date is always "today"; a scheduled job never backfills.
- `satellite`: `{"layer": "...", "title": "...", "description": "...", "platforms": [...], "imageWidth": 1200, "imageHeight": 900}` — every field optional, defaults come from `NasaGibs` config.

- [ ] **Step 1: Write the failing tests**

Create `tests/BarretApi.Core.UnitTests/Services/Jobs/TipOfDayJobHandler_Tests.cs`:

```csharp
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Core.Services.Jobs;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace BarretApi.Core.UnitTests.Services.Jobs;

public sealed class TipOfDayJobHandler_Tests
{
	private static readonly DateTimeOffset Now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

	private readonly ITipOfDayService _service = Substitute.For<ITipOfDayService>();

	private TipOfDayJobHandler CreateSut()
		=> new(_service, NullLogger<TipOfDayJobHandler>.Instance);

	private static JobExecutionContext CreateContext(string? argumentsJson)
		=> new("daily-tip", "tip-of-day", "run-1", Now, argumentsJson);

	private static TipOfDayPostResult CreateResult(bool success)
		=> new()
		{
			SelectedTip = new TipOfDayRecord
			{
				TipId = "tip-1",
				Category = "dotnet",
				Text = "Use TimeProvider."
			},
			PlatformResults =
			[
				new PlatformPostResult { Platform = "bluesky", Success = success, ErrorMessage = success ? null : "boom" }
			],
			TipMarkedPosted = success,
			AttemptedAtUtc = Now
		};

	[Fact]
	public void UsesTheExpectedJobType()
	{
		CreateSut().JobType.ShouldBe("tip-of-day");
	}

	[Fact]
	public async Task Fails_GivenNoArguments()
	{
		var result = await CreateSut().ExecuteAsync(CreateContext(null));

		result.Success.ShouldBeFalse();
		result.ErrorMessage.ShouldContain("category");
		await _service.DidNotReceiveWithAnyArgs().SelectAndPostAsync(default!, default);
	}

	[Fact]
	public async Task Fails_GivenABlankCategory()
	{
		var result = await CreateSut().ExecuteAsync(CreateContext("""{"category":"  "}"""));

		result.Success.ShouldBeFalse();
		result.ErrorMessage.ShouldContain("category");
	}

	[Fact]
	public async Task PassesTheCategoryPlatformsAndLeader()
	{
		_service.SelectAndPostAsync(Arg.Any<TipOfDayPostCommand>(), Arg.Any<CancellationToken>())
			.Returns(CreateResult(success: true));

		await CreateSut().ExecuteAsync(CreateContext(
			"""{"category":"dotnet","platforms":["bluesky","mastodon"],"leader":"Tip:"}"""));

		await _service.Received(1).SelectAndPostAsync(
			Arg.Is<TipOfDayPostCommand>(c =>
				c.Category == "dotnet"
				&& c.Platforms.Count == 2
				&& c.Leader == "Tip:"),
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Succeeds_GivenEveryPlatformPublished()
	{
		_service.SelectAndPostAsync(Arg.Any<TipOfDayPostCommand>(), Arg.Any<CancellationToken>())
			.Returns(CreateResult(success: true));

		var result = await CreateSut().ExecuteAsync(CreateContext("""{"category":"dotnet"}"""));

		result.Success.ShouldBeTrue();
	}

	[Fact]
	public async Task Fails_GivenAPlatformFailed()
	{
		_service.SelectAndPostAsync(Arg.Any<TipOfDayPostCommand>(), Arg.Any<CancellationToken>())
			.Returns(CreateResult(success: false));

		var result = await CreateSut().ExecuteAsync(CreateContext("""{"category":"dotnet"}"""));

		result.Success.ShouldBeFalse();
		result.ErrorMessage.ShouldContain("bluesky");
	}
}
```

Create `tests/BarretApi.Core.UnitTests/Services/Jobs/NasaApodJobHandler_Tests.cs`:

```csharp
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Core.Services.Jobs;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace BarretApi.Core.UnitTests.Services.Jobs;

public sealed class NasaApodJobHandler_Tests
{
	private static readonly DateTimeOffset Now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

	private readonly INasaApodPostService _service = Substitute.For<INasaApodPostService>();

	private NasaApodJobHandler CreateSut()
		=> new(_service, NullLogger<NasaApodJobHandler>.Instance);

	private static JobExecutionContext CreateContext(string? argumentsJson = null)
		=> new("daily-apod", "nasa-apod", "run-1", Now, argumentsJson);

	private static ApodPostResult CreateResult(bool success)
		=> new()
		{
			ApodEntry = new ApodEntry
			{
				Title = "Pillars of Creation",
				Date = new DateOnly(2026, 8, 20),
				Explanation = "A nebula.",
				MediaType = ApodMediaType.Image
			},
			PlatformResults =
			[
				new PlatformPostResult { Platform = "bluesky", Success = success, ErrorMessage = success ? null : "boom" }
			],
			ImageAttached = true,
			ImageResized = false
		};

	[Fact]
	public void UsesTheExpectedJobType()
	{
		CreateSut().JobType.ShouldBe("nasa-apod");
	}

	[Fact]
	public async Task AlwaysRequestsTodaysEntry()
	{
		_service.PostAsync(Arg.Any<DateOnly?>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
			.Returns(CreateResult(success: true));

		await CreateSut().ExecuteAsync(CreateContext());

		await _service.Received(1).PostAsync(
			null,
			Arg.Any<IReadOnlyList<string>>(),
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task PassesTheConfiguredPlatforms()
	{
		_service.PostAsync(Arg.Any<DateOnly?>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
			.Returns(CreateResult(success: true));

		await CreateSut().ExecuteAsync(CreateContext("""{"platforms":["bluesky","mastodon"]}"""));

		await _service.Received(1).PostAsync(
			null,
			Arg.Is<IReadOnlyList<string>>(p => p.Count == 2 && p[0] == "bluesky"),
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Succeeds_GivenEveryPlatformPublished()
	{
		_service.PostAsync(Arg.Any<DateOnly?>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
			.Returns(CreateResult(success: true));

		var result = await CreateSut().ExecuteAsync(CreateContext());

		result.Success.ShouldBeTrue();
		result.Summary.ShouldContain("Pillars of Creation");
	}

	[Fact]
	public async Task Fails_GivenAPlatformFailed()
	{
		_service.PostAsync(Arg.Any<DateOnly?>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
			.Returns(CreateResult(success: false));

		var result = await CreateSut().ExecuteAsync(CreateContext());

		result.Success.ShouldBeFalse();
	}
}
```

Create `tests/BarretApi.Core.UnitTests/Services/Jobs/SatelliteJobHandler_Tests.cs`:

```csharp
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Core.Services.Jobs;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace BarretApi.Core.UnitTests.Services.Jobs;

public sealed class SatelliteJobHandler_Tests
{
	private static readonly DateTimeOffset Now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

	private readonly INasaGibsPostService _service = Substitute.For<INasaGibsPostService>();

	private SatelliteJobHandler CreateSut()
		=> new(_service, NullLogger<SatelliteJobHandler>.Instance);

	private static JobExecutionContext CreateContext(string? argumentsJson = null)
		=> new("daily-satellite", "satellite", "run-1", Now, argumentsJson);

	private static SatellitePostResult CreateResult(bool success)
		=> new(
			new DateOnly(2026, 8, 19),
			"MODIS_Terra_CorrectedReflectance_TrueColor",
			"Satellite view of Ohio",
			"https://worldview.earthdata.nasa.gov/",
			38.0,
			-85.0,
			42.5,
			-80.0,
			1200,
			900,
			true,
			false,
			[new PlatformPostResult { Platform = "bluesky", Success = success, ErrorMessage = success ? null : "boom" }]);

	[Fact]
	public void UsesTheExpectedJobType()
	{
		CreateSut().JobType.ShouldBe("satellite");
	}

	[Fact]
	public async Task PassesNullsSoConfigDefaultsApply_GivenNoArguments()
	{
		_service.PostAsync(
				Arg.Any<DateOnly?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
				Arg.Any<double?>(), Arg.Any<double?>(), Arg.Any<double?>(), Arg.Any<double?>(),
				Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
			.Returns(CreateResult(success: true));

		await CreateSut().ExecuteAsync(CreateContext());

		await _service.Received(1).PostAsync(
			null, null, null, null, null, null, null, null, null, null,
			Arg.Any<IReadOnlyList<string>>(),
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task PassesEveryConfiguredArgument()
	{
		_service.PostAsync(
				Arg.Any<DateOnly?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
				Arg.Any<double?>(), Arg.Any<double?>(), Arg.Any<double?>(), Arg.Any<double?>(),
				Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
			.Returns(CreateResult(success: true));

		await CreateSut().ExecuteAsync(CreateContext(
			"""{"layer":"VIIRS","title":"Ohio today","description":"Daily view","platforms":["mastodon"],"imageWidth":800,"imageHeight":600}"""));

		await _service.Received(1).PostAsync(
			null,
			"VIIRS",
			"Ohio today",
			"Daily view",
			null, null, null, null,
			800,
			600,
			Arg.Is<IReadOnlyList<string>>(p => p.Count == 1 && p[0] == "mastodon"),
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Succeeds_GivenEveryPlatformPublished()
	{
		_service.PostAsync(
				Arg.Any<DateOnly?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
				Arg.Any<double?>(), Arg.Any<double?>(), Arg.Any<double?>(), Arg.Any<double?>(),
				Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
			.Returns(CreateResult(success: true));

		var result = await CreateSut().ExecuteAsync(CreateContext());

		result.Success.ShouldBeTrue();
		result.Summary.ShouldContain("Satellite view of Ohio");
	}

	[Fact]
	public async Task Fails_GivenAPlatformFailed()
	{
		_service.PostAsync(
				Arg.Any<DateOnly?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
				Arg.Any<double?>(), Arg.Any<double?>(), Arg.Any<double?>(), Arg.Any<double?>(),
				Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
			.Returns(CreateResult(success: false));

		var result = await CreateSut().ExecuteAsync(CreateContext());

		result.Success.ShouldBeFalse();
	}
}
```

Note: `ApodEntry` and `TipOfDayRecord` fixtures above set the properties those models require. Check both model definitions first and add any other `required` members rather than removing assertions.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/BarretApi.Core.UnitTests --filter "FullyQualifiedName~TipOfDayJobHandler_Tests|FullyQualifiedName~NasaApodJobHandler_Tests|FullyQualifiedName~SatelliteJobHandler_Tests"`
Expected: build failure — the three handlers do not exist.

- [ ] **Step 3: Write `TipOfDayJobHandler`**

Create `src/BarretApi.Core/Services/Jobs/TipOfDayJobHandler.cs`:

```csharp
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using Microsoft.Extensions.Logging;

namespace BarretApi.Core.Services.Jobs;

/// <summary>
/// Posts a tip from one category. The category comes from the job's arguments, so several
/// tip-of-day jobs can run on different schedules against different categories.
/// </summary>
public sealed class TipOfDayJobHandler(
	ITipOfDayService tipOfDayService,
	ILogger<TipOfDayJobHandler> logger)
	: IScheduledJobHandler
{
	private readonly ITipOfDayService _tipOfDayService = tipOfDayService;
	private readonly ILogger<TipOfDayJobHandler> _logger = logger;

	public sealed record Arguments(string? Category, string[]? Platforms, string? Leader);

	public string JobType => "tip-of-day";

	public async Task<JobExecutionResult> ExecuteAsync(
		JobExecutionContext context,
		CancellationToken cancellationToken = default)
	{
		var arguments = JobArguments.Deserialize<Arguments>(context.ArgumentsJson);

		if (string.IsNullOrWhiteSpace(arguments?.Category))
		{
			return JobExecutionResult.Fail(
				"A tip-of-day job requires a category in its arguments, for example {\"category\":\"dotnet\"}.");
		}

		var command = new TipOfDayPostCommand
		{
			Category = arguments.Category,
			Platforms = arguments.Platforms ?? [],
			Leader = arguments.Leader
		};

		var result = await _tipOfDayService.SelectAndPostAsync(command, cancellationToken);

		_logger.LogInformation(
			"Tip {TipId} selected for job {JobName} in category {Category}.",
			result.SelectedTip.TipId,
			context.JobName,
			command.Category);

		return PlatformResultSummary.ToResult(result.PlatformResults, result.SelectedTip.Text);
	}
}
```

- [ ] **Step 4: Write `NasaApodJobHandler`**

Create `src/BarretApi.Core/Services/Jobs/NasaApodJobHandler.cs`:

```csharp
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using Microsoft.Extensions.Logging;

namespace BarretApi.Core.Services.Jobs;

/// <summary>
/// Posts the NASA Astronomy Picture of the Day. The date is always left null so the
/// service fetches today's entry — a scheduled run never backfills an older one.
/// </summary>
public sealed class NasaApodJobHandler(
	INasaApodPostService nasaApodPostService,
	ILogger<NasaApodJobHandler> logger)
	: IScheduledJobHandler
{
	private readonly INasaApodPostService _nasaApodPostService = nasaApodPostService;
	private readonly ILogger<NasaApodJobHandler> _logger = logger;

	public sealed record Arguments(string[]? Platforms);

	public string JobType => "nasa-apod";

	public async Task<JobExecutionResult> ExecuteAsync(
		JobExecutionContext context,
		CancellationToken cancellationToken = default)
	{
		var arguments = JobArguments.Deserialize<Arguments>(context.ArgumentsJson);

		var result = await _nasaApodPostService.PostAsync(
			null,
			arguments?.Platforms ?? [],
			cancellationToken);

		_logger.LogInformation(
			"APOD {Title} ({Date}) posted for job {JobName}.",
			result.ApodEntry.Title,
			result.ApodEntry.Date,
			context.JobName);

		return PlatformResultSummary.ToResult(result.PlatformResults, result.ApodEntry.Title);
	}
}
```

- [ ] **Step 5: Write `SatelliteJobHandler`**

Create `src/BarretApi.Core/Services/Jobs/SatelliteJobHandler.cs`:

```csharp
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using Microsoft.Extensions.Logging;

namespace BarretApi.Core.Services.Jobs;

/// <summary>
/// Posts a NASA GIBS satellite snapshot. Every argument is optional; anything left null
/// falls back to the NasaGibs configuration, including the default bounding box.
/// </summary>
public sealed class SatelliteJobHandler(
	INasaGibsPostService nasaGibsPostService,
	ILogger<SatelliteJobHandler> logger)
	: IScheduledJobHandler
{
	private readonly INasaGibsPostService _nasaGibsPostService = nasaGibsPostService;
	private readonly ILogger<SatelliteJobHandler> _logger = logger;

	public sealed record Arguments(
		string? Layer,
		string? Title,
		string? Description,
		string[]? Platforms,
		double? BboxSouth,
		double? BboxWest,
		double? BboxNorth,
		double? BboxEast,
		int? ImageWidth,
		int? ImageHeight);

	public string JobType => "satellite";

	public async Task<JobExecutionResult> ExecuteAsync(
		JobExecutionContext context,
		CancellationToken cancellationToken = default)
	{
		var arguments = JobArguments.Deserialize<Arguments>(context.ArgumentsJson);

		var result = await _nasaGibsPostService.PostAsync(
			null,
			arguments?.Layer,
			arguments?.Title,
			arguments?.Description,
			arguments?.BboxSouth,
			arguments?.BboxWest,
			arguments?.BboxNorth,
			arguments?.BboxEast,
			arguments?.ImageWidth,
			arguments?.ImageHeight,
			arguments?.Platforms ?? [],
			cancellationToken);

		_logger.LogInformation(
			"Satellite snapshot {Layer} for {Date} posted for job {JobName}.",
			result.Layer,
			result.Date,
			context.JobName);

		return PlatformResultSummary.ToResult(result.PlatformResults, result.Title);
	}
}
```

- [ ] **Step 6: Run every handler test**

Run: `dotnet test tests/BarretApi.Core.UnitTests --filter "FullyQualifiedName~Services.Jobs"`
Expected: PASS — all handler and argument tests, 47 in total.

- [ ] **Step 7: Format and commit**

```bash
dotnet format --include src/BarretApi.Core/Services/Jobs/TipOfDayJobHandler.cs src/BarretApi.Core/Services/Jobs/NasaApodJobHandler.cs src/BarretApi.Core/Services/Jobs/SatelliteJobHandler.cs
git add src/BarretApi.Core/Services/Jobs tests/BarretApi.Core.UnitTests/Services/Jobs
git commit -m "feat: add tip of day, apod, and satellite job handlers"
```

---

### Task 12: Hosted service, DI wiring, and AppHost configuration

**Files:**
- Create: `src/BarretApi.Api/Scheduling/JobSchedulerHostedService.cs`
- Modify: `src/BarretApi.Api/Program.cs`
- Modify: `src/BarretApi.AppHost/Program.cs` (renamed section — the AppHost's top-level statements file)
- Test: `tests/BarretApi.Api.UnitTests/Scheduling/JobSchedulerHostedService_Tests.cs`

**Interfaces:**
- Consumes: `JobDispatcher` (Tasks 4–6), `JobSchedulerOptions` (Task 1), `JobHandlerRegistry` (Task 3), all seven handlers (Tasks 8, 10, 11), both Azure Table repositories (Tasks 7, 8).
- Produces: `JobSchedulerHostedService` — a `BackgroundService` with an `internal Task RunTickAsync(CancellationToken cancellationToken)` so a test can drive one tick without starting the loop.

- [ ] **Step 1: Write the failing tests**

Create `tests/BarretApi.Api.UnitTests/Scheduling/JobSchedulerHostedService_Tests.cs`:

```csharp
using BarretApi.Api.Scheduling;
using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace BarretApi.Api.UnitTests.Scheduling;

public sealed class JobSchedulerHostedService_Tests
{
	private readonly IScheduledJobRepository _jobRepository = Substitute.For<IScheduledJobRepository>();
	private readonly IJobRunRepository _runRepository = Substitute.For<IJobRunRepository>();

	private sealed class StubHandler : IScheduledJobHandler
	{
		public string JobType => "tip-of-day";

		public Task<JobExecutionResult> ExecuteAsync(
			JobExecutionContext context,
			CancellationToken cancellationToken = default)
			=> Task.FromResult(JobExecutionResult.Ok());
	}

	private ServiceProvider BuildServiceProvider(JobSchedulerOptions options)
	{
		var services = new ServiceCollection();
		services.AddSingleton(_jobRepository);
		services.AddSingleton(_runRepository);
		services.AddSingleton<IScheduledJobHandler, StubHandler>();
		services.AddSingleton<JobHandlerRegistry>();
		services.AddSingleton(TimeProvider.System);
		services.AddSingleton(Options.Create(options));
		services.AddLogging();
		services.AddSingleton<JobDispatcher>();
		return services.BuildServiceProvider();
	}

	private static JobSchedulerOptions CreateOptions(bool enabled)
		=> new()
		{
			Enabled = enabled,
			TableStorage = new JobSchedulerTableStorageOptions { ConnectionString = "UseDevelopmentStorage=true" }
		};

	private JobSchedulerHostedService CreateSut(bool enabled)
	{
		var provider = BuildServiceProvider(CreateOptions(enabled));
		return new JobSchedulerHostedService(
			provider.GetRequiredService<IServiceScopeFactory>(),
			Options.Create(CreateOptions(enabled)),
			NullLogger<JobSchedulerHostedService>.Instance);
	}

	[Fact]
	public async Task DoesNotQueryForDueJobs_GivenTheSchedulerIsDisabled()
	{
		await CreateSut(enabled: false).StartAsync(CancellationToken.None);

		await _jobRepository.DidNotReceiveWithAnyArgs().GetDueAsync(default, default);
	}

	[Fact]
	public async Task RunsDueJobs_WhenATickExecutes()
	{
		_jobRepository.GetDueAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
			.Returns([]);

		await CreateSut(enabled: true).RunTickAsync(CancellationToken.None);

		await _jobRepository.Received(1).GetDueAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task SwallowsTickFailuresSoTheLoopSurvives()
	{
		_jobRepository.GetDueAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
			.Returns<IReadOnlyList<ScheduledJobRecord>>(_ => throw new InvalidOperationException("storage down"));

		await Should.NotThrowAsync(() => CreateSut(enabled: true).RunTickAsync(CancellationToken.None));
	}
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/BarretApi.Api.UnitTests --filter "FullyQualifiedName~JobSchedulerHostedService_Tests"`
Expected: build failure — `JobSchedulerHostedService` does not exist.

- [ ] **Step 3: Write the hosted service**

Create `src/BarretApi.Api/Scheduling/JobSchedulerHostedService.cs`:

```csharp
using BarretApi.Core.Configuration;
using BarretApi.Core.Services;
using Microsoft.Extensions.Options;

namespace BarretApi.Api.Scheduling;

/// <summary>
/// Ticks on an interval and asks the dispatcher to run whatever is due. Holds no state:
/// if the process dies mid-run, the next tick recovers from what is in storage.
/// </summary>
public sealed class JobSchedulerHostedService(
	IServiceScopeFactory scopeFactory,
	IOptions<JobSchedulerOptions> options,
	ILogger<JobSchedulerHostedService> logger)
	: BackgroundService
{
	private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
	private readonly JobSchedulerOptions _options = options.Value;
	private readonly ILogger<JobSchedulerHostedService> _logger = logger;

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		if (!_options.Enabled)
		{
			_logger.LogInformation(
				"Job scheduler is disabled (JobScheduler:Enabled is false). Job management endpoints remain available.");
			return;
		}

		_logger.LogInformation(
			"Job scheduler started with a {TickIntervalSeconds}s tick.",
			_options.TickIntervalSeconds);

		using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.TickIntervalSeconds));

		// Run once immediately so a restart catches up anything missed while the app was down.
		await RunTickAsync(stoppingToken);

		try
		{
			while (await timer.WaitForNextTickAsync(stoppingToken))
			{
				await RunTickAsync(stoppingToken);
			}
		}
		catch (OperationCanceledException)
		{
			_logger.LogInformation("Job scheduler is stopping.");
		}
	}

	/// <summary>
	/// One pass over the due jobs. Never throws: a storage outage must not tear down the
	/// loop, because nothing would restart it short of an app restart.
	/// </summary>
	internal async Task RunTickAsync(CancellationToken cancellationToken)
	{
		try
		{
			using var scope = _scopeFactory.CreateScope();
			var dispatcher = scope.ServiceProvider.GetRequiredService<JobDispatcher>();

			var executedCount = await dispatcher.RunDueJobsAsync(cancellationToken);

			if (executedCount > 0)
			{
				_logger.LogInformation("Job scheduler tick executed {ExecutedCount} job(s).", executedCount);
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Job scheduler tick failed. The loop will continue.");
		}
	}
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/BarretApi.Api.UnitTests --filter "FullyQualifiedName~JobSchedulerHostedService_Tests"`
Expected: PASS, 3 tests.

- [ ] **Step 5: Register everything in `Program.cs`**

In `src/BarretApi.Api/Program.cs`, add the options binding beside the other `ValidateOnStart` blocks (after the `TipOfDayOptions` block):

```csharp
builder.Services
	.AddOptions<JobSchedulerOptions>()
	.Bind(builder.Configuration.GetSection(JobSchedulerOptions.SectionName))
	.ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<JobSchedulerOptions>>(
	new OptionsValidatorAdapter<JobSchedulerOptions>(o => o.Validate()));
```

Add the scheduler services beside the other singleton registrations (after the `ITipOfDayRepository` line):

```csharp
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IScheduledJobRepository, AzureTableScheduledJobRepository>();
builder.Services.AddSingleton<IJobRunRepository, AzureTableJobRunRepository>();
builder.Services.AddSingleton<IScheduledJobHandler, ProcessScheduledPostsJobHandler>();
builder.Services.AddSingleton<IScheduledJobHandler, RssPromotionJobHandler>();
builder.Services.AddSingleton<IScheduledJobHandler, RssRandomJobHandler>();
builder.Services.AddSingleton<IScheduledJobHandler, TipOfDayJobHandler>();
builder.Services.AddSingleton<IScheduledJobHandler, NasaApodJobHandler>();
builder.Services.AddSingleton<IScheduledJobHandler, SatelliteJobHandler>();
builder.Services.AddSingleton<IScheduledJobHandler, PurgeJobRunsJobHandler>();
builder.Services.AddSingleton<JobHandlerRegistry>();
builder.Services.AddSingleton<JobDispatcher>();
builder.Services.AddHostedService<JobSchedulerHostedService>();
```

Add the required usings at the top of the file:

```csharp
using BarretApi.Api.Scheduling;
using BarretApi.Core.Services.Jobs;
```

`TimeProvider.System` may already be registered by `AddServiceDefaults()`; if the build reports a duplicate or a test observes two registrations, drop the `AddSingleton(TimeProvider.System)` line.

- [ ] **Step 6: Add the AppHost parameters**

In `src/BarretApi.AppHost/Program.cs`, add the parameter declarations after the `gibsImageHeight` line:

```csharp
var jobSchedulerEnabled = builder.AddParameter("job-scheduler-enabled");
var jobSchedulerTickIntervalSeconds = builder.AddParameter("job-scheduler-tick-interval-seconds");
var jobSchedulerClaimTimeoutMinutes = builder.AddParameter("job-scheduler-claim-timeout-minutes");
var jobSchedulerRunRetentionDays = builder.AddParameter("job-scheduler-run-retention-days");
var jobSchedulerJobsTableName = builder.AddParameter("job-scheduler-jobs-table-name");
var jobSchedulerRunsTableName = builder.AddParameter("job-scheduler-runs-table-name");
var jobSchedulerPartitionKey = builder.AddParameter("job-scheduler-partition-key");
```

and the environment wiring at the end of the `AddProject` chain, before the closing semicolon:

```csharp
	.WithEnvironment("JobScheduler__Enabled", jobSchedulerEnabled)
	.WithEnvironment("JobScheduler__TickIntervalSeconds", jobSchedulerTickIntervalSeconds)
	.WithEnvironment("JobScheduler__ClaimTimeoutMinutes", jobSchedulerClaimTimeoutMinutes)
	.WithEnvironment("JobScheduler__RunRetentionDays", jobSchedulerRunRetentionDays)
	.WithEnvironment("JobScheduler__TableStorage__ConnectionString", azuriteConnectionString)
	.WithEnvironment("JobScheduler__TableStorage__JobsTableName", jobSchedulerJobsTableName)
	.WithEnvironment("JobScheduler__TableStorage__RunsTableName", jobSchedulerRunsTableName)
	.WithEnvironment("JobScheduler__TableStorage__PartitionKey", jobSchedulerPartitionKey)
```

- [ ] **Step 7: Add the local parameter values**

Set the AppHost user secrets so a local run resolves every parameter. `job-scheduler-enabled` is `false` locally — this is what stops a `dotnet run` on a dev machine from posting to real social accounts.

```bash
dotnet user-secrets --project src/BarretApi.AppHost set "Parameters:job-scheduler-enabled" "false"
dotnet user-secrets --project src/BarretApi.AppHost set "Parameters:job-scheduler-tick-interval-seconds" "30"
dotnet user-secrets --project src/BarretApi.AppHost set "Parameters:job-scheduler-claim-timeout-minutes" "30"
dotnet user-secrets --project src/BarretApi.AppHost set "Parameters:job-scheduler-run-retention-days" "30"
dotnet user-secrets --project src/BarretApi.AppHost set "Parameters:job-scheduler-jobs-table-name" "scheduledjobs"
dotnet user-secrets --project src/BarretApi.AppHost set "Parameters:job-scheduler-runs-table-name" "jobruns"
dotnet user-secrets --project src/BarretApi.AppHost set "Parameters:job-scheduler-partition-key" "scheduled-job"
```

- [ ] **Step 8: Verify the app starts and resolves the graph**

Run: `dotnet build`
Expected: succeeds with zero warnings. A DI mistake in Step 5 surfaces at startup rather than at build time, so also run the API long enough to see it bind:

Run: `dotnet run --project src/BarretApi.AppHost` and stop it once the `api` resource reports running. Confirm the log line `Job scheduler is disabled (JobScheduler:Enabled is false).` appears.

If startup throws `InvalidOperationException: More than one job handler is registered for job type ...`, two handlers returned the same `JobType` — fix the duplicate rather than loosening `JobHandlerRegistry`.

- [ ] **Step 9: Format and commit**

```bash
dotnet format --include src/BarretApi.Api/Scheduling/JobSchedulerHostedService.cs src/BarretApi.Api/Program.cs src/BarretApi.AppHost/Program.cs tests/BarretApi.Api.UnitTests/Scheduling/JobSchedulerHostedService_Tests.cs
git add src/BarretApi.Api/Scheduling src/BarretApi.Api/Program.cs src/BarretApi.AppHost/Program.cs tests/BarretApi.Api.UnitTests/Scheduling
git commit -m "feat: wire up the job scheduler hosted service"
```

---

### Task 13: Job response DTO, list, get, and delete endpoints

**Files:**
- Create: `src/BarretApi.Api/Features/Jobs/JobResponse.cs`
- Create: `src/BarretApi.Api/Features/Jobs/JobResponseMapper.cs`
- Create: `src/BarretApi.Api/Features/Jobs/ListJobsEndpoint.cs`
- Create: `src/BarretApi.Api/Features/Jobs/GetJobRequest.cs`
- Create: `src/BarretApi.Api/Features/Jobs/GetJobEndpoint.cs`
- Create: `src/BarretApi.Api/Features/Jobs/DeleteJobEndpoint.cs`
- Test: `tests/BarretApi.Api.UnitTests/Features/Jobs/ListJobsEndpoint_HandleAsync_Tests.cs`
- Test: `tests/BarretApi.Api.UnitTests/Features/Jobs/GetJobEndpoint_HandleAsync_Tests.cs`
- Test: `tests/BarretApi.Api.UnitTests/Features/Jobs/DeleteJobEndpoint_HandleAsync_Tests.cs`

**Interfaces:**
- Consumes: `IScheduledJobRepository` (Task 4), `ScheduledJobRecord` (Task 2).
- Produces:
  - `JobResponse` — the DTO every job endpoint returns.
  - `JobListResponse(IReadOnlyList<JobResponse> Jobs)`.
  - `JobResponseMapper.ToResponse(ScheduledJobRecord job)` → `JobResponse`.
  - `GetJobRequest { string Name { get; set; } }`, reused by delete and (in Task 15) the run endpoints.

Endpoints require `X-Api-Key`, so `Configure()` must **not** call `AllowAnonymous()`. Routes carry the `/api` prefix explicitly, matching every other endpoint in this project.

- [ ] **Step 1: Write the failing tests**

Create `tests/BarretApi.Api.UnitTests/Features/Jobs/ListJobsEndpoint_HandleAsync_Tests.cs`:

```csharp
using BarretApi.Api.Features.Jobs;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using FastEndpoints;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace BarretApi.Api.UnitTests.Features.Jobs;

public sealed class ListJobsEndpoint_HandleAsync_Tests
{
	private static readonly DateTimeOffset Now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

	private readonly IScheduledJobRepository _jobRepository = Substitute.For<IScheduledJobRepository>();

	private static ScheduledJobRecord CreateJob(string name = "daily-tip")
		=> new()
		{
			Name = name,
			DisplayName = "Daily tip",
			JobType = "tip-of-day",
			CronExpression = "0 8 * * *",
			TimeZoneId = "America/Chicago",
			ArgumentsJson = """{"category":"dotnet"}""",
			IsEnabled = true,
			NextRunUtc = Now.AddHours(1),
			LastRunUtc = Now.AddDays(-1),
			LastRunStatus = JobRunStatus.Succeeded,
			LastRunDurationMs = 1_234,
			ConsecutiveFailureCount = 0,
			MaxRetryCount = 2,
			RetryBaseDelaySeconds = 30,
			RunState = JobRunState.Idle,
			CreatedAtUtc = Now.AddDays(-30),
			UpdatedAtUtc = Now.AddDays(-1)
		};

	[Fact]
	public async Task ReturnsEveryJob()
	{
		_jobRepository.GetAllAsync(Arg.Any<CancellationToken>())
			.Returns([CreateJob("daily-tip"), CreateJob("daily-apod")]);

		var ep = Factory.Create<ListJobsEndpoint>(
			_jobRepository,
			NullLogger<ListJobsEndpoint>.Instance);

		await ep.HandleAsync(default);

		ep.Response.Jobs.Count.ShouldBe(2);
	}

	[Fact]
	public async Task MapsEveryFieldOntoTheResponse()
	{
		_jobRepository.GetAllAsync(Arg.Any<CancellationToken>()).Returns([CreateJob()]);

		var ep = Factory.Create<ListJobsEndpoint>(
			_jobRepository,
			NullLogger<ListJobsEndpoint>.Instance);

		await ep.HandleAsync(default);

		var job = ep.Response.Jobs[0];
		job.Name.ShouldBe("daily-tip");
		job.DisplayName.ShouldBe("Daily tip");
		job.JobType.ShouldBe("tip-of-day");
		job.CronExpression.ShouldBe("0 8 * * *");
		job.TimeZoneId.ShouldBe("America/Chicago");
		job.ArgumentsJson.ShouldBe("""{"category":"dotnet"}""");
		job.IsEnabled.ShouldBeTrue();
		job.NextRunUtc.ShouldBe(Now.AddHours(1));
		job.LastRunStatus.ShouldBe("Succeeded");
		job.LastRunDurationMs.ShouldBe(1_234);
		job.MaxRetryCount.ShouldBe(2);
		job.RetryBaseDelaySeconds.ShouldBe(30);
		job.IsRunning.ShouldBeFalse();
	}

	[Fact]
	public async Task ReportsAJobAsRunning_GivenItIsClaimed()
	{
		var job = CreateJob();
		job.RunState = JobRunState.Running;
		_jobRepository.GetAllAsync(Arg.Any<CancellationToken>()).Returns([job]);

		var ep = Factory.Create<ListJobsEndpoint>(
			_jobRepository,
			NullLogger<ListJobsEndpoint>.Instance);

		await ep.HandleAsync(default);

		ep.Response.Jobs[0].IsRunning.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnsAnEmptyList_GivenNoJobsExist()
	{
		_jobRepository.GetAllAsync(Arg.Any<CancellationToken>()).Returns([]);

		var ep = Factory.Create<ListJobsEndpoint>(
			_jobRepository,
			NullLogger<ListJobsEndpoint>.Instance);

		await ep.HandleAsync(default);

		ep.Response.Jobs.ShouldBeEmpty();
	}
}
```

Create `tests/BarretApi.Api.UnitTests/Features/Jobs/GetJobEndpoint_HandleAsync_Tests.cs`:

```csharp
using BarretApi.Api.Features.Jobs;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using FastEndpoints;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace BarretApi.Api.UnitTests.Features.Jobs;

public sealed class GetJobEndpoint_HandleAsync_Tests
{
	private static readonly DateTimeOffset Now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

	private readonly IScheduledJobRepository _jobRepository = Substitute.For<IScheduledJobRepository>();

	private static ScheduledJobRecord CreateJob()
		=> new()
		{
			Name = "daily-tip",
			DisplayName = "Daily tip",
			JobType = "tip-of-day",
			CronExpression = "0 8 * * *",
			TimeZoneId = "UTC",
			IsEnabled = true,
			NextRunUtc = Now.AddHours(1),
			CreatedAtUtc = Now,
			UpdatedAtUtc = Now
		};

	[Fact]
	public async Task ReturnsTheJob_GivenItExists()
	{
		_jobRepository.GetByNameAsync("daily-tip", Arg.Any<CancellationToken>()).Returns(CreateJob());

		var ep = Factory.Create<GetJobEndpoint>(
			_jobRepository,
			NullLogger<GetJobEndpoint>.Instance);

		await ep.HandleAsync(new GetJobRequest { Name = "daily-tip" }, default);

		ep.Response.Name.ShouldBe("daily-tip");
	}

	[Fact]
	public async Task Returns404_GivenNoSuchJob()
	{
		_jobRepository.GetByNameAsync("missing", Arg.Any<CancellationToken>())
			.Returns((ScheduledJobRecord?)null);

		var ep = Factory.Create<GetJobEndpoint>(
			_jobRepository,
			NullLogger<GetJobEndpoint>.Instance);

		await ep.HandleAsync(new GetJobRequest { Name = "missing" }, default);

		ep.HttpContext.Response.StatusCode.ShouldBe(404);
	}
}
```

Create `tests/BarretApi.Api.UnitTests/Features/Jobs/DeleteJobEndpoint_HandleAsync_Tests.cs`:

```csharp
using BarretApi.Api.Features.Jobs;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using FastEndpoints;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace BarretApi.Api.UnitTests.Features.Jobs;

public sealed class DeleteJobEndpoint_HandleAsync_Tests
{
	private static readonly DateTimeOffset Now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

	private readonly IScheduledJobRepository _jobRepository = Substitute.For<IScheduledJobRepository>();

	private static ScheduledJobRecord CreateJob()
		=> new()
		{
			Name = "daily-tip",
			DisplayName = "Daily tip",
			JobType = "tip-of-day",
			CronExpression = "0 8 * * *",
			CreatedAtUtc = Now,
			UpdatedAtUtc = Now
		};

	[Fact]
	public async Task DeletesTheJob_GivenItExists()
	{
		_jobRepository.GetByNameAsync("daily-tip", Arg.Any<CancellationToken>()).Returns(CreateJob());

		var ep = Factory.Create<DeleteJobEndpoint>(
			_jobRepository,
			NullLogger<DeleteJobEndpoint>.Instance);

		await ep.HandleAsync(new GetJobRequest { Name = "daily-tip" }, default);

		await _jobRepository.Received(1).DeleteAsync("daily-tip", Arg.Any<CancellationToken>());
		ep.HttpContext.Response.StatusCode.ShouldBe(204);
	}

	[Fact]
	public async Task Returns404AndDeletesNothing_GivenNoSuchJob()
	{
		_jobRepository.GetByNameAsync("missing", Arg.Any<CancellationToken>())
			.Returns((ScheduledJobRecord?)null);

		var ep = Factory.Create<DeleteJobEndpoint>(
			_jobRepository,
			NullLogger<DeleteJobEndpoint>.Instance);

		await ep.HandleAsync(new GetJobRequest { Name = "missing" }, default);

		await _jobRepository.DidNotReceiveWithAnyArgs().DeleteAsync(default!, default);
		ep.HttpContext.Response.StatusCode.ShouldBe(404);
	}
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/BarretApi.Api.UnitTests --filter "FullyQualifiedName~Features.Jobs"`
Expected: build failure — none of the endpoints exist.

- [ ] **Step 3: Write the response DTOs and mapper**

Create `src/BarretApi.Api/Features/Jobs/JobResponse.cs`:

```csharp
namespace BarretApi.Api.Features.Jobs;

public sealed class JobResponse
{
	public required string Name { get; init; }
	public required string DisplayName { get; init; }
	public required string JobType { get; init; }
	public required string CronExpression { get; init; }
	public required string TimeZoneId { get; init; }
	public string? ArgumentsJson { get; init; }
	public required bool IsEnabled { get; init; }
	public bool IsRunning { get; init; }
	public DateTimeOffset? NextRunUtc { get; init; }
	public DateTimeOffset? LastRunUtc { get; init; }
	public string? LastRunStatus { get; init; }
	public string? LastRunError { get; init; }
	public long? LastRunDurationMs { get; init; }
	public int ConsecutiveFailureCount { get; init; }
	public int MaxRetryCount { get; init; }
	public int RetryBaseDelaySeconds { get; init; }
	public DateTimeOffset CreatedAtUtc { get; init; }
	public DateTimeOffset UpdatedAtUtc { get; init; }
}

public sealed class JobListResponse
{
	public required IReadOnlyList<JobResponse> Jobs { get; init; }
}
```

Create `src/BarretApi.Api/Features/Jobs/JobResponseMapper.cs`:

```csharp
using BarretApi.Core.Models;

namespace BarretApi.Api.Features.Jobs;

/// <summary>
/// Projects the stored job definition onto the API contract. The ETag and claim
/// timestamp are storage concerns and are deliberately not exposed.
/// </summary>
internal static class JobResponseMapper
{
	public static JobResponse ToResponse(ScheduledJobRecord job)
	{
		return new JobResponse
		{
			Name = job.Name,
			DisplayName = job.DisplayName,
			JobType = job.JobType,
			CronExpression = job.CronExpression,
			TimeZoneId = job.TimeZoneId,
			ArgumentsJson = job.ArgumentsJson,
			IsEnabled = job.IsEnabled,
			IsRunning = job.RunState == JobRunState.Running,
			NextRunUtc = job.NextRunUtc,
			LastRunUtc = job.LastRunUtc,
			LastRunStatus = job.LastRunStatus?.ToString(),
			LastRunError = job.LastRunError,
			LastRunDurationMs = job.LastRunDurationMs,
			ConsecutiveFailureCount = job.ConsecutiveFailureCount,
			MaxRetryCount = job.MaxRetryCount,
			RetryBaseDelaySeconds = job.RetryBaseDelaySeconds,
			CreatedAtUtc = job.CreatedAtUtc,
			UpdatedAtUtc = job.UpdatedAtUtc
		};
	}
}
```

- [ ] **Step 4: Write the three endpoints**

Create `src/BarretApi.Api/Features/Jobs/ListJobsEndpoint.cs`:

```csharp
using BarretApi.Core.Interfaces;
using FastEndpoints;

namespace BarretApi.Api.Features.Jobs;

public sealed class ListJobsEndpoint(
	IScheduledJobRepository jobRepository,
	ILogger<ListJobsEndpoint> logger)
	: EndpointWithoutRequest<JobListResponse>
{
	private readonly IScheduledJobRepository _jobRepository = jobRepository;
	private readonly ILogger<ListJobsEndpoint> _logger = logger;

	public override void Configure()
	{
		Get("/api/jobs");

		Summary(s =>
		{
			s.Summary = "List scheduled jobs";
			s.Description = "Returns every job definition with its schedule and last-run state.";
			s.Responses[200] = "The job definitions.";
			s.Responses[401] = "Missing or invalid X-Api-Key.";
		});
	}

	public override async Task HandleAsync(CancellationToken ct)
	{
		var jobs = await _jobRepository.GetAllAsync(ct);
		_logger.LogDebug("Returning {JobCount} job definitions.", jobs.Count);

		await Send.OkAsync(
			new JobListResponse { Jobs = [.. jobs.Select(JobResponseMapper.ToResponse)] },
			ct);
	}
}
```

Create `src/BarretApi.Api/Features/Jobs/GetJobRequest.cs`:

```csharp
namespace BarretApi.Api.Features.Jobs;

/// <summary>
/// Addresses a single job by its slug name. Shared by get, delete, manual run, and run history.
/// </summary>
public sealed class GetJobRequest
{
	public string Name { get; set; } = string.Empty;
}
```

Create `src/BarretApi.Api/Features/Jobs/GetJobEndpoint.cs`:

```csharp
using BarretApi.Core.Interfaces;
using FastEndpoints;

namespace BarretApi.Api.Features.Jobs;

public sealed class GetJobEndpoint(
	IScheduledJobRepository jobRepository,
	ILogger<GetJobEndpoint> logger)
	: Endpoint<GetJobRequest, JobResponse>
{
	private readonly IScheduledJobRepository _jobRepository = jobRepository;
	private readonly ILogger<GetJobEndpoint> _logger = logger;

	public override void Configure()
	{
		Get("/api/jobs/{Name}");

		Summary(s =>
		{
			s.Summary = "Get a scheduled job";
			s.Responses[200] = "The job definition.";
			s.Responses[401] = "Missing or invalid X-Api-Key.";
			s.Responses[404] = "No job with that name exists.";
		});
	}

	public override async Task HandleAsync(GetJobRequest req, CancellationToken ct)
	{
		var job = await _jobRepository.GetByNameAsync(req.Name, ct);
		if (job is null)
		{
			_logger.LogInformation("Job {JobName} was not found.", req.Name);
			await Send.NotFoundAsync(ct);
			return;
		}

		await Send.OkAsync(JobResponseMapper.ToResponse(job), ct);
	}
}
```

Create `src/BarretApi.Api/Features/Jobs/DeleteJobEndpoint.cs`:

```csharp
using BarretApi.Core.Interfaces;
using FastEndpoints;

namespace BarretApi.Api.Features.Jobs;

public sealed class DeleteJobEndpoint(
	IScheduledJobRepository jobRepository,
	ILogger<DeleteJobEndpoint> logger)
	: Endpoint<GetJobRequest>
{
	private readonly IScheduledJobRepository _jobRepository = jobRepository;
	private readonly ILogger<DeleteJobEndpoint> _logger = logger;

	public override void Configure()
	{
		Delete("/api/jobs/{Name}");

		Summary(s =>
		{
			s.Summary = "Delete a scheduled job";
			s.Description = "Removes the job definition. Its run history is left in place and ages out with the retention window.";
			s.Responses[204] = "The job was deleted.";
			s.Responses[401] = "Missing or invalid X-Api-Key.";
			s.Responses[404] = "No job with that name exists.";
		});
	}

	public override async Task HandleAsync(GetJobRequest req, CancellationToken ct)
	{
		var job = await _jobRepository.GetByNameAsync(req.Name, ct);
		if (job is null)
		{
			await Send.NotFoundAsync(ct);
			return;
		}

		await _jobRepository.DeleteAsync(req.Name, ct);
		_logger.LogInformation("Deleted job {JobName}.", req.Name);

		await Send.NoContentAsync(ct);
	}
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/BarretApi.Api.UnitTests --filter "FullyQualifiedName~Features.Jobs"`
Expected: PASS, 8 tests.

If `Send.NoContentAsync` or `Send.NotFoundAsync` does not resolve, check the FastEndpoints 8.x surface used elsewhere in this repo — `Send.OkAsync` and `Send.ErrorsAsync` are already in use in `ProcessScheduledPostsEndpoint`, so the `Send.*` form is correct for this version.

- [ ] **Step 6: Format and commit**

```bash
dotnet format --include src/BarretApi.Api/Features/Jobs/JobResponse.cs src/BarretApi.Api/Features/Jobs/JobResponseMapper.cs src/BarretApi.Api/Features/Jobs/ListJobsEndpoint.cs src/BarretApi.Api/Features/Jobs/GetJobRequest.cs src/BarretApi.Api/Features/Jobs/GetJobEndpoint.cs src/BarretApi.Api/Features/Jobs/DeleteJobEndpoint.cs
git add src/BarretApi.Api/Features/Jobs tests/BarretApi.Api.UnitTests/Features/Jobs
git commit -m "feat: add job list, get, and delete endpoints"
```

---

### Task 14: Create and update endpoints with schedule validation

This is where FR-003 and FR-011 live: a schedule is parsed before it is stored, and enabling a job or changing its cron recomputes the next run time from now.

**Files:**
- Create: `src/BarretApi.Api/Features/Jobs/SaveJobRequest.cs`
- Create: `src/BarretApi.Api/Features/Jobs/SaveJobValidator.cs`
- Create: `src/BarretApi.Api/Features/Jobs/JobRequestValidation.cs`
- Create: `src/BarretApi.Api/Features/Jobs/CreateJobEndpoint.cs`
- Create: `src/BarretApi.Api/Features/Jobs/UpdateJobEndpoint.cs`
- Test: `tests/BarretApi.Api.UnitTests/Features/Jobs/SaveJobValidator_Tests.cs`
- Test: `tests/BarretApi.Api.UnitTests/Features/Jobs/CreateJobEndpoint_HandleAsync_Tests.cs`
- Test: `tests/BarretApi.Api.UnitTests/Features/Jobs/UpdateJobEndpoint_HandleAsync_Tests.cs`

**Interfaces:**
- Consumes: `IScheduledJobRepository` (Task 4), `JobHandlerRegistry` (Task 3), `CronSchedule` (Task 2), `JobArguments.IsValidJson` (Task 9), `JobResponseMapper` (Task 13), `TimeProvider`.
- Produces:
  - `SaveJobRequest` — used by both create and update; `Name` is bound from the route on update and from the body on create.
  - `JobRequestValidation.TryBuildSchedule(...)` — shared semantic validation returning a `CronSchedule` or an error message.

Field-shape rules (required, ranges, slug format) live in `SaveJobValidator`. Semantic rules that need services — is the job type registered, does the cron parse, does the time zone resolve — run in the endpoint via `AddError` + `Send.ErrorsAsync(400, ct)`, the pattern this project already uses.

- [ ] **Step 1: Write the failing validator tests**

Create `tests/BarretApi.Api.UnitTests/Features/Jobs/SaveJobValidator_Tests.cs`:

```csharp
using BarretApi.Api.Features.Jobs;
using Shouldly;

namespace BarretApi.Api.UnitTests.Features.Jobs;

public sealed class SaveJobValidator_Tests
{
	private readonly SaveJobValidator _sut = new();

	private static SaveJobRequest CreateValid()
		=> new()
		{
			Name = "daily-tip",
			DisplayName = "Daily tip",
			JobType = "tip-of-day",
			CronExpression = "0 8 * * *",
			TimeZoneId = "America/Chicago",
			ArgumentsJson = """{"category":"dotnet"}""",
			IsEnabled = true,
			MaxRetryCount = 2,
			RetryBaseDelaySeconds = 30
		};

	[Fact]
	public void Passes_GivenAValidRequest()
	{
		_sut.Validate(CreateValid()).IsValid.ShouldBeTrue();
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("Daily Tip")]
	[InlineData("daily_tip")]
	[InlineData("-daily")]
	[InlineData("daily-")]
	public void Fails_GivenANameThatIsNotASlug(string name)
	{
		var request = CreateValid();
		request.Name = name;

		_sut.Validate(request).IsValid.ShouldBeFalse();
	}

	[Fact]
	public void Fails_GivenANameLongerThan100Characters()
	{
		var request = CreateValid();
		request.Name = new string('a', 101);

		_sut.Validate(request).IsValid.ShouldBeFalse();
	}

	[Fact]
	public void Fails_GivenABlankJobType()
	{
		var request = CreateValid();
		request.JobType = "  ";

		_sut.Validate(request).IsValid.ShouldBeFalse();
	}

	[Fact]
	public void Fails_GivenABlankCronExpression()
	{
		var request = CreateValid();
		request.CronExpression = string.Empty;

		_sut.Validate(request).IsValid.ShouldBeFalse();
	}

	[Theory]
	[InlineData(-1)]
	[InlineData(11)]
	public void Fails_GivenMaxRetryCountOutOfRange(int maxRetryCount)
	{
		var request = CreateValid();
		request.MaxRetryCount = maxRetryCount;

		_sut.Validate(request).IsValid.ShouldBeFalse();
	}

	[Theory]
	[InlineData(-1)]
	[InlineData(3_601)]
	public void Fails_GivenRetryBaseDelayOutOfRange(int seconds)
	{
		var request = CreateValid();
		request.RetryBaseDelaySeconds = seconds;

		_sut.Validate(request).IsValid.ShouldBeFalse();
	}

	[Fact]
	public void Fails_GivenMalformedArgumentsJson()
	{
		var request = CreateValid();
		request.ArgumentsJson = "{not json";

		_sut.Validate(request).IsValid.ShouldBeFalse();
	}

	[Fact]
	public void Passes_GivenNoArgumentsJson()
	{
		var request = CreateValid();
		request.ArgumentsJson = null;

		_sut.Validate(request).IsValid.ShouldBeTrue();
	}

	[Fact]
	public void Passes_GivenNoDisplayName()
	{
		var request = CreateValid();
		request.DisplayName = null;

		_sut.Validate(request).IsValid.ShouldBeTrue();
	}
}
```

- [ ] **Step 2: Write the failing endpoint tests**

Create `tests/BarretApi.Api.UnitTests/Features/Jobs/CreateJobEndpoint_HandleAsync_Tests.cs`:

```csharp
using BarretApi.Api.Features.Jobs;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Core.Services;
using FastEndpoints;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;

namespace BarretApi.Api.UnitTests.Features.Jobs;

public sealed class CreateJobEndpoint_HandleAsync_Tests
{
	private static readonly DateTimeOffset Now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

	private readonly IScheduledJobRepository _jobRepository = Substitute.For<IScheduledJobRepository>();
	private readonly FakeTimeProvider _timeProvider = new(Now);
	private readonly JobHandlerRegistry _registry = new([new StubHandler()]);

	private sealed class StubHandler : IScheduledJobHandler
	{
		public string JobType => "tip-of-day";

		public Task<JobExecutionResult> ExecuteAsync(
			JobExecutionContext context,
			CancellationToken cancellationToken = default)
			=> Task.FromResult(JobExecutionResult.Ok());
	}

	private CreateJobEndpoint CreateEndpoint()
		=> Factory.Create<CreateJobEndpoint>(
			_jobRepository,
			_registry,
			_timeProvider,
			NullLogger<CreateJobEndpoint>.Instance);

	private static SaveJobRequest CreateRequest()
		=> new()
		{
			Name = "daily-tip",
			DisplayName = "Daily tip",
			JobType = "tip-of-day",
			CronExpression = "0 8 * * *",
			TimeZoneId = "UTC",
			IsEnabled = true,
			MaxRetryCount = 2,
			RetryBaseDelaySeconds = 30
		};

	[Fact]
	public async Task PersistsTheJob_GivenAValidRequest()
	{
		var ep = CreateEndpoint();

		await ep.HandleAsync(CreateRequest(), default);

		await _jobRepository.Received(1).CreateAsync(
			Arg.Is<ScheduledJobRecord>(j => j.Name == "daily-tip" && j.JobType == "tip-of-day"),
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task ComputesTheNextRunFromNow()
	{
		ScheduledJobRecord? created = null;
		await _jobRepository.CreateAsync(Arg.Do<ScheduledJobRecord>(j => created = j), Arg.Any<CancellationToken>());
		var ep = CreateEndpoint();

		await ep.HandleAsync(CreateRequest(), default);

		created!.NextRunUtc.ShouldBe(new DateTimeOffset(2026, 8, 21, 8, 0, 0, TimeSpan.Zero));
		ep.Response.NextRunUtc.ShouldBe(created.NextRunUtc);
	}

	[Fact]
	public async Task LeavesNextRunNull_GivenTheJobIsCreatedDisabled()
	{
		ScheduledJobRecord? created = null;
		await _jobRepository.CreateAsync(Arg.Do<ScheduledJobRecord>(j => created = j), Arg.Any<CancellationToken>());
		var request = CreateRequest();
		request.IsEnabled = false;
		var ep = CreateEndpoint();

		await ep.HandleAsync(request, default);

		created!.NextRunUtc.ShouldBeNull();
	}

	[Fact]
	public async Task DefaultsTheDisplayNameToTheName()
	{
		ScheduledJobRecord? created = null;
		await _jobRepository.CreateAsync(Arg.Do<ScheduledJobRecord>(j => created = j), Arg.Any<CancellationToken>());
		var request = CreateRequest();
		request.DisplayName = null;
		var ep = CreateEndpoint();

		await ep.HandleAsync(request, default);

		created!.DisplayName.ShouldBe("daily-tip");
	}

	[Fact]
	public async Task Returns400AndPersistsNothing_GivenAnUnregisteredJobType()
	{
		var request = CreateRequest();
		request.JobType = "gone-missing";
		var ep = CreateEndpoint();

		await ep.HandleAsync(request, default);

		ep.ValidationFailures.ShouldNotBeEmpty();
		await _jobRepository.DidNotReceiveWithAnyArgs().CreateAsync(default!, default);
	}

	[Fact]
	public async Task Returns400_GivenAnUnparseableCronExpression()
	{
		var request = CreateRequest();
		request.CronExpression = "not a cron";
		var ep = CreateEndpoint();

		await ep.HandleAsync(request, default);

		ep.ValidationFailures.ShouldNotBeEmpty();
		await _jobRepository.DidNotReceiveWithAnyArgs().CreateAsync(default!, default);
	}

	[Fact]
	public async Task Returns400_GivenAnUnknownTimeZone()
	{
		var request = CreateRequest();
		request.TimeZoneId = "Mars/Olympus_Mons";
		var ep = CreateEndpoint();

		await ep.HandleAsync(request, default);

		ep.ValidationFailures.ShouldNotBeEmpty();
	}

	[Fact]
	public async Task Returns409_GivenAJobWithThatNameAlreadyExists()
	{
		_jobRepository.GetByNameAsync("daily-tip", Arg.Any<CancellationToken>())
			.Returns(new ScheduledJobRecord
			{
				Name = "daily-tip",
				DisplayName = "Daily tip",
				JobType = "tip-of-day",
				CronExpression = "0 8 * * *"
			});
		var ep = CreateEndpoint();

		await ep.HandleAsync(CreateRequest(), default);

		ep.HttpContext.Response.StatusCode.ShouldBe(409);
		await _jobRepository.DidNotReceiveWithAnyArgs().CreateAsync(default!, default);
	}
}
```

Create `tests/BarretApi.Api.UnitTests/Features/Jobs/UpdateJobEndpoint_HandleAsync_Tests.cs`:

```csharp
using BarretApi.Api.Features.Jobs;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Core.Services;
using FastEndpoints;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;

namespace BarretApi.Api.UnitTests.Features.Jobs;

public sealed class UpdateJobEndpoint_HandleAsync_Tests
{
	private static readonly DateTimeOffset Now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

	private readonly IScheduledJobRepository _jobRepository = Substitute.For<IScheduledJobRepository>();
	private readonly FakeTimeProvider _timeProvider = new(Now);
	private readonly JobHandlerRegistry _registry = new([new StubHandler()]);

	private sealed class StubHandler : IScheduledJobHandler
	{
		public string JobType => "tip-of-day";

		public Task<JobExecutionResult> ExecuteAsync(
			JobExecutionContext context,
			CancellationToken cancellationToken = default)
			=> Task.FromResult(JobExecutionResult.Ok());
	}

	private UpdateJobEndpoint CreateEndpoint()
		=> Factory.Create<UpdateJobEndpoint>(
			_jobRepository,
			_registry,
			_timeProvider,
			NullLogger<UpdateJobEndpoint>.Instance);

	private static ScheduledJobRecord CreateExisting(bool isEnabled = true)
		=> new()
		{
			Name = "daily-tip",
			DisplayName = "Daily tip",
			JobType = "tip-of-day",
			CronExpression = "0 8 * * *",
			TimeZoneId = "UTC",
			IsEnabled = isEnabled,
			NextRunUtc = isEnabled ? Now.AddHours(20) : null,
			MaxRetryCount = 2,
			RetryBaseDelaySeconds = 30,
			CreatedAtUtc = Now.AddDays(-10),
			UpdatedAtUtc = Now.AddDays(-1)
		};

	private static SaveJobRequest CreateRequest(
		string cron = "0 8 * * *",
		bool isEnabled = true)
		=> new()
		{
			Name = "daily-tip",
			DisplayName = "Daily tip",
			JobType = "tip-of-day",
			CronExpression = cron,
			TimeZoneId = "UTC",
			IsEnabled = isEnabled,
			MaxRetryCount = 2,
			RetryBaseDelaySeconds = 30
		};

	[Fact]
	public async Task Returns404_GivenNoSuchJob()
	{
		_jobRepository.GetByNameAsync("daily-tip", Arg.Any<CancellationToken>())
			.Returns((ScheduledJobRecord?)null);
		var ep = CreateEndpoint();

		await ep.HandleAsync(CreateRequest(), default);

		ep.HttpContext.Response.StatusCode.ShouldBe(404);
		await _jobRepository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
	}

	[Fact]
	public async Task PersistsTheEditedFields()
	{
		var existing = CreateExisting();
		_jobRepository.GetByNameAsync("daily-tip", Arg.Any<CancellationToken>()).Returns(existing);
		var request = CreateRequest();
		request.ArgumentsJson = """{"category":"azure"}""";
		request.MaxRetryCount = 5;
		var ep = CreateEndpoint();

		await ep.HandleAsync(request, default);

		existing.ArgumentsJson.ShouldBe("""{"category":"azure"}""");
		existing.MaxRetryCount.ShouldBe(5);
		existing.UpdatedAtUtc.ShouldBe(Now);
		await _jobRepository.Received(1).UpdateAsync(existing, Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task LeavesNextRunAlone_GivenNeitherTheScheduleNorTheEnabledFlagChanged()
	{
		var existing = CreateExisting();
		var originalNextRun = existing.NextRunUtc;
		_jobRepository.GetByNameAsync("daily-tip", Arg.Any<CancellationToken>()).Returns(existing);
		var ep = CreateEndpoint();

		await ep.HandleAsync(CreateRequest(), default);

		existing.NextRunUtc.ShouldBe(originalNextRun);
	}

	[Fact]
	public async Task RecomputesNextRunFromNow_GivenTheCronExpressionChanged()
	{
		var existing = CreateExisting();
		_jobRepository.GetByNameAsync("daily-tip", Arg.Any<CancellationToken>()).Returns(existing);
		var ep = CreateEndpoint();

		await ep.HandleAsync(CreateRequest(cron: "0 15 * * *"), default);

		existing.NextRunUtc.ShouldBe(new DateTimeOffset(2026, 8, 20, 15, 0, 0, TimeSpan.Zero));
	}

	[Fact]
	public async Task RecomputesNextRunFromNow_GivenAPausedJobIsEnabled()
	{
		var existing = CreateExisting(isEnabled: false);
		_jobRepository.GetByNameAsync("daily-tip", Arg.Any<CancellationToken>()).Returns(existing);
		var ep = CreateEndpoint();

		await ep.HandleAsync(CreateRequest(isEnabled: true), default);

		existing.NextRunUtc.ShouldBe(new DateTimeOffset(2026, 8, 21, 8, 0, 0, TimeSpan.Zero));
	}

	[Fact]
	public async Task ClearsNextRun_GivenTheJobIsDisabled()
	{
		var existing = CreateExisting();
		_jobRepository.GetByNameAsync("daily-tip", Arg.Any<CancellationToken>()).Returns(existing);
		var ep = CreateEndpoint();

		await ep.HandleAsync(CreateRequest(isEnabled: false), default);

		existing.NextRunUtc.ShouldBeNull();
	}

	[Fact]
	public async Task Returns400_GivenAnUnparseableCronExpression()
	{
		_jobRepository.GetByNameAsync("daily-tip", Arg.Any<CancellationToken>()).Returns(CreateExisting());
		var ep = CreateEndpoint();

		await ep.HandleAsync(CreateRequest(cron: "not a cron"), default);

		ep.ValidationFailures.ShouldNotBeEmpty();
		await _jobRepository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
	}
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/BarretApi.Api.UnitTests --filter "FullyQualifiedName~SaveJobValidator_Tests|FullyQualifiedName~CreateJobEndpoint|FullyQualifiedName~UpdateJobEndpoint"`
Expected: build failure — `SaveJobRequest` does not exist.

- [ ] **Step 4: Write the request and validator**

Create `src/BarretApi.Api/Features/Jobs/SaveJobRequest.cs`:

```csharp
namespace BarretApi.Api.Features.Jobs;

/// <summary>
/// Body for both create and update. On update the name comes from the route and any
/// value in the body is ignored — a job cannot be renamed, because the name is its key.
/// </summary>
public sealed class SaveJobRequest
{
	public string Name { get; set; } = string.Empty;
	public string? DisplayName { get; set; }
	public string JobType { get; set; } = string.Empty;
	public string CronExpression { get; set; } = string.Empty;
	public string TimeZoneId { get; set; } = "UTC";
	public string? ArgumentsJson { get; set; }
	public bool IsEnabled { get; set; }
	public int MaxRetryCount { get; set; } = 2;
	public int RetryBaseDelaySeconds { get; set; } = 30;
}
```

Create `src/BarretApi.Api/Features/Jobs/SaveJobValidator.cs`:

```csharp
using System.Text.RegularExpressions;
using BarretApi.Core.Services.Jobs;
using FastEndpoints;
using FluentValidation;

namespace BarretApi.Api.Features.Jobs;

/// <summary>
/// Shape-only validation. Rules that need services — is the job type registered, does the
/// cron parse — run in the endpoint, which can resolve them.
/// </summary>
public sealed partial class SaveJobValidator : Validator<SaveJobRequest>
{
	public SaveJobValidator()
	{
		RuleFor(r => r.Name)
			.NotEmpty().WithMessage("Name is required.")
			.MaximumLength(100).WithMessage("Name must be 100 characters or fewer.")
			.Must(name => SlugPattern().IsMatch(name))
			.WithMessage("Name must be a slug: lowercase letters, digits, and single hyphens between them.");

		RuleFor(r => r.DisplayName)
			.MaximumLength(200).WithMessage("DisplayName must be 200 characters or fewer.");

		RuleFor(r => r.JobType)
			.NotEmpty().WithMessage("JobType is required.")
			.MaximumLength(100).WithMessage("JobType must be 100 characters or fewer.");

		RuleFor(r => r.CronExpression)
			.NotEmpty().WithMessage("CronExpression is required.")
			.MaximumLength(200).WithMessage("CronExpression must be 200 characters or fewer.");

		RuleFor(r => r.TimeZoneId)
			.MaximumLength(100).WithMessage("TimeZoneId must be 100 characters or fewer.");

		RuleFor(r => r.MaxRetryCount)
			.InclusiveBetween(0, 10).WithMessage("MaxRetryCount must be between 0 and 10.");

		RuleFor(r => r.RetryBaseDelaySeconds)
			.InclusiveBetween(0, 3_600).WithMessage("RetryBaseDelaySeconds must be between 0 and 3600.");

		RuleFor(r => r.ArgumentsJson)
			.Must(JobArguments.IsValidJson)
			.WithMessage("ArgumentsJson must be a JSON object.");
	}

	[GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
	private static partial Regex SlugPattern();
}
```

- [ ] **Step 5: Write the shared semantic validation**

Create `src/BarretApi.Api/Features/Jobs/JobRequestValidation.cs`:

```csharp
using BarretApi.Core.Services;

namespace BarretApi.Api.Features.Jobs;

internal static class JobRequestValidation
{
	/// <summary>
	/// The next occurrence after <paramref name="fromUtc"/> for a request's schedule, or
	/// null when the job is disabled. Callers validate the schedule first.
	/// </summary>
	public static DateTimeOffset? ComputeNextRun(
		SaveJobRequest request,
		DateTimeOffset fromUtc)
	{
		if (!request.IsEnabled)
		{
			return null;
		}

		return CronSchedule.TryParse(request.CronExpression, request.TimeZoneId, out var schedule, out _)
			? schedule!.GetNextOccurrence(fromUtc)
			: null;
	}
}
```

- [ ] **Step 6: Write the create endpoint**

Create `src/BarretApi.Api/Features/Jobs/CreateJobEndpoint.cs`:

```csharp
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Core.Services;
using FastEndpoints;

namespace BarretApi.Api.Features.Jobs;

public sealed class CreateJobEndpoint(
	IScheduledJobRepository jobRepository,
	JobHandlerRegistry handlerRegistry,
	TimeProvider timeProvider,
	ILogger<CreateJobEndpoint> logger)
	: Endpoint<SaveJobRequest, JobResponse>
{
	private readonly IScheduledJobRepository _jobRepository = jobRepository;
	private readonly JobHandlerRegistry _handlerRegistry = handlerRegistry;
	private readonly TimeProvider _timeProvider = timeProvider;
	private readonly ILogger<CreateJobEndpoint> _logger = logger;

	public override void Configure()
	{
		Post("/api/jobs");

		Summary(s =>
		{
			s.Summary = "Create a scheduled job";
			s.Description = "Validates the schedule before storing it and returns the computed next run time.";
			s.Responses[200] = "The job was created.";
			s.Responses[400] = "Request validation failed.";
			s.Responses[401] = "Missing or invalid X-Api-Key.";
			s.Responses[409] = "A job with that name already exists.";
		});
	}

	public override async Task HandleAsync(SaveJobRequest req, CancellationToken ct)
	{
		if (!await ValidateScheduleAsync(req, ct))
		{
			return;
		}

		var existing = await _jobRepository.GetByNameAsync(req.Name, ct);
		if (existing is not null)
		{
			await Send.ResponseAsync(JobResponseMapper.ToResponse(existing), 409, ct);
			return;
		}

		var now = _timeProvider.GetUtcNow();
		var job = new ScheduledJobRecord
		{
			Name = req.Name.Trim(),
			DisplayName = string.IsNullOrWhiteSpace(req.DisplayName) ? req.Name.Trim() : req.DisplayName.Trim(),
			JobType = req.JobType.Trim(),
			CronExpression = req.CronExpression.Trim(),
			TimeZoneId = string.IsNullOrWhiteSpace(req.TimeZoneId) ? "UTC" : req.TimeZoneId.Trim(),
			ArgumentsJson = req.ArgumentsJson,
			IsEnabled = req.IsEnabled,
			MaxRetryCount = req.MaxRetryCount,
			RetryBaseDelaySeconds = req.RetryBaseDelaySeconds,
			NextRunUtc = JobRequestValidation.ComputeNextRun(req, now),
			RunState = JobRunState.Idle,
			CreatedAtUtc = now,
			UpdatedAtUtc = now
		};

		await _jobRepository.CreateAsync(job, ct);
		_logger.LogInformation(
			"Created job {JobName} ({JobType}); next run {NextRunUtc}.",
			job.Name,
			job.JobType,
			job.NextRunUtc);

		await Send.OkAsync(JobResponseMapper.ToResponse(job), ct);
	}

	/// <summary>
	/// Rules the shape validator cannot express because they need the handler registry
	/// and the cron parser. Adds errors and sends a 400 when anything fails.
	/// </summary>
	private async Task<bool> ValidateScheduleAsync(SaveJobRequest req, CancellationToken ct)
	{
		if (!_handlerRegistry.IsRegistered(req.JobType))
		{
			AddError(
				r => r.JobType,
				$"'{req.JobType}' is not a registered job type. Call GET /api/jobs/types for the list.");
		}

		if (!CronSchedule.TryParse(req.CronExpression, req.TimeZoneId, out _, out var scheduleError))
		{
			AddError(r => r.CronExpression, scheduleError ?? "The schedule is not valid.");
		}

		if (ValidationFailed)
		{
			await Send.ErrorsAsync(400, ct);
			return false;
		}

		return true;
	}
}
```

- [ ] **Step 7: Write the update endpoint**

Create `src/BarretApi.Api/Features/Jobs/UpdateJobEndpoint.cs`:

```csharp
using BarretApi.Core.Interfaces;
using BarretApi.Core.Services;
using FastEndpoints;

namespace BarretApi.Api.Features.Jobs;

public sealed class UpdateJobEndpoint(
	IScheduledJobRepository jobRepository,
	JobHandlerRegistry handlerRegistry,
	TimeProvider timeProvider,
	ILogger<UpdateJobEndpoint> logger)
	: Endpoint<SaveJobRequest, JobResponse>
{
	private readonly IScheduledJobRepository _jobRepository = jobRepository;
	private readonly JobHandlerRegistry _handlerRegistry = handlerRegistry;
	private readonly TimeProvider _timeProvider = timeProvider;
	private readonly ILogger<UpdateJobEndpoint> _logger = logger;

	public override void Configure()
	{
		Put("/api/jobs/{Name}");

		Summary(s =>
		{
			s.Summary = "Update a scheduled job";
			s.Description = "Changing the schedule, the time zone, or enabling a paused job recomputes the next run time from now, so a long-paused job does not fire immediately on resume.";
			s.Responses[200] = "The job was updated.";
			s.Responses[400] = "Request validation failed.";
			s.Responses[401] = "Missing or invalid X-Api-Key.";
			s.Responses[404] = "No job with that name exists.";
		});
	}

	public override async Task HandleAsync(SaveJobRequest req, CancellationToken ct)
	{
		if (!_handlerRegistry.IsRegistered(req.JobType))
		{
			AddError(
				r => r.JobType,
				$"'{req.JobType}' is not a registered job type. Call GET /api/jobs/types for the list.");
		}

		if (!CronSchedule.TryParse(req.CronExpression, req.TimeZoneId, out _, out var scheduleError))
		{
			AddError(r => r.CronExpression, scheduleError ?? "The schedule is not valid.");
		}

		if (ValidationFailed)
		{
			await Send.ErrorsAsync(400, ct);
			return;
		}

		var job = await _jobRepository.GetByNameAsync(req.Name, ct);
		if (job is null)
		{
			await Send.NotFoundAsync(ct);
			return;
		}

		var now = _timeProvider.GetUtcNow();
		var scheduleChanged =
			!string.Equals(job.CronExpression, req.CronExpression.Trim(), StringComparison.Ordinal)
			|| !string.Equals(job.TimeZoneId, req.TimeZoneId.Trim(), StringComparison.OrdinalIgnoreCase);
		var wasEnabled = job.IsEnabled;

		job.DisplayName = string.IsNullOrWhiteSpace(req.DisplayName) ? job.Name : req.DisplayName.Trim();
		job.JobType = req.JobType.Trim();
		job.CronExpression = req.CronExpression.Trim();
		job.TimeZoneId = string.IsNullOrWhiteSpace(req.TimeZoneId) ? "UTC" : req.TimeZoneId.Trim();
		job.ArgumentsJson = req.ArgumentsJson;
		job.IsEnabled = req.IsEnabled;
		job.MaxRetryCount = req.MaxRetryCount;
		job.RetryBaseDelaySeconds = req.RetryBaseDelaySeconds;
		job.UpdatedAtUtc = now;

		if (!req.IsEnabled)
		{
			job.NextRunUtc = null;
		}
		else if (scheduleChanged || !wasEnabled || job.NextRunUtc is null)
		{
			job.NextRunUtc = JobRequestValidation.ComputeNextRun(req, now);
		}

		await _jobRepository.UpdateAsync(job, ct);
		_logger.LogInformation(
			"Updated job {JobName}; enabled={IsEnabled}, next run {NextRunUtc}.",
			job.Name,
			job.IsEnabled,
			job.NextRunUtc);

		await Send.OkAsync(JobResponseMapper.ToResponse(job), ct);
	}
}
```

- [ ] **Step 8: Run the tests to verify they pass**

Run: `dotnet test tests/BarretApi.Api.UnitTests --filter "FullyQualifiedName~Features.Jobs"`
Expected: PASS — the 8 tests from Task 13 plus 12 validator tests and 15 endpoint tests.

If `ValidationFailed` or `AddError` do not resolve on `Endpoint<TRequest, TResponse>`, check `CreateSocialPostValidator` and the endpoints under `Features/SocialPost` for the exact FastEndpoints 8.2 spelling used in this project.

- [ ] **Step 9: Format and commit**

```bash
dotnet format --include src/BarretApi.Api/Features/Jobs/SaveJobRequest.cs src/BarretApi.Api/Features/Jobs/SaveJobValidator.cs src/BarretApi.Api/Features/Jobs/JobRequestValidation.cs src/BarretApi.Api/Features/Jobs/CreateJobEndpoint.cs src/BarretApi.Api/Features/Jobs/UpdateJobEndpoint.cs
git add src/BarretApi.Api/Features/Jobs tests/BarretApi.Api.UnitTests/Features/Jobs
git commit -m "feat: add job create and update endpoints with schedule validation"
```

---

### Task 15: Manual run, run history, and job types endpoints

**Files:**
- Create: `src/BarretApi.Api/Features/Jobs/RunJobEndpoint.cs`
- Create: `src/BarretApi.Api/Features/Jobs/JobRunResponse.cs`
- Create: `src/BarretApi.Api/Features/Jobs/ListJobRunsRequest.cs`
- Create: `src/BarretApi.Api/Features/Jobs/ListJobRunsEndpoint.cs`
- Create: `src/BarretApi.Api/Features/Jobs/ListJobTypesEndpoint.cs`
- Test: `tests/BarretApi.Api.UnitTests/Features/Jobs/RunJobEndpoint_HandleAsync_Tests.cs`
- Test: `tests/BarretApi.Api.UnitTests/Features/Jobs/ListJobRunsEndpoint_HandleAsync_Tests.cs`
- Test: `tests/BarretApi.Api.UnitTests/Features/Jobs/ListJobTypesEndpoint_HandleAsync_Tests.cs`

**Interfaces:**
- Consumes: `JobDispatcher.RunManuallyAsync` and `ManualRunResult` / `ManualRunOutcome` (Task 6), `IJobRunRepository.GetByJobAsync` (Tasks 4, 8), `JobHandlerRegistry.RegisteredTypes` (Task 3), `GetJobRequest` (Task 13).
- Produces:
  - `JobRunResponse` and `JobRunListResponse(IReadOnlyList<JobRunResponse> Runs)`.
  - `ListJobRunsRequest { string Name; int? MaxCount; }`.
  - `JobTypesResponse { IReadOnlyList<string> JobTypes }`.

- [ ] **Step 1: Write the failing tests**

Create `tests/BarretApi.Api.UnitTests/Features/Jobs/RunJobEndpoint_HandleAsync_Tests.cs`:

```csharp
using BarretApi.Api.Features.Jobs;
using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Core.Services;
using FastEndpoints;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;

namespace BarretApi.Api.UnitTests.Features.Jobs;

public sealed class RunJobEndpoint_HandleAsync_Tests
{
	private static readonly DateTimeOffset Now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

	private readonly IScheduledJobRepository _jobRepository = Substitute.For<IScheduledJobRepository>();
	private readonly IJobRunRepository _runRepository = Substitute.For<IJobRunRepository>();
	private readonly FakeTimeProvider _timeProvider = new(Now);

	private sealed class StubHandler(bool succeeds) : IScheduledJobHandler
	{
		public string JobType => "tip-of-day";

		public Task<JobExecutionResult> ExecuteAsync(
			JobExecutionContext context,
			CancellationToken cancellationToken = default)
			=> Task.FromResult(succeeds
				? JobExecutionResult.Ok("posted")
				: JobExecutionResult.Fail("platform rejected the post"));
	}

	private JobDispatcher CreateDispatcher(bool handlerSucceeds = true)
		=> new(
			_jobRepository,
			_runRepository,
			new JobHandlerRegistry([new StubHandler(handlerSucceeds)]),
			Options.Create(new JobSchedulerOptions
			{
				TableStorage = new JobSchedulerTableStorageOptions { ConnectionString = "UseDevelopmentStorage=true" }
			}),
			_timeProvider,
			NullLogger<JobDispatcher>.Instance);

	private RunJobEndpoint CreateEndpoint(bool handlerSucceeds = true)
		=> Factory.Create<RunJobEndpoint>(
			CreateDispatcher(handlerSucceeds),
			NullLogger<RunJobEndpoint>.Instance);

	private static ScheduledJobRecord CreateJob()
		=> new()
		{
			Name = "daily-tip",
			DisplayName = "Daily tip",
			JobType = "tip-of-day",
			CronExpression = "0 8 * * *",
			TimeZoneId = "UTC",
			IsEnabled = true,
			NextRunUtc = Now.AddHours(20),
			MaxRetryCount = 0,
			RetryBaseDelaySeconds = 0,
			CreatedAtUtc = Now,
			UpdatedAtUtc = Now,
			ETag = "etag-1"
		};

	private void AllowClaim()
		=> _jobRepository.TryClaimAsync(Arg.Any<ScheduledJobRecord>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
			.Returns(callInfo =>
			{
				var job = callInfo.Arg<ScheduledJobRecord>();
				job.RunState = JobRunState.Running;
				job.ClaimedAtUtc = callInfo.ArgAt<DateTimeOffset>(1);
				return true;
			});

	[Fact]
	public async Task Returns404_GivenNoSuchJob()
	{
		_jobRepository.GetByNameAsync("missing", Arg.Any<CancellationToken>())
			.Returns((ScheduledJobRecord?)null);
		var ep = CreateEndpoint();

		await ep.HandleAsync(new GetJobRequest { Name = "missing" }, default);

		ep.HttpContext.Response.StatusCode.ShouldBe(404);
	}

	[Fact]
	public async Task Returns409_GivenTheJobIsAlreadyRunning()
	{
		var job = CreateJob();
		job.RunState = JobRunState.Running;
		job.ClaimedAtUtc = Now.AddMinutes(-1);
		_jobRepository.GetByNameAsync("daily-tip", Arg.Any<CancellationToken>()).Returns(job);
		var ep = CreateEndpoint();

		await ep.HandleAsync(new GetJobRequest { Name = "daily-tip" }, default);

		ep.HttpContext.Response.StatusCode.ShouldBe(409);
	}

	[Fact]
	public async Task Returns200WithTheRun_GivenTheRunSucceeded()
	{
		_jobRepository.GetByNameAsync("daily-tip", Arg.Any<CancellationToken>()).Returns(CreateJob());
		AllowClaim();
		var ep = CreateEndpoint();

		await ep.HandleAsync(new GetJobRequest { Name = "daily-tip" }, default);

		ep.Response.Status.ShouldBe("Succeeded");
		ep.Response.TriggerType.ShouldBe("Manual");
		ep.Response.JobName.ShouldBe("daily-tip");
	}

	[Fact]
	public async Task Returns502_GivenTheRunFailed()
	{
		_jobRepository.GetByNameAsync("daily-tip", Arg.Any<CancellationToken>()).Returns(CreateJob());
		AllowClaim();
		var ep = CreateEndpoint(handlerSucceeds: false);

		await ep.HandleAsync(new GetJobRequest { Name = "daily-tip" }, default);

		ep.HttpContext.Response.StatusCode.ShouldBe(502);
	}

	[Fact]
	public async Task LeavesTheScheduleAlone()
	{
		var job = CreateJob();
		var originalNextRun = job.NextRunUtc;
		_jobRepository.GetByNameAsync("daily-tip", Arg.Any<CancellationToken>()).Returns(job);
		AllowClaim();
		var ep = CreateEndpoint();

		await ep.HandleAsync(new GetJobRequest { Name = "daily-tip" }, default);

		job.NextRunUtc.ShouldBe(originalNextRun);
	}
}
```

Create `tests/BarretApi.Api.UnitTests/Features/Jobs/ListJobRunsEndpoint_HandleAsync_Tests.cs`:

```csharp
using BarretApi.Api.Features.Jobs;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using FastEndpoints;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace BarretApi.Api.UnitTests.Features.Jobs;

public sealed class ListJobRunsEndpoint_HandleAsync_Tests
{
	private static readonly DateTimeOffset Now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

	private readonly IScheduledJobRepository _jobRepository = Substitute.For<IScheduledJobRepository>();
	private readonly IJobRunRepository _runRepository = Substitute.For<IJobRunRepository>();

	private ListJobRunsEndpoint CreateEndpoint()
		=> Factory.Create<ListJobRunsEndpoint>(
			_jobRepository,
			_runRepository,
			NullLogger<ListJobRunsEndpoint>.Instance);

	private static ScheduledJobRecord CreateJob()
		=> new()
		{
			Name = "daily-tip",
			DisplayName = "Daily tip",
			JobType = "tip-of-day",
			CronExpression = "0 8 * * *",
			CreatedAtUtc = Now,
			UpdatedAtUtc = Now
		};

	private static JobRunRecord CreateRun(string runId = "run-1")
		=> new()
		{
			RunId = runId,
			JobName = "daily-tip",
			JobType = "tip-of-day",
			TriggerType = JobTriggerType.Scheduled,
			ScheduledForUtc = Now.AddMinutes(-1),
			StartedAtUtc = Now,
			CompletedAtUtc = Now.AddSeconds(2),
			DurationMs = 2_000,
			Status = JobRunStatus.Succeeded,
			AttemptCount = 1,
			Summary = "posted"
		};

	[Fact]
	public async Task Returns404_GivenNoSuchJob()
	{
		_jobRepository.GetByNameAsync("missing", Arg.Any<CancellationToken>())
			.Returns((ScheduledJobRecord?)null);
		var ep = CreateEndpoint();

		await ep.HandleAsync(new ListJobRunsRequest { Name = "missing" }, default);

		ep.HttpContext.Response.StatusCode.ShouldBe(404);
	}

	[Fact]
	public async Task ReturnsTheRunsForTheJob()
	{
		_jobRepository.GetByNameAsync("daily-tip", Arg.Any<CancellationToken>()).Returns(CreateJob());
		_runRepository.GetByJobAsync("daily-tip", Arg.Any<int>(), Arg.Any<CancellationToken>())
			.Returns([CreateRun("run-2"), CreateRun("run-1")]);
		var ep = CreateEndpoint();

		await ep.HandleAsync(new ListJobRunsRequest { Name = "daily-tip" }, default);

		ep.Response.Runs.Count.ShouldBe(2);
		ep.Response.Runs[0].RunId.ShouldBe("run-2");
		ep.Response.Runs[0].Status.ShouldBe("Succeeded");
		ep.Response.Runs[0].TriggerType.ShouldBe("Scheduled");
		ep.Response.Runs[0].DurationMs.ShouldBe(2_000);
	}

	[Fact]
	public async Task DefaultsToFiftyRuns()
	{
		_jobRepository.GetByNameAsync("daily-tip", Arg.Any<CancellationToken>()).Returns(CreateJob());
		_runRepository.GetByJobAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
			.Returns([]);
		var ep = CreateEndpoint();

		await ep.HandleAsync(new ListJobRunsRequest { Name = "daily-tip" }, default);

		await _runRepository.Received(1).GetByJobAsync("daily-tip", 50, Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task ClampsMaxCountToTheAllowedRange()
	{
		_jobRepository.GetByNameAsync("daily-tip", Arg.Any<CancellationToken>()).Returns(CreateJob());
		_runRepository.GetByJobAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
			.Returns([]);
		var ep = CreateEndpoint();

		await ep.HandleAsync(new ListJobRunsRequest { Name = "daily-tip", MaxCount = 5_000 }, default);

		await _runRepository.Received(1).GetByJobAsync("daily-tip", 500, Arg.Any<CancellationToken>());
	}
}
```

Create `tests/BarretApi.Api.UnitTests/Features/Jobs/ListJobTypesEndpoint_HandleAsync_Tests.cs`:

```csharp
using BarretApi.Api.Features.Jobs;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Core.Services;
using FastEndpoints;
using Shouldly;

namespace BarretApi.Api.UnitTests.Features.Jobs;

public sealed class ListJobTypesEndpoint_HandleAsync_Tests
{
	private sealed class StubHandler(string jobType) : IScheduledJobHandler
	{
		public string JobType { get; } = jobType;

		public Task<JobExecutionResult> ExecuteAsync(
			JobExecutionContext context,
			CancellationToken cancellationToken = default)
			=> Task.FromResult(JobExecutionResult.Ok());
	}

	[Fact]
	public async Task ReturnsEveryRegisteredJobTypeSorted()
	{
		var registry = new JobHandlerRegistry([new StubHandler("tip-of-day"), new StubHandler("nasa-apod")]);
		var ep = Factory.Create<ListJobTypesEndpoint>(registry);

		await ep.HandleAsync(default);

		ep.Response.JobTypes.ShouldBe(["nasa-apod", "tip-of-day"]);
	}

	[Fact]
	public async Task ReturnsAnEmptyList_GivenNoHandlersAreRegistered()
	{
		var ep = Factory.Create<ListJobTypesEndpoint>(new JobHandlerRegistry([]));

		await ep.HandleAsync(default);

		ep.Response.JobTypes.ShouldBeEmpty();
	}
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/BarretApi.Api.UnitTests --filter "FullyQualifiedName~RunJobEndpoint|FullyQualifiedName~ListJobRunsEndpoint|FullyQualifiedName~ListJobTypesEndpoint"`
Expected: build failure — the endpoints do not exist.

- [ ] **Step 3: Write the run response DTOs**

Create `src/BarretApi.Api/Features/Jobs/JobRunResponse.cs`:

```csharp
using BarretApi.Core.Models;

namespace BarretApi.Api.Features.Jobs;

public sealed class JobRunResponse
{
	public required string RunId { get; init; }
	public required string JobName { get; init; }
	public required string JobType { get; init; }
	public required string TriggerType { get; init; }
	public DateTimeOffset? ScheduledForUtc { get; init; }
	public required DateTimeOffset StartedAtUtc { get; init; }
	public DateTimeOffset? CompletedAtUtc { get; init; }
	public long DurationMs { get; init; }
	public required string Status { get; init; }
	public int AttemptCount { get; init; }
	public string? Summary { get; init; }
	public string? ErrorMessage { get; init; }

	public static JobRunResponse FromRecord(JobRunRecord run)
	{
		return new JobRunResponse
		{
			RunId = run.RunId,
			JobName = run.JobName,
			JobType = run.JobType,
			TriggerType = run.TriggerType.ToString(),
			ScheduledForUtc = run.ScheduledForUtc,
			StartedAtUtc = run.StartedAtUtc,
			CompletedAtUtc = run.CompletedAtUtc,
			DurationMs = run.DurationMs,
			Status = run.Status.ToString(),
			AttemptCount = run.AttemptCount,
			Summary = run.Summary,
			ErrorMessage = run.ErrorMessage
		};
	}
}

public sealed class JobRunListResponse
{
	public required IReadOnlyList<JobRunResponse> Runs { get; init; }
}

public sealed class JobTypesResponse
{
	public required IReadOnlyList<string> JobTypes { get; init; }
}
```

Create `src/BarretApi.Api/Features/Jobs/ListJobRunsRequest.cs`:

```csharp
namespace BarretApi.Api.Features.Jobs;

public sealed class ListJobRunsRequest
{
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// How many runs to return, newest first. Defaults to 50 and is clamped to 500.
	/// </summary>
	public int? MaxCount { get; set; }
}
```

- [ ] **Step 4: Write the manual run endpoint**

Create `src/BarretApi.Api/Features/Jobs/RunJobEndpoint.cs`:

```csharp
using BarretApi.Core.Models;
using BarretApi.Core.Services;
using FastEndpoints;

namespace BarretApi.Api.Features.Jobs;

public sealed class RunJobEndpoint(
	JobDispatcher dispatcher,
	ILogger<RunJobEndpoint> logger)
	: Endpoint<GetJobRequest, JobRunResponse>
{
	private readonly JobDispatcher _dispatcher = dispatcher;
	private readonly ILogger<RunJobEndpoint> _logger = logger;

	public override void Configure()
	{
		Post("/api/jobs/{Name}/run");

		Summary(s =>
		{
			s.Summary = "Run a job immediately";
			s.Description = "Runs the job out of band without changing its next run time. Works on a disabled job. Returns 409 if a run is already in progress.";
			s.Responses[200] = "The run completed successfully.";
			s.Responses[401] = "Missing or invalid X-Api-Key.";
			s.Responses[404] = "No job with that name exists.";
			s.Responses[409] = "A run is already in progress for this job.";
			s.Responses[502] = "The run completed but the job failed.";
		});
	}

	public override async Task HandleAsync(GetJobRequest req, CancellationToken ct)
	{
		var result = await _dispatcher.RunManuallyAsync(req.Name, ct);

		switch (result.Outcome)
		{
			case ManualRunOutcome.NotFound:
				await Send.NotFoundAsync(ct);
				return;

			case ManualRunOutcome.Busy:
				_logger.LogInformation("Manual run of job {JobName} was rejected: already running.", req.Name);
				await Send.ResponseAsync(
					new JobRunResponse
					{
						RunId = string.Empty,
						JobName = req.Name,
						JobType = string.Empty,
						TriggerType = nameof(JobTriggerType.Manual),
						StartedAtUtc = default,
						Status = "Busy",
						ErrorMessage = "A run is already in progress for this job."
					},
					409,
					ct);
				return;

			default:
				var run = result.Run!;
				var statusCode = run.Status == JobRunStatus.Succeeded ? 200 : 502;
				await Send.ResponseAsync(JobRunResponse.FromRecord(run), statusCode, ct);
				return;
		}
	}
}
```

- [ ] **Step 5: Write the run history and job types endpoints**

Create `src/BarretApi.Api/Features/Jobs/ListJobRunsEndpoint.cs`:

```csharp
using BarretApi.Core.Interfaces;
using FastEndpoints;

namespace BarretApi.Api.Features.Jobs;

public sealed class ListJobRunsEndpoint(
	IScheduledJobRepository jobRepository,
	IJobRunRepository runRepository,
	ILogger<ListJobRunsEndpoint> logger)
	: Endpoint<ListJobRunsRequest, JobRunListResponse>
{
	private const int DefaultMaxCount = 50;
	private const int MaximumMaxCount = 500;

	private readonly IScheduledJobRepository _jobRepository = jobRepository;
	private readonly IJobRunRepository _runRepository = runRepository;
	private readonly ILogger<ListJobRunsEndpoint> _logger = logger;

	public override void Configure()
	{
		Get("/api/jobs/{Name}/runs");

		Summary(s =>
		{
			s.Summary = "List a job's run history";
			s.Description = "Returns runs newest first. Runs age out after the configured retention window.";
			s.Responses[200] = "The run history.";
			s.Responses[401] = "Missing or invalid X-Api-Key.";
			s.Responses[404] = "No job with that name exists.";
		});
	}

	public override async Task HandleAsync(ListJobRunsRequest req, CancellationToken ct)
	{
		var job = await _jobRepository.GetByNameAsync(req.Name, ct);
		if (job is null)
		{
			await Send.NotFoundAsync(ct);
			return;
		}

		var maxCount = Math.Clamp(req.MaxCount ?? DefaultMaxCount, 1, MaximumMaxCount);
		var runs = await _runRepository.GetByJobAsync(req.Name, maxCount, ct);

		_logger.LogDebug("Returning {RunCount} runs for job {JobName}.", runs.Count, req.Name);

		await Send.OkAsync(
			new JobRunListResponse { Runs = [.. runs.Select(JobRunResponse.FromRecord)] },
			ct);
	}
}
```

Create `src/BarretApi.Api/Features/Jobs/ListJobTypesEndpoint.cs`:

```csharp
using BarretApi.Core.Services;
using FastEndpoints;

namespace BarretApi.Api.Features.Jobs;

public sealed class ListJobTypesEndpoint(JobHandlerRegistry handlerRegistry)
	: EndpointWithoutRequest<JobTypesResponse>
{
	private readonly JobHandlerRegistry _handlerRegistry = handlerRegistry;

	public override void Configure()
	{
		Get("/api/jobs/types");

		Summary(s =>
		{
			s.Summary = "List registered job types";
			s.Description = "The values accepted for a job definition's jobType. Adding a new one requires a code change and a deploy.";
			s.Responses[200] = "The registered job types.";
			s.Responses[401] = "Missing or invalid X-Api-Key.";
		});
	}

	public override async Task HandleAsync(CancellationToken ct)
	{
		await Send.OkAsync(
			new JobTypesResponse { JobTypes = _handlerRegistry.RegisteredTypes },
			ct);
	}
}
```

Route ordering note: `/api/jobs/types` and `/api/jobs/{Name}` both match `GET /api/jobs/types`. Verify after wiring that `GET /api/jobs/types` returns the type list rather than a 404 from `GetJobEndpoint`. If the wildcard wins, either give the literal route a higher priority (`Options(x => x.WithOrder(-1))`) or move the endpoint to `/api/job-types` and update the documentation in Task 17 to match.

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test tests/BarretApi.Api.UnitTests --filter "FullyQualifiedName~Features.Jobs"`
Expected: PASS — 11 new tests plus everything from Tasks 13 and 14.

- [ ] **Step 7: Format and commit**

```bash
dotnet format --include src/BarretApi.Api/Features/Jobs/RunJobEndpoint.cs src/BarretApi.Api/Features/Jobs/JobRunResponse.cs src/BarretApi.Api/Features/Jobs/ListJobRunsRequest.cs src/BarretApi.Api/Features/Jobs/ListJobRunsEndpoint.cs src/BarretApi.Api/Features/Jobs/ListJobTypesEndpoint.cs
git add src/BarretApi.Api/Features/Jobs tests/BarretApi.Api.UnitTests/Features/Jobs
git commit -m "feat: add manual run, run history, and job types endpoints"
```

---

### Task 16: Seed the built-in purge job on startup

**Files:**
- Create: `src/BarretApi.Api/Scheduling/BuiltInJobSeeder.cs`
- Modify: `src/BarretApi.Api/Program.cs`
- Test: `tests/BarretApi.Api.UnitTests/Scheduling/BuiltInJobSeeder_Tests.cs`

**Interfaces:**
- Consumes: `IScheduledJobRepository` (Task 4), `ScheduledJobRecord` (Task 2), `CronSchedule` (Task 2), `TimeProvider`.
- Produces: `BuiltInJobSeeder` with `Task SeedAsync(CancellationToken cancellationToken = default)`.

The seeder creates `purge-job-runs` if it is absent and otherwise leaves it alone — a paused or retuned purge job must survive a restart.

- [ ] **Step 1: Write the failing tests**

Create `tests/BarretApi.Api.UnitTests/Scheduling/BuiltInJobSeeder_Tests.cs`:

```csharp
using BarretApi.Api.Scheduling;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;

namespace BarretApi.Api.UnitTests.Scheduling;

public sealed class BuiltInJobSeeder_Tests
{
	private static readonly DateTimeOffset Now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

	private readonly IScheduledJobRepository _jobRepository = Substitute.For<IScheduledJobRepository>();
	private readonly FakeTimeProvider _timeProvider = new(Now);

	private BuiltInJobSeeder CreateSut()
		=> new(_jobRepository, _timeProvider, NullLogger<BuiltInJobSeeder>.Instance);

	[Fact]
	public async Task CreatesThePurgeJob_GivenItDoesNotExist()
	{
		_jobRepository.GetByNameAsync("purge-job-runs", Arg.Any<CancellationToken>())
			.Returns((ScheduledJobRecord?)null);

		await CreateSut().SeedAsync();

		await _jobRepository.Received(1).CreateAsync(
			Arg.Is<ScheduledJobRecord>(j =>
				j.Name == "purge-job-runs"
				&& j.JobType == "purge-job-runs"
				&& j.CronExpression == "0 3 * * *"
				&& j.TimeZoneId == "UTC"
				&& j.IsEnabled),
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task SetsTheNextRunFromNow()
	{
		ScheduledJobRecord? created = null;
		_jobRepository.GetByNameAsync("purge-job-runs", Arg.Any<CancellationToken>())
			.Returns((ScheduledJobRecord?)null);
		await _jobRepository.CreateAsync(Arg.Do<ScheduledJobRecord>(j => created = j), Arg.Any<CancellationToken>());

		await CreateSut().SeedAsync();

		created!.NextRunUtc.ShouldBe(new DateTimeOffset(2026, 8, 21, 3, 0, 0, TimeSpan.Zero));
	}

	[Fact]
	public async Task LeavesAnExistingPurgeJobAlone()
	{
		_jobRepository.GetByNameAsync("purge-job-runs", Arg.Any<CancellationToken>())
			.Returns(new ScheduledJobRecord
			{
				Name = "purge-job-runs",
				DisplayName = "Purge job run history",
				JobType = "purge-job-runs",
				CronExpression = "0 5 * * *",
				IsEnabled = false
			});

		await CreateSut().SeedAsync();

		await _jobRepository.DidNotReceiveWithAnyArgs().CreateAsync(default!, default);
		await _jobRepository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
	}

	[Fact]
	public async Task DoesNotThrow_GivenStorageIsUnavailable()
	{
		_jobRepository.GetByNameAsync("purge-job-runs", Arg.Any<CancellationToken>())
			.Returns<ScheduledJobRecord?>(_ => throw new InvalidOperationException("storage down"));

		await Should.NotThrowAsync(() => CreateSut().SeedAsync());
	}
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/BarretApi.Api.UnitTests --filter "FullyQualifiedName~BuiltInJobSeeder_Tests"`
Expected: build failure — `BuiltInJobSeeder` does not exist.

- [ ] **Step 3: Write the seeder**

Create `src/BarretApi.Api/Scheduling/BuiltInJobSeeder.cs`:

```csharp
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Core.Services;

namespace BarretApi.Api.Scheduling;

/// <summary>
/// Creates the job definitions the scheduler ships with. Existing rows are never
/// overwritten, so pausing or retuning a built-in job survives a restart.
/// </summary>
public sealed class BuiltInJobSeeder(
	IScheduledJobRepository jobRepository,
	TimeProvider timeProvider,
	ILogger<BuiltInJobSeeder> logger)
{
	private const string PurgeJobName = "purge-job-runs";
	private const string PurgeCronExpression = "0 3 * * *";

	private readonly IScheduledJobRepository _jobRepository = jobRepository;
	private readonly TimeProvider _timeProvider = timeProvider;
	private readonly ILogger<BuiltInJobSeeder> _logger = logger;

	public async Task SeedAsync(CancellationToken cancellationToken = default)
	{
		try
		{
			var existing = await _jobRepository.GetByNameAsync(PurgeJobName, cancellationToken);
			if (existing is not null)
			{
				return;
			}

			var now = _timeProvider.GetUtcNow();
			CronSchedule.TryParse(PurgeCronExpression, "UTC", out var schedule, out _);

			var job = new ScheduledJobRecord
			{
				Name = PurgeJobName,
				DisplayName = "Purge job run history",
				JobType = PurgeJobName,
				CronExpression = PurgeCronExpression,
				TimeZoneId = "UTC",
				IsEnabled = true,
				NextRunUtc = schedule?.GetNextOccurrence(now),
				MaxRetryCount = 1,
				RetryBaseDelaySeconds = 30,
				RunState = JobRunState.Idle,
				CreatedAtUtc = now,
				UpdatedAtUtc = now
			};

			await _jobRepository.CreateAsync(job, cancellationToken);
			_logger.LogInformation("Seeded the built-in {JobName} job; next run {NextRunUtc}.", job.Name, job.NextRunUtc);
		}
		catch (Exception ex)
		{
			// Seeding is a convenience. A storage outage must not stop the API from starting;
			// the job can be created by hand through POST /api/jobs.
			_logger.LogError(ex, "Failed to seed built-in jobs. The API will start without them.");
		}
	}
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/BarretApi.Api.UnitTests --filter "FullyQualifiedName~BuiltInJobSeeder_Tests"`
Expected: PASS, 4 tests.

- [ ] **Step 5: Run the seeder at startup**

In `src/BarretApi.Api/Program.cs`, register the seeder beside the other scheduler services:

```csharp
builder.Services.AddSingleton<BuiltInJobSeeder>();
```

and invoke it after the app is built but before `app.Run()`, next to the other startup work:

```csharp
await app.Services.GetRequiredService<BuiltInJobSeeder>().SeedAsync();
```

If `Program.cs` ends with `app.Run();` rather than `await app.RunAsync();`, the `await` above still compiles — top-level statements support it.

- [ ] **Step 6: Format and commit**

```bash
dotnet format --include src/BarretApi.Api/Scheduling/BuiltInJobSeeder.cs src/BarretApi.Api/Program.cs tests/BarretApi.Api.UnitTests/Scheduling/BuiltInJobSeeder_Tests.cs
git add src/BarretApi.Api/Scheduling src/BarretApi.Api/Program.cs tests/BarretApi.Api.UnitTests/Scheduling
git commit -m "feat: seed the built-in purge job on startup"
```

---

### Task 17: Documentation and end-to-end verification

**Files:**
- Create: `docs/JOB_SCHEDULER.md`
- Modify: `README.md`

**Interfaces:**
- Consumes: everything built in Tasks 1–16.
- Produces: no code.

- [ ] **Step 1: Verify the whole build and suite**

Run: `dotnet build`
Expected: succeeds, zero warnings.

Run: `dotnet test`
Expected: every test added by this plan passes. The ~25 pre-existing Nasa failures noted in the Global Constraints remain — confirm the count matches the baseline on `main` rather than assuming.

Record the actual numbers; do not claim the suite is green if it is not.

- [ ] **Step 2: Verify the scheduler end to end against Azurite**

Start the app: `dotnet run --project src/BarretApi.AppHost`

With the scheduler disabled (the local default), confirm the management endpoints still work:

```bash
curl -s -H "X-Api-Key: $API_KEY" http://localhost:<port>/api/jobs/types
curl -s -H "X-Api-Key: $API_KEY" http://localhost:<port>/api/jobs
```

Expected: the seven job types, and a job list containing `purge-job-runs`.

Create a job and confirm the computed next run:

```bash
curl -s -X POST -H "X-Api-Key: $API_KEY" -H "Content-Type: application/json" \
  -d '{"name":"test-tip","jobType":"tip-of-day","cronExpression":"0 8 * * *","timeZoneId":"America/Chicago","argumentsJson":"{\"category\":\"dotnet\"}","isEnabled":true}' \
  http://localhost:<port>/api/jobs
```

Expected: `200` with `nextRunUtc` at 13:00Z (summer) or 14:00Z (winter) on the next day that 08:00 Chicago falls after now.

Confirm invalid input is rejected before storage:

```bash
curl -s -o /dev/null -w "%{http_code}\n" -X POST -H "X-Api-Key: $API_KEY" -H "Content-Type: application/json" \
  -d '{"name":"bad-cron","jobType":"tip-of-day","cronExpression":"not a cron","isEnabled":true}' \
  http://localhost:<port>/api/jobs
```

Expected: `400`.

Confirm the run history records a manual run, then clean up:

```bash
curl -s -X POST -H "X-Api-Key: $API_KEY" http://localhost:<port>/api/jobs/test-tip/run
curl -s -H "X-Api-Key: $API_KEY" http://localhost:<port>/api/jobs/test-tip/runs
curl -s -X DELETE -H "X-Api-Key: $API_KEY" http://localhost:<port>/api/jobs/test-tip
```

Expected: a run tagged `Manual`, visible in the history, and the job's `nextRunUtc` unchanged from the create response.

Also confirm the route-ordering caveat from Task 15: `GET /api/jobs/types` must return the type list, not a 404.

- [ ] **Step 3: Write the user documentation**

Create `docs/JOB_SCHEDULER.md` covering, in this order:

1. **What it is** — an in-process scheduler that replaces the Power Automate flows; job definitions live in Azure Table Storage and are managed over the API.
2. **Prerequisite** — Always On must be enabled on the `barretapi` Web App, or App Service unloads the process when idle and the tick loop stops. Include this as a callout, not a footnote.
3. **Enabling it** — `JobScheduler:Enabled` defaults to `false`, including locally, so a `dotnet run` on a dev machine never posts to real accounts. Management endpoints work when it is disabled; only the tick loop is off.
4. **Endpoint reference** — a section per endpoint from Tasks 13–15 in the style the README already uses for social-post endpoints: a details table (auth, content type), a request-field table, and an example request and response.
5. **Job types and their arguments** — a table with one row per `JobType` and a JSON example for each, copied from the Arguments records in Tasks 8, 10, and 11:
   - `process-scheduled-posts` — `{"maxCount":100}`
   - `rss-promotion` — `{"feedUrl":"...","header":"...","recentDaysWindow":7}`
   - `rss-random` — `{"feedUrl":"...","platforms":["bluesky"],"excludeTags":["draft"],"maxAgeDays":90,"header":"ICYMI"}`
   - `tip-of-day` — `{"category":"dotnet","platforms":["bluesky"],"leader":"Tip:"}` (category required)
   - `nasa-apod` — `{"platforms":["bluesky","mastodon"]}`
   - `satellite` — `{"layer":"...","title":"...","description":"...","platforms":[...],"imageWidth":1200,"imageHeight":900}`
   - `purge-job-runs` — no arguments; seeded automatically at 03:00 UTC daily
6. **Cron and time zones** — 5- and 6-field expressions are both accepted; the sixth field is seconds. Schedules resolve in the job's `timeZoneId` (IANA or Windows id), so a job fires exactly once across both DST transitions. Note that `timeZoneId` defaults to `UTC`, which is what most Power Automate flows will *not* have been using.
7. **Behaviour worth knowing** — missed occurrences run late exactly once rather than replaying every missed slot; enabling a paused job or editing its cron recomputes the next run from now; a manual run never shifts the schedule; retries share one run-history row; only a fully failed run sends email.
8. **Configuration table** — every setting from the spec with its config key, Aspire parameter, and default.
9. **Migrating from Power Automate** — the four-step cutover from the spec, ending with the note that a job can be paused and its flow re-enabled with no code change.
10. **Troubleshooting** — job never fires (scheduler disabled, or Always On off, or job disabled); job disabled itself (unregistered job type or unparseable cron — check `lastRunError`); job stuck showing `isRunning` (a stale claim clears after `ClaimTimeoutMinutes`); no failure emails (email options unconfigured, or the rate limiter suppressed it).

- [ ] **Step 4: Update the README**

In `README.md`:

- Add the new endpoints to the Table of Contents endpoint list, matching the existing anchor style.
- Add a **Job Scheduler** section after the scheduled-posts material that summarises the feature in a short paragraph and links to `docs/JOB_SCHEDULER.md` for the full reference. Do not duplicate the endpoint tables in both files.
- In **Production Notes**, add a "Job Scheduler Configuration" subsection in the same shape as the existing "Scheduled Posts Configuration": the storage requirement (`JobScheduler__TableStorage__ConnectionString` or `__AccountEndpoint`), the two table names, the note that the unified storage pattern applies here too, and the **Always On** requirement.
- Note that the Power Automate flows are being retired in favour of this scheduler, and that both triggers hit the same services during the migration.

- [ ] **Step 5: Final format, verify, and commit**

```bash
dotnet format
dotnet build
dotnet test
```

Confirm the build is warning-free and no new test failures appeared before committing.

```bash
git add docs/JOB_SCHEDULER.md README.md
git commit -m "docs: document the job scheduler"
```

- [ ] **Step 6: Open the pull request**

```bash
git push -u origin 011-job-scheduler
gh pr create --title "Job scheduler" --body "$(cat <<'PRBODY'
Adds an in-process job scheduler so recurring work runs from BarretApi instead of Power Automate.

Job definitions live in Azure Table Storage and are managed over `/api/jobs`; a background tick loop claims due jobs, dispatches them to named handlers that wrap the existing services, retries with backoff, records run history, and reschedules. Missed occurrences run late exactly once.

Spec: `specs/011-job-scheduler/spec.md`
Plan: `specs/011-job-scheduler/plan.md`

**Deploy note:** `JobScheduler:Enabled` defaults to `false`. Production also needs **Always On** enabled on the `barretapi` Web App before any job is turned on — without it App Service unloads the process when idle and the scheduler stops.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
PRBODY
)"
```

---

## Verification Summary

When every task is complete, these must all hold:

- `dotnet build` succeeds with zero warnings.
- `dotnet test` shows no new failures beyond the pre-existing Nasa ones.
- `GET /api/jobs/types` returns all seven job types.
- Creating a job returns a `nextRunUtc` that matches the intended local time in the job's time zone.
- An invalid cron expression, unknown time zone, unregistered job type, or malformed arguments JSON is rejected with `400` and nothing is written.
- A manual run appears in the run history tagged `Manual` and leaves `nextRunUtc` untouched.
- With the scheduler disabled, no jobs execute but every management endpoint still responds.
- `docs/JOB_SCHEDULER.md` and the README describe the endpoints, job-type arguments, configuration, and the Always On prerequisite.

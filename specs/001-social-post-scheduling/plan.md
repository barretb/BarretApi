# Implementation Plan: Scheduled Social Post Publishing

**Branch**: `001-social-post-scheduling` | **Date**: 2026-03-22 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/001-social-post-scheduling/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

Add optional scheduling support to social post APIs via a `scheduledFor` date/time field and introduce a dedicated processing endpoint that publishes due scheduled posts. The design builds on existing social-post architecture by introducing durable scheduled-post persistence, a due-post orchestrator service, and run-summary responses for operational visibility, while preserving existing immediate-post behavior for requests without scheduling.

## Technical Context

**Language/Version**: C# / .NET 10.0 (`net10.0`)  
**Primary Dependencies**: FastEndpoints, FluentValidation, Microsoft.Extensions.Logging, existing social platform clients (Bluesky, Mastodon, LinkedIn), Azure.Data.Tables (existing persistence pattern)  
**Storage**: Azure Table Storage for scheduled-post durable state (new table/entity set), using existing infrastructure persistence approach  
**Testing**: xUnit, NSubstitute, Shouldly  
**Target Platform**: Linux-hosted ASP.NET API via Aspire AppHost
**Project Type**: Web service feature enhancement (existing API)  
**Performance Goals**: Due-post processing endpoint completes typical runs (<100 due posts) within 10 seconds excluding third-party latency; per-post create request remains in current envelope for immediate posts  
**Constraints**: Must avoid duplicate publishes; must preserve existing non-scheduled behavior; must use UTC-based comparisons for due checks; must return clear per-run summary  
**Scale/Scope**: Single-user automation API; expected scheduled backlog in low hundreds; no recurring schedule support in this increment

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| # | Principle | Status | Notes |
|---|-----------|--------|-------|
| I | Code Quality & Consistency | PASS | New files will follow file-scoped namespaces, naming rules, and analyzer requirements; warnings remain errors. |
| II | Clean Architecture & REPR Design | PASS | API remains REPR-oriented with `*Request`/`*Response`; orchestration logic in Core; persistence abstraction in Core interface, implementation in Infrastructure. |
| III | Test-Driven Quality Assurance | PASS | Unit tests planned for validators, scheduling service logic, repository mapping, and endpoint result/status behaviors. |
| IV | Centralized Configuration via Aspire | PASS | Any new storage/table options flow through AppHost-managed configuration and strongly-typed options. |
| V | Secure by Design | PASS | Existing API key auth remains; schedule inputs validated; failure responses continue with appropriate HTTP statuses. |
| VI | Observability & Structured Logging | PASS | Processing run IDs, due counts, per-post failures, and correlation IDs logged using structured logging. |
| VII | Simplicity & Maintainability | PASS | One-time scheduling only; no background scheduler introduced; trigger remains explicit endpoint call. |

## Project Structure

### Documentation (this feature)

```text
specs/001-social-post-scheduling/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
├── BarretApi.Api/
│   └── Features/
│       └── SocialPost/
│           ├── CreateSocialPostRequest.cs                # Add optional scheduledFor
│           ├── CreateSocialPostUploadEndpoint.cs         # Align upload flow with scheduling field if applicable
│           ├── CreateSocialPostValidator.cs              # Validate future schedule datetime
│           ├── CreateSocialPostEndpoint.cs               # Immediate vs scheduled branch behavior
│           ├── CreateSocialPostResponse.cs               # Include scheduling metadata in response
│           ├── ProcessScheduledPostsEndpoint.cs          # New due-processing endpoint
│           ├── ProcessScheduledPostsRequest.cs           # Optional batch/run controls (if needed)
│           ├── ProcessScheduledPostsResponse.cs          # Run summary counts + failures
│           └── ProcessScheduledPostsValidator.cs         # Validate processing request inputs
├── BarretApi.Core/
│   ├── Interfaces/
│   │   ├── IScheduledSocialPostRepository.cs             # New persistence abstraction
│   │   └── IScheduledSocialPostProcessor.cs              # New due-processing orchestrator abstraction
│   ├── Models/
│   │   ├── ScheduledSocialPostRecord.cs                  # Scheduled post durable state
│   │   ├── ScheduledPostStatus.cs                        # Pending/Processing/Succeeded/Failed (or equivalent)
│   │   ├── ScheduledPostProcessingSummary.cs             # Aggregate run summary
│   │   └── ScheduledPostFailureDetails.cs                # Per-post failure output model
│   └── Services/
│       └── ScheduledSocialPostProcessor.cs               # Core due-post selection + publish orchestration
├── BarretApi.Infrastructure/
│   └── Services/
│       └── AzureTableScheduledSocialPostRepository.cs    # Azure Table implementation for schedule records
└── BarretApi.AppHost/
  └── Program.cs                                        # Optional new table parameter wiring, if needed

tests/
├── BarretApi.Api.UnitTests/
│   └── Features/
│       └── SocialPost/
│           ├── CreateSocialPostValidator_Tests.cs
│           ├── CreateSocialPostEndpoint_Scheduling_Tests.cs
│           └── ProcessScheduledPostsEndpoint_HandleAsync_Tests.cs
├── BarretApi.Core.UnitTests/
│   └── Services/
│       └── ScheduledSocialPostProcessor_ProcessDueAsync_Tests.cs
└── BarretApi.Infrastructure.UnitTests/
  └── Services/
    └── AzureTableScheduledSocialPostRepository_Tests.cs
```

**Structure Decision**: Extend existing API/Core/Infrastructure projects only; no new projects. This keeps the architecture consistent with current social-post and RSS promotion patterns, reuses Azure Table persistence conventions, and isolates scheduling concerns to dedicated request/response, processor, and repository types.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| None | N/A | N/A |

## Constitution Re-Check (Post Phase 1 Design)

*Re-evaluated after completing research, data model, contracts, and quickstart.*

| # | Principle | Status | Post-Design Notes |
|---|-----------|--------|-------------------|
| I | Code Quality & Consistency | PASS | Planned files and symbols conform to current naming/file-scoped conventions; no deviations required. |
| II | Clean Architecture & REPR Design | PASS | Contracts and models preserve API/Core/Infrastructure separation and REPR naming. |
| III | Test-Driven Quality Assurance | PASS | Test strategy documented across API, Core, and Infrastructure units with mandated frameworks. |
| IV | Centralized Configuration via Aspire | PASS | Storage/config assumptions align with AppHost-only configuration model. |
| V | Secure by Design | PASS | No authentication changes; validation and status-code behavior explicitly maintained. |
| VI | Observability & Structured Logging | PASS | Processing run summary and failure details are first-class outputs and logging targets. |
| VII | Simplicity & Maintainability | PASS | Design intentionally excludes recurring scheduling and internal background workers (YAGNI). |

**Conclusion**: All constitutional gates pass before and after design. No waivers required.

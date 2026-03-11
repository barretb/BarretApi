# Implementation Plan: RSS Reminder Post Header Update

**Branch**: `005-rss-reminder-header` | **Date**: 2026-03-11 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/005-rss-reminder-header/spec.md`

## Summary

Change the reminder post text prefix in `BlogPromotionOrchestrator` from the current inline `"Did you miss it earlier? {Title}\n{URL}"` to a header-on-own-line format: `"In case you missed it earlier...\n\n{Title}\n{URL}"`. Initial posts remain unchanged. The change is a single-line edit in the `BuildReminderPostText` static method, plus unit tests.

## Technical Context

**Language/Version**: C# (latest) on .NET 10.0, Aspire 13
**Primary Dependencies**: FastEndpoints 7.x, Microsoft.Extensions.Options
**Storage**: N/A (no storage changes)
**Testing**: xUnit, NSubstitute, Shouldly
**Target Platform**: Linux/Windows server (ASP.NET Core)
**Project Type**: Web service (API)
**Performance Goals**: N/A (string formatting change only)
**Constraints**: N/A
**Scale/Scope**: Single method change + unit tests

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| # | Principle | Status | Evidence |
|---|-----------|--------|----------|
| I | Code Quality & Consistency | ✅ PASS | Change is in a single static method; follows existing code style |
| II | Clean Architecture & REPR | ✅ PASS | No architectural changes; service layer method only |
| III | Test-Driven Quality Assurance | ✅ PASS | Unit tests will be added for `BuildReminderPostText` and `BuildInitialPostText` using xUnit + Shouldly |
| IV | Centralized Config via Aspire | ✅ PASS | No new configuration; header is a fixed string per spec |
| V | Secure by Design | ✅ PASS | No user input; no new endpoints; no security surface changes |
| VI | Observability & Structured Logging | ✅ PASS | No logging changes needed |
| VII | Simplicity & Maintainability | ✅ PASS | Single-line string format change; YAGNI-compliant |

**Gate result**: ALL PASS. No violations.

## Project Structure

### Documentation (this feature)

```text
specs/005-rss-reminder-header/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output (empty — no external interface changes)
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (affected files)

```text
src/
└── BarretApi.Core/
    └── Services/
        └── BlogPromotionOrchestrator.cs   # BuildReminderPostText method (line ~217)

tests/
└── BarretApi.Core.UnitTests/
    └── Services/
        └── BlogPromotionOrchestrator_BuildReminderPostText_Tests.cs  # New test file
```

**Structure Decision**: No new projects or directories. The change targets a single existing method in `BarretApi.Core`. A new test file is added to the existing `BarretApi.Core.UnitTests` project following naming convention `ClassName_MethodName_Tests.cs`.

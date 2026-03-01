# Implementation Plan: RSS Blog Post Promotion

**Branch**: `001-rss-blog-posting` | **Date**: 2026-03-01 | **Spec**: `specs/001-rss-blog-posting/spec.md`
**Input**: Feature specification from `/specs/001-rss-blog-posting/spec.md`

## Summary

Add a secured API endpoint that processes RSS blog entries in two ordered passes: (1) publish new entries within a configurable day window that have not been previously posted, then (2) publish optional delayed reminder posts for previously posted entries that have reached a configurable hour threshold. Persist idempotent per-entry state in Azure Table Storage to prevent duplicate initial or reminder posts across runs and return a run summary with counts and failures.

## Technical Context

**Language/Version**: C# (latest) on .NET `net10.0`  
**Primary Dependencies**: FastEndpoints 8, FastEndpoints.Swagger, Microsoft.Extensions.* logging/options, Azure.Data.Tables (planned), Azure.Identity (planned)  
**Storage**: Azure Table Storage for blog-post promotion tracking records  
**Testing**: xUnit 2.9, NSubstitute 5, Shouldly 4, integration tests in existing `tests/BarretApi.Integration.Tests`  
**Target Platform**: ASP.NET Core API hosted on Azure App Service (Linux)  
**Project Type**: Multi-project backend web service (Aspire-oriented)  
**Performance Goals**: Complete one promotion run for up to 100 RSS entries within 60 seconds under nominal platform/API latency  
**Constraints**: HTTPS-only; API-key protected endpoint; no duplicate initial/reminder posts; preserve existing social posting service behavior; continue-on-error semantics  
**Scale/Scope**: Single-tenant feed polling endpoint, expected invocation frequency 1-24 times/day, tracking thousands of entries over time

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Pre-Phase 0 Gate Review

- **I. Code Quality & Consistency**: PASS — planned changes stay within existing solution style/analyzers.
- **II. Clean Architecture & REPR Design**: PASS — new capability will be expressed as FastEndpoints REPR endpoint + core/infrastructure services.
- **III. Test-Driven Quality Assurance**: PASS — unit/integration test additions planned using xUnit/NSubstitute/Shouldly.
- **IV. Centralized Configuration via Aspire**: PASS — new settings planned via strongly typed options and AppHost-managed configuration.
- **V. Secure by Design**: PASS — endpoint remains authenticated via existing API key scheme; input/config validation required.
- **VI. Observability & Structured Logging**: PASS — run-level and per-entry structured logs with correlation context planned.
- **VII. Simplicity & Maintainability**: PASS — workflow split into focused services and deterministic two-pass orchestration.

### Post-Phase 1 Design Re-check

- PASS — data model, API contract, and quickstart keep clean architecture boundaries (`Api` endpoint orchestration, `Core` business rules, `Infrastructure` RSS + table persistence adapters).
- PASS — no constitution violations introduced; no complexity exceptions required.

## Project Structure

### Documentation (this feature)

```text
specs/001-rss-blog-posting/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── rss-promotion-endpoint.openapi.yaml
└── tasks.md
```

### Source Code (repository root)

```text
src/
├── BarretApi.Api/
│   ├── Features/
│   │   └── SocialPost/
│   └── Program.cs
├── BarretApi.Core/
│   ├── Configuration/
│   ├── Interfaces/
│   ├── Models/
│   └── Services/
├── BarretApi.Infrastructure/
│   ├── Services/
│   └── (new RSS + table persistence adapters)
└── BarretApi.AppHost/

tests/
├── BarretApi.Api.UnitTests/
├── BarretApi.Core.UnitTests/
└── BarretApi.Integration.Tests/
```

**Structure Decision**: Reuse existing layered solution structure (`Api`/`Core`/`Infrastructure`) and add feature-specific endpoint orchestration in `BarretApi.Api`, business rules/models in `BarretApi.Core`, and external integrations (RSS parser + Azure Table tracking repository) in `BarretApi.Infrastructure`.

## Complexity Tracking

No constitution violations or justified exceptions are required at planning time.

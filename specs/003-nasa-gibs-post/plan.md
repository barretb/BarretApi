# Implementation Plan: NASA GIBS Ohio Satellite Image Social Posting

**Branch**: `003-nasa-gibs-post` | **Date**: 2026-03-09 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/003-nasa-gibs-post/spec.md`

## Summary

Add an API endpoint that fetches satellite imagery of Ohio from NASA's GIBS Worldview Snapshot API and posts it to configured social media platforms (Bluesky, Mastodon, LinkedIn) with a descriptive caption, NASA acknowledgement, and a Worldview interactive link. The endpoint supports optional date selection, configurable imagery layers (5 true-color CorrectedReflectance layers), and reuses the existing social posting infrastructure. No API key is required for GIBS. The Snapshot API returns raw image bytes via `GetSnapshot` requests at `https://wvs.earthdata.nasa.gov/api/v1/snapshot`.

## Technical Context

**Language/Version**: C# latest / .NET 10.0 (`net10.0`)
**Primary Dependencies**: FastEndpoints 8.x (REPR), SkiaSharp 3.119.2 (image resizing), Aspire 13 (orchestration)
**Storage**: N/A (no persistence; stateless image fetch + social post)
**Testing**: xUnit + NSubstitute 5.3.0 + Shouldly 4.3.0
**Target Platform**: Linux/Windows server (.NET Aspire AppHost)
**Project Type**: Web service (REST API endpoint)
**Performance Goals**: Single request completes within 30 seconds (SC-001); validation within 1 second (SC-006)
**Constraints**: GIBS Snapshot API has no documented rate limit but is intended for reasonable use (not bulk download); images typically 80–400 KB JPEG at 1024×768
**Scale/Scope**: Single new endpoint added to existing API; follows established APOD feature pattern

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| # | Principle | Status | Notes |
|---|-----------|--------|-------|
| I | Code Quality & Consistency | **PASS** | TreatWarningsAsErrors enabled; file-scoped namespaces; Allman bracing; primary constructors with readonly fields; `dotnet format` enforced. All new code follows existing patterns. |
| II | Clean Architecture & REPR Design | **PASS** | New endpoint follows REPR via FastEndpoints (`OhioSatellitePostEndpoint` + `OhioSatellitePostRequest` / `OhioSatellitePostResponse`). Core orchestrator (`NasaGibsPostService`) separated from Infrastructure client (`INasaGibsClient` interface in Core, implementation in Infrastructure). DTOs use `*Request`/`*Response` suffixes. |
| III | Test-Driven Quality Assurance | **PASS** | xUnit + NSubstitute + Shouldly. Test naming: `ClassName_MethodName_Tests.cs` / `DoesSomething_GivenSomeCondition`. Unit tests for Core service + Infrastructure client + Api endpoint/validator. No commercial libraries. |
| IV | Centralized Configuration via Aspire | **PASS** | GIBS config (base URL, default layer, supported layers, Ohio bounding box, image dimensions) managed via Aspire AppHost `WithEnvironment()`. Strongly-typed `NasaGibsOptions` class in Core. No `appsettings.json` outside AppHost. |
| V | Secure by Design | **PASS** | Input validation via FastEndpoints `Validator<T>` (date range, layer allowlist, platform names). HTTPS only for GIBS API. API key auth on endpoint (existing mechanism). Proper HTTP status codes (400, 502, 207). No user-supplied data in raw queries. |
| VI | Observability & Structured Logging | **PASS** | `ILogger<T>` with structured message templates for GIBS requests, image retrieval, post results, and errors. Correlation via existing middleware. No sensitive data logged. |
| VII | Simplicity & Maintainability | **PASS** | Methods ≤20 lines. Reuses existing `SocialPostService`, `IImageResizer`, platform clients. No speculative features beyond spec requirements. Follows established APOD orchestrator pattern. |

**Gate Result**: ALL PASS — no violations to justify.

### Post-Design Re-Check

| # | Principle | Status | Notes |
|---|-----------|--------|-------|
| I | Code Quality & Consistency | **PASS** | No changes from pre-research check. |
| II | Clean Architecture & REPR Design | **PASS** | `INasaGibsClient` in Core, `NasaGibsClient` in Infrastructure — interfaces and implementations in separate projects. |
| III | Test-Driven Quality Assurance | **PASS** | Test plan covers Core service, Infrastructure client, Api endpoint, and validator. |
| IV | Centralized Configuration via Aspire | **PASS** | `NasaGibsOptions` populated from AppHost environment variables. No GIBS API key needed (public API). |
| V | Secure by Design | **PASS** | Date validation prevents out-of-range requests. Layer allowlist prevents arbitrary layer injection. Content-Type checking on GIBS responses detects XML error responses. |
| VI | Observability & Structured Logging | **PASS** | Logging plan includes GIBS request URL, response content-type, image byte size, post results per platform. |
| VII | Simplicity & Maintainability | **PASS** | Design closely mirrors proven APOD pattern. |

## Project Structure

### Documentation (this feature)

```text
specs/003-nasa-gibs-post/
├── plan.md              # This file
├── research.md          # Phase 0 output (complete)
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   └── nasa-gibs-post-endpoint.openapi.yaml
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
src/
├── BarretApi.Api/
│   └── Features/
│       └── Nasa/
│           ├── OhioSatellitePostEndpoint.cs      # NEW - FastEndpoints REPR endpoint
│           ├── OhioSatellitePostRequest.cs        # NEW - Request DTO
│           ├── OhioSatellitePostResponse.cs       # NEW - Response DTO
│           └── OhioSatellitePostValidator.cs      # NEW - FluentValidation rules
├── BarretApi.AppHost/
│   └── Program.cs                                 # MODIFIED - add GIBS config parameters
├── BarretApi.Core/
│   ├── Configuration/
│   │   └── NasaGibsOptions.cs                     # NEW - strongly-typed GIBS config
│   ├── Interfaces/
│   │   └── INasaGibsClient.cs                     # NEW - GIBS client abstraction
│   ├── Models/
│   │   ├── GibsSnapshotEntry.cs                   # NEW - snapshot metadata domain model
│   │   └── OhioSatellitePostResult.cs             # NEW - post result domain model
│   └── Services/
│       └── NasaGibsPostService.cs                 # NEW - orchestrator service
├── BarretApi.Infrastructure/
│   └── Nasa/
│       └── NasaGibsClient.cs                      # NEW - HTTP client for GIBS Snapshot API
└── BarretApi.ServiceDefaults/                     # NO CHANGES

tests/
├── BarretApi.Api.UnitTests/
│   └── Features/
│       └── Nasa/
│           ├── OhioSatellitePostEndpoint_HandleAsync_Tests.cs  # NEW
│           └── OhioSatellitePostValidator_Tests.cs             # NEW
├── BarretApi.Core.UnitTests/
│   └── Services/
│       └── NasaGibsPostService_PostAsync_Tests.cs              # NEW
└── BarretApi.Infrastructure.UnitTests/
    └── Nasa/
        └── NasaGibsClient_GetSnapshotAsync_Tests.cs            # NEW
```

**Structure Decision**: Follows existing Clean Architecture layout — new files slot into the established `Features/Nasa/`, `Core/`, `Infrastructure/Nasa/` directories. No new projects required. The GIBS feature parallels the APOD feature within the same project structure, sharing the `Nasa/` feature folder and reusing `SocialPostService`, `IImageResizer`, and platform clients.

## Complexity Tracking

> No constitution violations — no justifications needed.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| *(none)* | — | — |

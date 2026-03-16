# Implementation Plan: DiceBear Random Avatar

**Branch**: `007-dicebear-avatar` | **Date**: 2026-03-15 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/007-dicebear-avatar/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

Add an API endpoint that generates random avatar images by proxying requests to the DiceBear HTTP API (v9.x). The endpoint accepts optional parameters for avatar style, output format, and seed value, and returns the generated avatar image with the correct content type. When no parameters are provided, a random style and seed are selected automatically. The implementation follows existing patterns: a FastEndpoints REPR endpoint in the API layer, an interface in Core, and a typed HttpClient integration in Infrastructure.

## Technical Context

**Language/Version**: C# / .NET 10.0 (`net10.0`)
**Primary Dependencies**: FastEndpoints 8.x, Aspire 13, Microsoft.Extensions.Http.Resilience
**Storage**: N/A — no persistence required; avatars are generated on-demand from upstream API
**Testing**: xUnit, NSubstitute, Shouldly
**Target Platform**: Linux server (container) via Aspire
**Project Type**: Web service (additional endpoint on existing API)
**Performance Goals**: < 3 seconds end-to-end response time under normal conditions
**Constraints**: DiceBear upstream rate limits (50 req/s SVG, 10 req/s raster); raster formats max 256×256px; no authentication required for upstream API
**Scale/Scope**: Single endpoint addition to existing API; no new projects needed

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| # | Principle | Status | Notes |
|---|-----------|--------|-------|
| I | Code Quality & Consistency | PASS | Will follow all naming conventions, file-scoped namespaces, Allman braces, tab indentation. `dotnet format` will be run before merge. |
| II | Clean Architecture & REPR Design | PASS | FastEndpoints REPR pattern with `*Request`/`*Response` suffixes. Interface in Core, implementation in Infrastructure. No domain entities exposed directly. |
| III | Test-Driven Quality Assurance | PASS | xUnit tests with NSubstitute and Shouldly. Test classes: `DiceBearAvatarClient_GetAvatarAsync_Tests`, `GenerateAvatarEndpoint_HandleAsync_Tests`, etc. |
| IV | Centralized Configuration via Aspire | PASS | No configuration needed — DiceBear API requires no API key and has a fixed public base URL. If base URL override is desired, it can be added via Aspire parameter. |
| V | Secure by Design | PASS | Input validation via FluentValidation (style name allowlist, format enum, seed length limit). Proper HTTP status codes (400, 502). No sensitive data. |
| VI | Observability & Structured Logging | PASS | Structured logging for upstream requests and errors. Request correlation via Aspire defaults. |
| VII | Simplicity & Maintainability | PASS | Single endpoint, single infrastructure client, no new projects. Methods < 20 lines. Async throughout. |

## Project Structure

### Documentation (this feature)

```text
specs/007-dicebear-avatar/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output (API contract)
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
src/
├── BarretApi.Api/
│   └── Features/
│       └── Avatar/
│           ├── GenerateAvatarEndpoint.cs      # FastEndpoints REPR endpoint
│           ├── GenerateAvatarRequest.cs        # Request DTO with optional style, format, seed
│           ├── GenerateAvatarResponse.cs       # Response DTO (not used directly — returns image bytes)
│           └── GenerateAvatarValidator.cs      # FluentValidation: style allowlist, format enum, seed length
├── BarretApi.Core/
│   ├── Interfaces/
│   │   └── IDiceBearAvatarClient.cs           # Interface for DiceBear API integration
│   └── Models/
│       ├── AvatarResult.cs                    # Domain model: image bytes + content type + metadata
│       ├── AvatarStyle.cs                     # Static class with allowed style constants
│       └── AvatarFormat.cs                    # Enum: Svg, Png, Jpg, WebP, Avif
├── BarretApi.Infrastructure/
│   └── DiceBear/
│       └── DiceBearAvatarClient.cs            # Typed HttpClient calling DiceBear HTTP API
└── BarretApi.AppHost/
    └── Program.cs                             # (minor) Add DiceBear base URL parameter if needed

tests/
├── BarretApi.Core.UnitTests/
│   └── Models/
│       └── AvatarStyle_Tests.cs               # Tests for style validation/constants
├── BarretApi.Infrastructure.UnitTests/
│   └── DiceBear/
│       └── DiceBearAvatarClient_GetAvatarAsync_Tests.cs  # Tests with mocked HttpClient
└── BarretApi.Api.UnitTests/
    └── Features/
        └── Avatar/
            ├── GenerateAvatarEndpoint_HandleAsync_Tests.cs  # Endpoint logic tests
            └── GenerateAvatarValidator_Tests.cs              # Validation rule tests
```

**Structure Decision**: This feature adds files to the existing project structure with no new projects. The Avatar feature gets its own folder under `Features/` (API layer), a new `DiceBear/` folder under Infrastructure (following the pattern of `Nasa/`, `Bluesky/`, etc.), and model/interface additions in Core. This matches the established architecture perfectly.

## Complexity Tracking

No constitution violations. The feature is a straightforward single-endpoint addition following all established patterns. No additional complexity justification needed.

## Constitution Re-Check (Post Phase 1 Design)

*Re-evaluated after completing data model, contracts, and quickstart.*

| # | Principle | Status | Post-Design Notes |
|---|-----------|--------|-------------------|
| I | Code Quality & Consistency | PASS | All new files follow naming conventions. `AvatarStyle` uses PascalCase constants, `_httpClient` underscore prefix for fields, `GetAvatarAsync` async suffix. |
| II | Clean Architecture & REPR Design | PASS | REPR pattern confirmed: `GenerateAvatarEndpoint` → `GenerateAvatarRequest` / image bytes response. Interface `IDiceBearAvatarClient` in Core, implementation `DiceBearAvatarClient` in Infrastructure. No domain entities exposed. |
| III | Test-Driven Quality Assurance | PASS | Test classes defined: `DiceBearAvatarClient_GetAvatarAsync_Tests`, `GenerateAvatarEndpoint_HandleAsync_Tests`, `GenerateAvatarValidator_Tests`. xUnit + NSubstitute + Shouldly. |
| IV | Centralized Configuration via Aspire | PASS | No configuration needed (public API, no secrets). If base URL is later made configurable, it will go through Aspire AppHost parameter. |
| V | Secure by Design | PASS | Inputs validated via FluentValidation (style allowlist, format allowlist, seed length). HTTP 400 for validation, 502 for upstream errors. API key auth inherited from existing scheme. |
| VI | Observability & Structured Logging | PASS | Structured logging for upstream requests (`"Fetching avatar from {Url}"`) and errors. No sensitive data logged. |
| VII | Simplicity & Maintainability | PASS | No new projects, no new packages, no persistence. Single endpoint with < 20-line methods. Async throughout. YAGNI respected (no caching, no self-hosted option, no advanced DiceBear options). |

**Conclusion**: All 7 gates pass. No violations to justify. Design is ready for task generation.

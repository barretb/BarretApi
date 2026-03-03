# Implementation Plan: LinkedIn Posting Support

**Branch**: `001-linkedin-posting` | **Date**: 2026-03-01 | **Spec**: `specs/001-linkedin-posting/spec.md`
**Input**: Feature specification from `/specs/001-linkedin-posting/spec.md`

## Summary

Extend the existing social post workflow to support LinkedIn as an additional target platform while preserving current endpoint contracts and multi-platform partial-success semantics. Implement LinkedIn via a new `ISocialPlatformClient` adapter, wire environment-based configuration in AppHost/API options, update platform validation to accept `linkedin`, and return LinkedIn results using the existing per-platform response structure.

## Technical Context

**Language/Version**: C# (latest) on .NET `net10.0`  
**Primary Dependencies**: FastEndpoints 8, FluentValidation 11, Microsoft.Extensions.* logging/options, existing HttpClient-based platform adapters  
**Storage**: N/A for LinkedIn posting itself (no new persistent store); existing API behavior remains stateless per request for direct post endpoint  
**Testing**: xUnit 2.9, NSubstitute 5, Shouldly 4 in existing unit/integration test projects  
**Target Platform**: ASP.NET Core API hosted on Azure App Service (Linux) and local Aspire-driven development environment
**Project Type**: Multi-project backend web service (`Api`/`Core`/`Infrastructure` + Aspire AppHost)  
**Performance Goals**: Maintain existing endpoint behavior with added LinkedIn target and keep per-request completion within current operational expectations for multi-platform posting  
**Constraints**: HTTPS-only; API-key protected endpoint; no credential leakage in logs/responses; preserve existing status code semantics (200/207/502) for aggregate platform outcomes  
**Scale/Scope**: Single-tenant API usage; expected request payloads with 1-3 target platforms and up to 4 images

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Pre-Phase 0 Gate Review

- **I. Code Quality & Consistency**: PASS — planned changes fit current analyzers/style and keep conventions unchanged.
- **II. Clean Architecture & REPR Design**: PASS — LinkedIn integration is an infrastructure adapter behind existing core interface and REPR endpoint.
- **III. Test-Driven Quality Assurance**: PASS — tests planned in existing xUnit projects with NSubstitute/Shouldly.
- **IV. Centralized Configuration via Aspire**: PASS — LinkedIn settings remain AppHost-managed and bound via strongly typed options.
- **V. Secure by Design**: PASS — endpoint auth unchanged; explicit handling for auth/config errors and no secret exposure required.
- **VI. Observability & Structured Logging**: PASS — platform-level structured logs/correlation remain applicable to LinkedIn path.
- **VII. Simplicity & Maintainability**: PASS — single new platform client and minimal validation/DI updates, no speculative features.

### Post-Phase 1 Design Re-check

- PASS — data model and contract reuse existing endpoint/response semantics with one additional platform value.
- PASS — no constitution violations introduced; no complexity exception required.

## Project Structure

### Documentation (this feature)

```text
specs/001-linkedin-posting/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── social-post-linkedin.openapi.yaml
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
│   ├── Bluesky/
│   ├── Mastodon/
│   └── (new LinkedIn adapter)
└── BarretApi.AppHost/

tests/
├── BarretApi.Api.UnitTests/
├── BarretApi.Core.UnitTests/
└── BarretApi.Integration.Tests/
```

**Structure Decision**: Reuse the established layered architecture and add LinkedIn as another infrastructure platform client implementing `ISocialPlatformClient`, with minimal API/core changes for validation, DI registration, and configuration.

## Complexity Tracking

No constitution violations or justified exceptions are required at planning time.

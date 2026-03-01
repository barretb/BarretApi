# Implementation Plan: Social Media Post API

**Branch**: `001-social-post-api` | **Date**: 2026-02-28 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/001-social-post-api/spec.md`

## Summary

Build a fire-and-forget Web API that cross-posts text, images, and hashtags to Bluesky and Mastodon in a single request. The API automatically shortens text per platform character limits, enforces mandatory alt text on all images, builds platform-specific rich text metadata (Bluesky facets), supports both multipart upload and URL-referenced images, and retries transient failures with configurable exponential backoff. Authentication is via a pre-shared API key (`X-Api-Key` header). The API uses FastEndpoints REPR pattern on .NET 10 with Aspire 13 for configuration and service discovery.

## Technical Context

**Language/Version**: C# / .NET 10.0 (`net10.0`), latest language features, nullable reference types enabled  
**Primary Dependencies**: FastEndpoints 7.x, Aspire 13 (AppHost + ServiceDefaults), FluentValidation, Microsoft.Extensions.Http (for HttpClientFactory with Polly retry)  
**Storage**: N/A (fire-and-forget; no database)  
**Testing**: xUnit, NSubstitute, Shouldly  
**Target Platform**: Linux/Windows server (container-ready via Aspire)  
**Project Type**: Web API service  
**Performance Goals**: < 10 seconds end-to-end per post under normal network conditions (SC-001)  
**Constraints**: No local persistence; single-user; HTTPS only; all images require alt text  
**Scale/Scope**: Single user, 2 platform integrations (Bluesky, Mastodon), ~5 endpoints

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| # | Gate (from Constitution) | Status | Notes |
|---|--------------------------|--------|-------|
| I | Code Quality: TreatWarningsAsErrors, file-scoped namespaces, Allman braces, naming conventions, `dotnet format` | ✅ PASS | All projects will inherit from Directory.Build.props |
| II | REPR pattern via FastEndpoints 7.x; DDD where applicable; built-in DI only; `*Request`/`*Response` naming; interfaces ≠ implementations in same project | ✅ PASS | Single post endpoint follows REPR; platform clients behind interfaces in separate project |
| III | xUnit + NSubstitute + Shouldly; AAA pattern; test naming conventions; no commercially licensed test libs | ✅ PASS | Test projects in `tests/`; no FluentAssertions or Moq |
| IV | All config in Aspire AppHost; no appsettings in other projects; IOptions<T> for typed config | ✅ PASS | Platform credentials, API key, retry settings all in AppHost User Secrets / config |
| V | OWASP; input validation; HTTPS; API key auth; proper HTTP status codes | ✅ PASS | API key via X-Api-Key header; FluentValidation for request validation; 400/401/500 codes |
| VI | Microsoft.Extensions.Logging; structured logging; correlation IDs; no sensitive data in logs | ✅ PASS | Logging via DI; no credentials logged |
| VII | Methods < 20 lines; ≤ 4 params; async/await for I/O; YAGNI | ✅ PASS | Fire-and-forget keeps scope minimal; no speculative features |

**Result**: All 7 gates PASS. No violations to justify.

## Project Structure

### Documentation (this feature)

```text
specs/001-social-post-api/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
src/
├── BarretApi.AppHost/              # Aspire AppHost — all config, secrets, service discovery
├── BarretApi.ServiceDefaults/      # Shared service defaults (logging, health checks, telemetry)
├── BarretApi.Api/                  # Web API project (FastEndpoints, validation, API key auth)
│   ├── Features/
│   │   └── SocialPost/             # REPR endpoint(s) for social posting
│   │       ├── CreateSocialPostEndpoint.cs
│   │       ├── CreateSocialPostRequest.cs
│   │       ├── CreateSocialPostResponse.cs
│   │       └── CreateSocialPostValidator.cs
│   └── Auth/
│       └── ApiKeyAuthHandler.cs    # X-Api-Key authentication handler
├── BarretApi.Core/                 # Domain models, interfaces, DTOs, text shortening logic
│   ├── Models/
│   ├── Interfaces/
│   │   ├── ISocialPlatformClient.cs
│   │   └── ITextShorteningService.cs
│   ├── DTOs/
│   └── Services/
│       ├── TextShorteningService.cs
│       └── HashtagService.cs
└── BarretApi.Infrastructure/       # Platform client implementations (Bluesky, Mastodon)
    ├── Bluesky/
    │   ├── BlueskyClient.cs
    │   └── BlueskyFacetBuilder.cs
    └── Mastodon/
        └── MastodonClient.cs

tests/
├── BarretApi.Core.UnitTests/       # Unit tests for text shortening, hashtag logic, DTOs
├── BarretApi.Api.UnitTests/        # Unit tests for validators, endpoint logic
└── BarretApi.Integration.Tests/    # Integration tests against real/mock platform APIs
```

**Structure Decision**: Multi-project Clean Architecture with 4 production projects (AppHost, ServiceDefaults, Api, Core, Infrastructure) and 3 test projects. This aligns with Constitution Principle II (interfaces in Core, implementations in Infrastructure) and Principle IV (config only in AppHost). The 5-project production layout is the minimum needed to keep interfaces and implementations in separate projects per the constitution.

## Complexity Tracking

> No violations to justify — all constitution gates pass.

## Constitution Re-Check (Post Phase 1 Design)

*Re-evaluated after data-model.md, contracts/, and research.md are complete.*

| # | Gate | Status | Post-Design Notes |
|---|------|--------|-------------------|
| I | Code Quality | ✅ PASS | No design decisions conflict; all projects inherit Directory.Build.props |
| II | Clean Architecture & REPR | ✅ PASS | Two REPR endpoints (JSON + multipart); `ISocialPlatformClient` in Core, implementations in Infrastructure; `*Request`/`*Response` naming throughout |
| III | Test-Driven QA | ✅ PASS | Three test projects defined; NSubstitute for mocking platform clients; Shouldly for assertions |
| IV | Centralized Config via Aspire | ✅ PASS | `BlueskyOptions`, `MastodonOptions`, `ApiKeyOptions`, `RetryOptions` all via AppHost User Secrets + `IOptions<T>` |
| V | Secure by Design | ✅ PASS | API key auth; FluentValidation; HTTPS; proper status codes (400/401/207/502) |
| VI | Observability | ✅ PASS | Microsoft.Extensions.Logging; structured logging; no credentials in logs |
| VII | Simplicity | ✅ PASS | Fire-and-forget; no speculative features; async/await for all I/O |

**Result**: All 7 gates PASS. No violations introduced by Phase 1 design.

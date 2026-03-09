# Implementation Plan: NASA APOD Social Posting

**Branch**: `001-nasa-apod-post` | **Date**: 2026-03-08 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/001-nasa-apod-post/spec.md`

## Summary

Add a new API endpoint (`POST /api/social-posts/nasa-apod`) that fetches the NASA Astronomy Picture of the Day via the public API and posts it to selected social media platforms (Bluesky, Mastodon, LinkedIn). The image is attached using the standard-resolution URL, with the HD link included in the post text. Images exceeding platform size limits are resized to JPEG. The APOD explanation is used as image alt text. The feature follows the established REPR endpoint pattern and reuses the existing `SocialPostService` posting infrastructure.

## Technical Context

**Language/Version**: C# latest / .NET 10.0
**Primary Dependencies**: FastEndpoints 8.x, SkiaSharp (new — image resizing), Microsoft.Extensions.Logging, Microsoft.Extensions.Options
**Storage**: N/A (stateless — no persistence required for APOD posts)
**Testing**: xUnit + NSubstitute + Shouldly
**Target Platform**: Linux/Windows server (Aspire 13 AppHost)
**Project Type**: Web service (API endpoint addition)
**Performance Goals**: Response within 30 seconds (includes NASA API call + image download + platform fan-out)
**Constraints**: NASA API rate limit 1,000 req/hr (sufficient for manual trigger)
**Scale/Scope**: Single new endpoint, ~8 new files, ~4 modified files

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| # | Principle | Gate | Status |
|---|-----------|------|--------|
| I | Code Quality & Consistency | File-scoped namespaces, `_camelCase` fields, primary constructors with readonly fields, TreatWarningsAsErrors | **PASS** — all new code will follow existing patterns |
| II | Clean Architecture & REPR | REPR pattern via FastEndpoints, `*Request`/`*Response` suffixes, interfaces in Core / implementations in Infrastructure | **PASS** — endpoint follows `RssRandomPostEndpoint` pattern; new `INasaApodClient` in Core, implementation in Infrastructure |
| III | Test-Driven Quality Assurance | xUnit, NSubstitute, Shouldly; `ClassName_MethodName_Tests.cs` naming; no commercial libs | **PASS** — tests follow `RssRandomPostService_SelectAndPostAsync_Tests` pattern |
| IV | Centralized Configuration via Aspire | NASA API key in AppHost User Secrets, `IOptions<NasaApodOptions>` | **PASS** — config follows existing `BlueskyOptions` pattern |
| V | Secure by Design | Input validation via `Validator<T>`, API key auth, HTTPS only | **PASS** — reuses existing auth; date validation via FluentValidation |
| VI | Observability & Structured Logging | Structured logging with correlation IDs | **PASS** — follows existing `RssRandomPostService` logging pattern |
| VII | Simplicity & Maintainability | Methods <20 lines, ≤4 params, async/await for I/O | **PASS** — service orchestrates via composition |

**New dependency justification**: `SkiaSharp` is required for image resizing (FR-018/FR-020). No image processing library currently exists in the codebase. SkiaSharp is MIT-licensed, cross-platform, and the most widely used .NET image library for server-side work. It adds no architectural complexity — the resize logic will be encapsulated in a new `IImageResizer` interface/implementation pair.

## Project Structure

### Documentation (this feature)

```text
specs/001-nasa-apod-post/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   └── nasa-apod-post-endpoint.openapi.yaml
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
src/
├── BarretApi.Api/
│   └── Features/
│       └── NasaApod/                          # NEW folder per user request
│           ├── NasaApodPostEndpoint.cs         # REPR endpoint
│           ├── NasaApodPostRequest.cs          # Request model
│           ├── NasaApodPostResponse.cs         # Response model
│           └── NasaApodPostValidator.cs        # FluentValidation
├── BarretApi.Core/
│   ├── Configuration/
│   │   └── NasaApodOptions.cs                  # NEW options class
│   ├── Interfaces/
│   │   ├── INasaApodClient.cs                  # NEW interface
│   │   └── IImageResizer.cs                    # NEW interface
│   ├── Models/
│   │   ├── ApodEntry.cs                        # NEW model
│   │   └── ApodPostResult.cs                   # NEW result model
│   └── Services/
│       └── NasaApodPostService.cs              # NEW orchestrator service
├── BarretApi.Infrastructure/
│   ├── Nasa/                                   # NEW folder
│   │   └── NasaApodClient.cs                   # NASA API HTTP client
│   └── Services/
│       └── SkiaImageResizer.cs                 # NEW SkiaSharp resizer
├── BarretApi.AppHost/
│   └── Program.cs                              # MODIFIED — add NASA API key param
└── BarretApi.Api/
    └── Program.cs                              # MODIFIED — register new services

tests/
├── BarretApi.Core.UnitTests/
│   └── Services/
│       └── NasaApodPostService_PostAsync_Tests.cs   # NEW
├── BarretApi.Api.UnitTests/
│   └── Features/
│       └── NasaApod/
│           └── NasaApodPostEndpoint_HandleAsync_Tests.cs  # NEW
└── BarretApi.Infrastructure.UnitTests/
    ├── Nasa/
    │   └── NasaApodClient_GetApodAsync_Tests.cs     # NEW
    └── Services/
        └── SkiaImageResizer_ResizeAsync_Tests.cs    # NEW
```

**Structure Decision**: Follows the existing layered architecture (Api → Core → Infrastructure). New endpoint gets its own `Features/NasaApod/` folder as requested by the user, separate from the `SocialPost/` folder. The NASA client goes in `Infrastructure/Nasa/` mirroring the existing `Bluesky/`, `Mastodon/`, `LinkedIn/` folder pattern. Image resizer is a general-purpose service in `Infrastructure/Services/`.

## Complexity Tracking

> No constitution violations. All new code fits within existing architecture.
> SkiaSharp is the only new external dependency, justified by FR-018/FR-020 (image resizing requirement).

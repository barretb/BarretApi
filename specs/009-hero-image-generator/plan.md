# Implementation Plan: Hero Image Generator

**Branch**: `009-hero-image-generator` | **Date**: 2026-04-10 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/009-hero-image-generator/spec.md`

## Summary

Build a stateless API endpoint (`POST /api/hero-image`) that composites a branded hero image (1280×720 PNG) from user-provided title text, optional subtitle, and optional background image. Uses SkiaSharp (already in project) for image composition: faded background, face image (lower-right), logo (lower-left), and JetBrains Mono text rendered dynamically between the overlays. Follows existing REPR pattern with FastEndpoints, with the generator interface in Core and SkiaSharp implementation in Infrastructure.

## Technical Context

**Language/Version**: C# latest, .NET 10.0 (`net10.0`)
**Primary Dependencies**: SkiaSharp 3.119.2, FastEndpoints 8.1.0, FluentValidation (via FastEndpoints)
**Storage**: N/A (stateless — no data persistence)
**Testing**: xUnit, NSubstitute, Shouldly
**Target Platform**: Linux server (via Aspire AppHost)
**Project Type**: Web service (additional API endpoint in existing project)
**Performance Goals**: < 5 seconds per request (SC-001)
**Constraints**: Output ≥ 1280×720 pixels, uploaded background ≤ 10 MB, PNG output
**Scale/Scope**: Single endpoint addition to existing API; ~6 new files across 3 existing projects

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Code Quality & Consistency | ✅ PASS | File-scoped namespaces, Allman braces, tab indentation, `_camelCase` fields, primary constructors with readonly fields, `Async` suffix on async methods |
| II. Clean Architecture & REPR | ✅ PASS | REPR pattern via FastEndpoints; `IHeroImageGenerator` interface in Core, `SkiaHeroImageGenerator` implementation in Infrastructure; `*Request` suffix on API DTO; `*Command` suffix on use-case input; interface and implementation in separate projects |
| III. Test-Driven Quality | ✅ PASS | xUnit tests with NSubstitute/Shouldly; `ClassName_MethodName_Tests.cs` naming; Arrange-Act-Assert pattern; unit tests for generator and validator |
| IV. Centralized Config via Aspire | ✅ PASS | No appsettings.json needed — static asset paths resolved from content root in DI registration; no User Secrets required (no external service keys) |
| V. Secure by Design | ✅ PASS | FluentValidation for input validation; file size limit (10 MB); format validation (JPEG/PNG magic bytes); no SQL or external queries; appropriate HTTP status codes (400, 422, 500) |
| VI. Observability & Structured Logging | ✅ PASS | `ILogger<T>` with structured messages and correlation IDs; appropriate log levels |
| VII. Simplicity & Maintainability | ✅ PASS | Single endpoint; generator method under 20 lines (composition delegated to focused helper methods); no speculative features; async for I/O (file reads) |

**Post-Phase 1 re-check**: All gates still pass. No violations detected.

## Project Structure

### Documentation (this feature)

```text
specs/009-hero-image-generator/
├── plan.md              # This file
├── research.md          # Phase 0 output — technology decisions
├── data-model.md        # Phase 1 output — entity definitions
├── quickstart.md        # Phase 1 output — build and test guide
├── contracts/
│   └── hero-image-api.md   # Phase 1 output — endpoint contract
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
├── BarretApi.Api/
│   ├── Features/
│   │   └── HeroImage/
│   │       ├── GenerateHeroImageEndpoint.cs    # FastEndpoints REPR endpoint
│   │       ├── GenerateHeroImageRequest.cs     # Request DTO (title, subtitle, backgroundImage)
│   │       └── GenerateHeroImageValidator.cs   # FluentValidation rules
│   └── images/
│       ├── barretcircle2.png                   # (existing) Face overlay
│       ├── barret-blake-logo-1024.png          # (existing) Logo overlay
│       └── generic-background.jpg              # (existing) Default background
├── BarretApi.Core/
│   ├── Interfaces/
│   │   └── IHeroImageGenerator.cs              # Generator interface
│   └── Models/
│       ├── HeroImageGenerationCommand.cs       # Use-case input DTO
│       └── HeroImageOptions.cs                 # Configuration for asset paths
└── BarretApi.Infrastructure/
    ├── Fonts/
    │   ├── JetBrainsMono-Bold.ttf              # (new) Bundled font — title
    │   └── JetBrainsMono-Regular.ttf           # (new) Bundled font — subtitle
    └── Services/
        └── SkiaHeroImageGenerator.cs           # SkiaSharp composition implementation

tests/
├── BarretApi.Infrastructure.UnitTests/
│   └── Services/
│       └── SkiaHeroImageGenerator_GenerateAsync_Tests.cs
└── BarretApi.Api.UnitTests/
    └── Features/
        └── HeroImage/
            └── GenerateHeroImageValidator_Tests.cs
```

**Structure Decision**: Follows the existing multi-project Clean Architecture layout already in the repository. New files are added to existing projects — no new projects needed. The `Features/HeroImage/` folder in the API project follows the same feature-folder pattern used by `Features/WordCloud/` and `Features/SocialPost/`. Font files are embedded resources in the Infrastructure project since SkiaSharp rendering is Infrastructure's responsibility.

## Complexity Tracking

No constitution violations. All design decisions align with existing project patterns.

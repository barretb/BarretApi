# Implementation Plan: Standard Atom/RSS Feed Support

**Branch**: `006-atom-feed-support` | **Date**: 2026-03-14 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/006-atom-feed-support/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

Enhance the existing feed reader and RSS endpoint to support standard Atom 1.0/2.0 and RSS 2.0 feeds without custom namespace extensions. Changes span three areas: (1) `RssBlogFeedReader` gains fallback extraction for standard categories, standard media/enclosure images, and HTML-stripping for summaries; (2) `RssRandomPostService` removes the hard tag-requirement filter so tagless entries are eligible; (3) the `RssRandomPostRequest` gains an optional `Header` field that prepends caller-supplied text to the social post body. No new projects, no new infrastructure dependencies.

## Technical Context

**Language/Version**: C# (latest) on .NET 10.0 (`net10.0`), Aspire 13  
**Primary Dependencies**: FastEndpoints 8.0.0, System.ServiceModel.Syndication 8.0.0, AngleSharp 1.4.0 (already in solution for HTML processing)  
**Storage**: Azure Table Storage (existing — no schema changes needed; tracking is per-entry identity, unaffected by feed format)  
**Testing**: xUnit 2.9.3, NSubstitute 5.3.0, Shouldly 4.3.0  
**Target Platform**: Linux container / Windows via Aspire AppHost  
**Project Type**: Web service (API endpoints via FastEndpoints REPR pattern)  
**Performance Goals**: N/A (endpoint is invoked on-demand, not high-throughput)  
**Constraints**: Feed parsing must complete within existing HTTP timeout; no new NuGet packages required  
**Scale/Scope**: Single endpoint enhancement; ~4 files modified, ~2 new test classes

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| # | Principle | Status | Notes |
|---|-----------|--------|-------|
| I | Code Quality & Consistency | PASS | No new projects; all changes in existing files follow tab indentation, Allman braces, file-scoped namespaces, `_camelCase` fields. `dotnet format` will be run. |
| II | Clean Architecture & REPR | PASS | Endpoint uses FastEndpoints REPR. Request/Response suffixes maintained. Feed reader stays in Infrastructure; service stays in Core. No new interfaces needed — extending existing `IBlogFeedReader`. Header field added to existing `*Request` DTO. |
| III | Test-Driven Quality | PASS | New unit tests for: feed parsing fallback (categories, media, HTML strip), tag eligibility change, header prepend. xUnit + NSubstitute + Shouldly. Test naming: `ClassName_MethodName_Tests`. |
| IV | Centralized Config via Aspire | PASS | No new configuration parameters. Header is a per-request field, not config. Existing `BlogPromotionOptions` unchanged. |
| V | Secure by Design | PASS | XML parsing already disables DTD + external resolver. HTML stripping via AngleSharp (safe parser). No raw string queries. Input validation via FluentValidation. |
| VI | Observability & Structured Logging | PASS | Existing structured logging in `RssBlogFeedReader` unchanged. No new log points needed beyond existing patterns. |
| VII | Simplicity & Maintainability | PASS | Changes confined to ~4 production files. No new abstractions, no speculative features. Methods stay under 20 lines by extracting helpers (already exists: `ReadTags`, `ReadHeroImageUrl`). |

**Gate Result**: ALL PASS — proceed to Phase 0.

### Post-Phase 1 Re-check

All seven principles re-evaluated against concrete Phase 1 designs (data-model.md, contracts/, quickstart.md, research.md):

- **I**: No new files outside existing project structure; all changes in existing files.
- **II**: `BlogFeedEntry` model unchanged; `Header` flows Request → Query → Service per REPR. No new interfaces.
- **III**: Test plan covers category fallback, image fallback, HTML strip, tagless eligibility, header prepend.
- **IV**: No new configuration. Header is per-request.
- **V**: XML DTD disabled; HTML stripped via AngleSharp safe parser; enclosure/media URLs validated as absolute HTTP(S).
- **VI**: No logging changes needed; existing structured logging covers new code paths.
- **VII**: ~4 production files modified, 0 new abstractions, 0 new packages.

**Post-Phase 1 Gate Result**: ALL PASS — proceed to `/speckit.tasks`.

## Project Structure

### Documentation (this feature)

```text
specs/[###-feature]/
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
│   └── Features/SocialPost/
│       ├── RssRandomPostEndpoint.cs       # No changes
│       ├── RssRandomPostRequest.cs        # ADD: optional Header property
│       ├── RssRandomPostResponse.cs       # No changes
│       └── RssRandomPostValidator.cs      # No changes (Header is optional)
├── BarretApi.Core/
│   ├── Models/
│   │   └── BlogFeedEntry.cs              # No changes (Tags already optional list)
│   └── Services/
│       └── RssRandomPostService.cs       # MODIFY: remove tag-required filter; prepend header to post text
└── BarretApi.Infrastructure/
    └── Services/
        └── RssBlogFeedReader.cs          # MODIFY: add category fallback, media/enclosure image fallback, HTML strip

tests/
├── BarretApi.Infrastructure.UnitTests/
│   └── Services/
│       └── RssBlogFeedReader_ParseFeed_Tests.cs   # NEW: standard feed parsing, category fallback, image fallback, HTML strip
└── BarretApi.Core.UnitTests/
    └── Services/
        ├── RssRandomPostService_SelectAndPostAsync_Tests.cs  # MODIFY: add tests for tagless eligibility + header prepend
        └── BlogPromotionOrchestrator_BuildReminderPostText_Tests.cs  # No changes
```

**Structure Decision**: All changes fit within existing project structure. No new projects needed. Modified files: 3 production, 1–2 test files (1 new, 1 modified).

## Complexity Tracking

No constitution violations. All changes are within existing project boundaries, use existing dependencies, and follow established patterns. No justification entries needed.

# Implementation Plan: RSS Random Post

**Branch**: `002-rss-random-post` | **Date**: 2026-03-04 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/002-rss-random-post/spec.md`

## Summary

Add a new `POST /api/social-posts/rss-random` endpoint that accepts an RSS feed URL, fetches the feed, applies optional tag-exclusion and recency filters, selects one random entry from the eligible pool, and posts it to the targeted social platforms. The endpoint reuses the existing `SocialPostService` for platform publishing and follows the established REPR pattern with FastEndpoints.

## Technical Context

**Language/Version**: C# latest / .NET 10.0 (`net10.0`)
**Primary Dependencies**: FastEndpoints 8.x, FluentValidation (via FastEndpoints), System.ServiceModel.Syndication
**Storage**: N/A (stateless — no tracking of previously posted entries)
**Testing**: xUnit, NSubstitute, Shouldly
**Target Platform**: Linux/Windows server (Aspire-hosted web API)
**Project Type**: Web service (additional endpoint in existing API)
**Performance Goals**: Complete request (fetch feed + post to platforms) within 30 seconds per spec SC-001
**Constraints**: Must reuse existing `SocialPostService` infrastructure; no new NuGet packages needed
**Scale/Scope**: Single new endpoint with request/response models, one new core service, one new/extended feed reader interface

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| # | Principle | Status | Notes |
|---|-----------|--------|-------|
| I | Code Quality & Consistency | PASS | File-scoped namespaces, Allman bracing, naming conventions — standard compliance |
| II | Clean Architecture & REPR Design | PASS | New endpoint follows REPR via FastEndpoints; `*Request`/`*Response` suffixes; domain logic in Core; interfaces in Core, implementations in Infrastructure |
| III | Test-Driven Quality Assurance | PASS | Unit tests for filter logic, service orchestration; integration test for endpoint; xUnit/NSubstitute/Shouldly |
| IV | Centralized Configuration via Aspire | PASS | No new configuration needed — feed URL comes from request body, not appsettings |
| V | Secure by Design | PASS | API key auth required; FluentValidation on request; URL input validated |
| VI | Observability & Structured Logging | PASS | Structured logging in new service layer |
| VII | Simplicity & Maintainability | PASS | Single-responsibility service; methods under 20 lines; no speculative features |

**Gate result**: PASS — no violations. Proceeding to Phase 0.

## Project Structure

### Documentation (this feature)

```text
specs/002-rss-random-post/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   └── rss-random-post-endpoint.openapi.yaml
└── tasks.md             # Phase 2 output (NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
├── BarretApi.Api/
│   └── Features/
│       └── SocialPost/
│           ├── RssRandomPostEndpoint.cs       # NEW: FastEndpoints REPR endpoint
│           ├── RssRandomPostRequest.cs        # NEW: request model
│           ├── RssRandomPostResponse.cs       # NEW: response model
│           └── RssRandomPostValidator.cs      # NEW: FluentValidation validator
├── BarretApi.Core/
│   ├── Interfaces/
│   │   └── IBlogFeedReader.cs                 # MODIFIED: add URL-parameterized overload
│   └── Services/
│       └── RssRandomPostService.cs            # NEW: filter + random selection orchestration
└── BarretApi.Infrastructure/
    └── Services/
        └── RssBlogFeedReader.cs               # MODIFIED: implement new overload

tests/
├── BarretApi.Core.UnitTests/
│   └── Services/
│       └── RssRandomPostService_SelectAsync_Tests.cs  # NEW
└── BarretApi.Api.UnitTests/
    └── Features/
        └── SocialPost/
            └── RssRandomPostValidator_Tests.cs        # NEW
```

**Structure Decision**: Extends existing project structure — no new projects. The endpoint lives alongside existing `SocialPost` endpoints. Core orchestration logic is in a new `RssRandomPostService` in `BarretApi.Core`. The existing `IBlogFeedReader` gains a URL-parameterized overload, implemented in the existing `RssBlogFeedReader`.

## Complexity Tracking

> No constitution violations to justify.

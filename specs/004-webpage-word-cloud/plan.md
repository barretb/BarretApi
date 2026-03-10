# Implementation Plan: Webpage Word Cloud Generator

**Branch**: `004-webpage-word-cloud` | **Date**: 2026-03-09 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/004-webpage-word-cloud/spec.md`

## Summary

Add a new API endpoint that accepts a web page URL, fetches and parses the HTML to extract visible text, removes English stop words, counts word frequencies, and returns a PNG word cloud image where word size is proportional to frequency. Uses AngleSharp for HTML parsing, KnowledgePicker.WordCloud (SkiaSharp-based) for image generation, and an embedded static stop word list. Follows the existing REPR pattern with FastEndpoints, layered across Api/Core/Infrastructure.

## Technical Context

**Language/Version**: C# (latest) / .NET 10.0 (`net10.0`)
**Primary Dependencies**: FastEndpoints 8.x, AngleSharp 1.4.0 (HTML parsing), KnowledgePicker.WordCloud 1.3.2 (image generation), SkiaSharp 3.119.2 (already in project)
**Storage**: N/A — stateless request/response, no persistence
**Testing**: xUnit, NSubstitute, Shouldly (per constitution)
**Target Platform**: Linux server (Docker via Aspire), Windows development
**Project Type**: Web service (REST API endpoint)
**Performance Goals**: Response in under 15 seconds for typical pages (< 100 KB HTML)
**Constraints**: Max 500 KB HTML content processed, max 100 words in cloud, image dimensions 200–2000 px
**Scale/Scope**: Single new endpoint added to existing API; touches 3 existing projects (Api, Core, Infrastructure)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| # | Gate | Status | Notes |
|---|------|--------|-------|
| I | Code Quality — TreatWarningsAsErrors, file-scoped namespaces, Allman bracing, naming conventions | PASS | All new files will follow existing patterns |
| II | REPR pattern via FastEndpoints 7.x+ | PASS | New endpoint follows Request-Endpoint-Response with `*Request`/`*Response` suffixes |
| II | Interfaces and implementations in separate projects | PASS | Interfaces in Core, implementations in Infrastructure |
| II | DTOs with `*Details`/`*Summary` suffixes | N/A | No DTOs needed — request/response are API-layer messages, image is returned as bytes |
| II | Single responsibility, composition over inheritance | PASS | Separate services: HTML extraction, text processing, word cloud generation |
| III | xUnit, NSubstitute, Shouldly; no commercial test libs | PASS | All test projects already configured |
| III | Test naming: `ClassName_MethodName_Tests.cs` / `DoesSomething_GivenSomeCondition` | PASS | Will follow convention |
| IV | Configuration only in Aspire AppHost | PASS | No new configuration needed — uses HttpClient with default settings |
| V | Input validation, appropriate status codes | PASS | FluentValidation for URL format; 400/422 for invalid input, 502 for fetch failures |
| V | OWASP — validate/sanitize all inputs | PASS | URL whitelist (HTTP/HTTPS only), fetch timeout, content size limit |
| VI | Structured logging via ILogger | PASS | Logging at appropriate levels in service layer |
| VII | Methods under 20 lines, ≤ 4 parameters | PASS | Small focused methods; parameter object for word cloud options |

**Pre-Phase 0 gate result**: ALL PASS — no violations

## Project Structure

### Documentation (this feature)

```text
specs/004-webpage-word-cloud/
├── plan.md              # This file
├── research.md          # Phase 0 output — library selection decisions
├── data-model.md        # Phase 1 output — entity definitions
├── quickstart.md        # Phase 1 output — developer setup guide
├── contracts/           # Phase 1 output — OpenAPI contract
│   └── word-cloud.openapi.yaml
├── checklists/
│   └── requirements.md  # Specification quality checklist
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
src/
├── BarretApi.Api/
│   └── Features/
│       └── WordCloud/
│           ├── GenerateWordCloudEndpoint.cs     # FastEndpoints REPR endpoint
│           ├── GenerateWordCloudRequest.cs       # Request model (URL, width, height)
│           └── GenerateWordCloudValidator.cs     # FluentValidation rules
├── BarretApi.Core/
│   ├── Interfaces/
│   │   ├── IHtmlTextExtractor.cs                # Extract visible text from HTML
│   │   └── IWordCloudGenerator.cs               # Generate word cloud image bytes
│   ├── Models/
│   │   ├── WordCloudOptions.cs                  # Width, height, max words
│   │   └── WordFrequency.cs                     # Word + count pair
│   └── Services/
│       ├── EnglishStopWords.cs                  # Static FrozenSet of ~175 stop words
│       └── TextAnalysisService.cs               # Tokenize, clean, count, filter
├── BarretApi.Infrastructure/
│   └── Services/
│       ├── AngleSharpHtmlTextExtractor.cs        # IHtmlTextExtractor via AngleSharp
│       └── SkiaWordCloudGenerator.cs            # IWordCloudGenerator via KnowledgePicker

tests/
├── BarretApi.Core.UnitTests/
│   └── Services/
│       ├── EnglishStopWords_IsStopWord_Tests.cs
│       └── TextAnalysisService_AnalyzeText_Tests.cs
├── BarretApi.Infrastructure.UnitTests/
│   └── Services/
│       ├── AngleSharpHtmlTextExtractor_ExtractTextAsync_Tests.cs
│       └── SkiaWordCloudGenerator_GenerateAsync_Tests.cs
└── BarretApi.Api.UnitTests/
    └── Features/
        └── WordCloud/
            └── GenerateWordCloudValidator_Tests.cs
```

**Structure Decision**: Follows the existing layered architecture — Api references Core and Infrastructure. No new projects needed; all new code fits into existing projects. The feature adds a new `Features/WordCloud/` folder in Api (matching existing `Features/SocialPost/`, `Features/Nasa/` pattern), new interfaces and models in Core, and new service implementations in Infrastructure.

## Complexity Tracking

No constitution violations. No complexity justifications needed.

## Post-Design Constitution Re-Check

| # | Gate | Status | Notes |
|---|------|--------|-------|
| II | REPR pattern | PASS | `GenerateWordCloudEndpoint` extends `Endpoint<GenerateWordCloudRequest>` returning byte[] as PNG stream |
| II | Interfaces separate from implementations | PASS | `IHtmlTextExtractor` and `IWordCloudGenerator` in Core; implementations in Infrastructure |
| II | Single responsibility | PASS | 4 distinct classes: endpoint (HTTP), text extractor (HTML to text), text analyzer (text to frequencies), cloud generator (frequencies to image) |
| V | Security — SSRF mitigation | PASS | URL validated as HTTP/HTTPS, fetch timeout 30s, content size limit 500 KB, redirect limit 5 |
| VII | YAGNI | PASS | No speculative features; color/font customization explicitly deferred per spec |

**Post-design gate result**: ALL PASS

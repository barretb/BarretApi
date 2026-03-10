# Tasks: Webpage Word Cloud Generator

**Input**: Design documents from `/specs/004-webpage-word-cloud/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Not explicitly requested in specification. Test tasks are excluded.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Add new NuGet packages and prepare project references

- [x] T001 Add AngleSharp 1.4.0 and KnowledgePicker.WordCloud 1.3.2 to Directory.Packages.props
- [x] T002 Add AngleSharp and KnowledgePicker.WordCloud package references to src/BarretApi.Infrastructure/BarretApi.Infrastructure.csproj

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core models, interfaces, and services that ALL user stories depend on

**CRITICAL**: No user story work can begin until this phase is complete

- [x] T003 [P] Create WordFrequency record in src/BarretApi.Core/Models/WordFrequency.cs
- [x] T004 [P] Create WordCloudOptions class in src/BarretApi.Core/Models/WordCloudOptions.cs
- [x] T005 [P] Create IHtmlTextExtractor interface in src/BarretApi.Core/Interfaces/IHtmlTextExtractor.cs
- [x] T006 [P] Create IWordCloudGenerator interface in src/BarretApi.Core/Interfaces/IWordCloudGenerator.cs
- [x] T007 [P] Implement EnglishStopWords static class with FrozenSet of ~175 words in src/BarretApi.Core/Services/EnglishStopWords.cs
- [x] T008 Implement TextAnalysisService (tokenize, lowercase, strip punctuation, filter stop words, count, rank) in src/BarretApi.Core/Services/TextAnalysisService.cs
- [x] T009 Implement AngleSharpHtmlTextExtractor (fetch URL via HttpClient, parse with AngleSharp, strip script/style/noscript, return visible text) in src/BarretApi.Infrastructure/Services/AngleSharpHtmlTextExtractor.cs
- [x] T010 Implement SkiaWordCloudGenerator (accept WordFrequency list and WordCloudOptions, render via KnowledgePicker.WordCloud, return PNG bytes) in src/BarretApi.Infrastructure/Services/SkiaWordCloudGenerator.cs

**Checkpoint**: Foundation ready — all core services implemented, user story endpoint work can begin

---

## Phase 3: User Story 1 — Generate Word Cloud from URL (Priority: P1) MVP

**Goal**: User submits a URL and receives a PNG word cloud image with stop words excluded and words sized by frequency

**Independent Test**: Send a URL to a known web page, verify the response is a valid PNG image with Content-Type image/png

### Implementation for User Story 1

- [x] T011 [P] [US1] Create GenerateWordCloudRequest class (Url, Width?, Height?) in src/BarretApi.Api/Features/WordCloud/GenerateWordCloudRequest.cs
- [x] T012 [P] [US1] Create GenerateWordCloudValidator with Url required and valid absolute HTTP/HTTPS URI rule in src/BarretApi.Api/Features/WordCloud/GenerateWordCloudValidator.cs
- [x] T013 [US1] Implement GenerateWordCloudEndpoint (POST /api/word-cloud, auth required, orchestrate IHtmlTextExtractor → TextAnalysisService → IWordCloudGenerator, return PNG bytes with Content-Type image/png) in src/BarretApi.Api/Features/WordCloud/GenerateWordCloudEndpoint.cs
- [x] T014 [US1] Register IHtmlTextExtractor, IWordCloudGenerator, and TextAnalysisService in DI container in src/BarretApi.Api/Program.cs
- [x] T015 [US1] Verify dotnet build succeeds with zero errors and zero warnings

**Checkpoint**: User Story 1 is fully functional — POST /api/word-cloud returns a word cloud PNG for any valid URL

---

## Phase 4: User Story 2 — Handle Invalid or Unreachable URLs (Priority: P2)

**Goal**: Return clear, descriptive error responses for malformed URLs, unreachable pages, non-HTML content, and pages with insufficient text

**Independent Test**: Submit malformed URLs, unreachable domains, and image-only pages; verify each returns the correct status code and descriptive error message

### Implementation for User Story 2

- [x] T016 [US2] Add error handling in GenerateWordCloudEndpoint for HttpRequestException (502), InvalidOperationException for non-HTML (502), and insufficient text after analysis (422) in src/BarretApi.Api/Features/WordCloud/GenerateWordCloudEndpoint.cs
- [x] T017 [US2] Add timeout handling in AngleSharpHtmlTextExtractor (30s fetch timeout, max 5 redirects, max 500 KB content) in src/BarretApi.Infrastructure/Services/AngleSharpHtmlTextExtractor.cs
- [x] T018 [US2] Add structured logging for fetch failures, timeout, non-HTML content, and insufficient text scenarios in src/BarretApi.Api/Features/WordCloud/GenerateWordCloudEndpoint.cs

**Checkpoint**: All error paths return appropriate status codes (400, 422, 502) with descriptive messages

---

## Phase 5: User Story 3 — Customize Word Cloud Output (Priority: P3)

**Goal**: Users can optionally specify width and height for the generated image, with defaults of 800x600 and enforced min/max limits

**Independent Test**: Submit URLs with explicit width/height values and verify returned image dimensions match; submit out-of-range values and verify validation error

### Implementation for User Story 3

- [x] T019 [US3] Add Width and Height validation rules (min 200, max 2000 when provided) to GenerateWordCloudValidator in src/BarretApi.Api/Features/WordCloud/GenerateWordCloudValidator.cs
- [x] T020 [US3] Update GenerateWordCloudEndpoint to map request Width/Height (with defaults 800/600) into WordCloudOptions and pass to IWordCloudGenerator in src/BarretApi.Api/Features/WordCloud/GenerateWordCloudEndpoint.cs

**Checkpoint**: Custom dimensions work correctly; defaults apply when omitted; out-of-range values return 400

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Documentation, build validation, and final cleanup

- [x] T021 Update README.md with new POST /api/word-cloud endpoint documentation (request/response structure, example payloads, error codes)
- [x] T022 Run dotnet build and dotnet test to confirm zero errors and all existing tests pass
- [x] T023 Run dotnet format to confirm zero formatting violations
- [ ] T024 Validate quickstart.md steps work end-to-end against running API

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion — BLOCKS all user stories
- **User Stories (Phase 3–5)**: All depend on Foundational phase completion
  - US2 and US3 add to files created in US1, so execute sequentially: P1 → P2 → P3
- **Polish (Phase 6)**: Depends on all user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2) — creates the endpoint and core pipeline
- **User Story 2 (P2)**: Depends on US1 — adds error handling to the endpoint and extractor created in US1
- **User Story 3 (P3)**: Depends on US1 — adds dimension validation and options mapping to the endpoint created in US1

### Within Each User Story

- Models/request before validator
- Validator before endpoint
- Endpoint before DI registration
- Build verification after each story

### Parallel Opportunities

Within Phase 2 (Foundational):

- T003, T004, T005, T006, T007 can all run in parallel (different files, no dependencies)
- T008 depends on T003, T007 (uses WordFrequency and EnglishStopWords)
- T009 depends on T005 (implements IHtmlTextExtractor)
- T010 depends on T003, T004, T006 (implements IWordCloudGenerator using WordFrequency and WordCloudOptions)

Within Phase 3 (US1):

- T011, T012 can run in parallel (request and validator are separate files)
- T013 depends on T011, T012 (endpoint uses request + validator)
- T014 depends on T013 (register services after endpoint is ready)

---

## Parallel Example: Phase 2 (Foundational)

```text
# Batch 1 — all independent, different files:
T003: Create WordFrequency in src/BarretApi.Core/Models/WordFrequency.cs
T004: Create WordCloudOptions in src/BarretApi.Core/Models/WordCloudOptions.cs
T005: Create IHtmlTextExtractor in src/BarretApi.Core/Interfaces/IHtmlTextExtractor.cs
T006: Create IWordCloudGenerator in src/BarretApi.Core/Interfaces/IWordCloudGenerator.cs
T007: Create EnglishStopWords in src/BarretApi.Core/Services/EnglishStopWords.cs

# Batch 2 — depend on Batch 1 models/interfaces:
T008: Implement TextAnalysisService (needs T003, T007)
T009: Implement AngleSharpHtmlTextExtractor (needs T005)
T010: Implement SkiaWordCloudGenerator (needs T003, T004, T006)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (add NuGet packages)
2. Complete Phase 2: Foundational (models, interfaces, services)
3. Complete Phase 3: User Story 1 (endpoint + DI wiring)
4. **STOP and VALIDATE**: POST /api/word-cloud returns a valid PNG for a real URL
5. Deploy/demo if ready

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. Add User Story 1 → MVP: endpoint works for valid URLs
3. Add User Story 2 → Proper error handling for all failure modes
4. Add User Story 3 → Custom image dimensions
5. Polish → Documentation, build validation

---

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks
- [Story] label maps task to specific user story for traceability
- US2 and US3 modify files created in US1, so they must execute after US1
- No test tasks included — tests were not explicitly requested in the specification
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently

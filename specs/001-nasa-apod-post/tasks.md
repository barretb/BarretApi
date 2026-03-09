# Tasks: NASA APOD Social Posting

**Input**: Design documents from `/specs/001-nasa-apod-post/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/nasa-apod-post-endpoint.openapi.yaml, quickstart.md

**Tests**: Included — plan.md explicitly defines test file locations.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3, US4)
- Include exact file paths in descriptions

## Path Conventions

- **Source**: `src/BarretApi.{Layer}/`
- **Tests**: `tests/BarretApi.{Layer}.UnitTests/`
- **Config**: `Directory.Packages.props`, `Directory.Build.props`

---

## Phase 1: Setup (Package & Configuration Infrastructure)

**Purpose**: Add SkiaSharp packages and NASA API key configuration to the Aspire AppHost

- [x] T001 Add SkiaSharp 3.119.2 and SkiaSharp.NativeAssets.Linux.NoDependencies 3.119.2 package versions to Directory.Packages.props
- [x] T002 [P] Add SkiaSharp and SkiaSharp.NativeAssets.Linux.NoDependencies package references to src/BarretApi.Infrastructure/BarretApi.Infrastructure.csproj
- [x] T003 [P] Add NASA API key parameter and NasaApod configuration section to src/BarretApi.AppHost/Program.cs

---

## Phase 2: Foundational (Core Models & Interfaces)

**Purpose**: Create all domain models, configuration, and interface abstractions that ALL user stories depend on

**CRITICAL**: No user story work can begin until this phase is complete

- [x] T004 [P] Create ApodMediaType enum (Image, Video) in src/BarretApi.Core/Models/ApodMediaType.cs
- [x] T005 [P] Create ApodEntry model with Title, Date, Explanation, Url, HdUrl, MediaType, Copyright, ThumbnailUrl properties in src/BarretApi.Core/Models/ApodEntry.cs
- [x] T006 [P] Create ApodPostResult model with ApodEntry, PlatformResults, ImageAttached, ImageResized properties in src/BarretApi.Core/Models/ApodPostResult.cs
- [x] T007 [P] Create NasaApodOptions configuration class with ApiKey and BaseUrl properties in src/BarretApi.Core/Configuration/NasaApodOptions.cs
- [x] T008 [P] Create INasaApodClient interface with GetApodAsync(DateOnly?, CancellationToken) method in src/BarretApi.Core/Interfaces/INasaApodClient.cs
- [x] T009 [P] Create IImageResizer interface with ResizeToFit(byte[], long) method returning byte[] in src/BarretApi.Core/Interfaces/IImageResizer.cs

**Checkpoint**: All core models and interfaces defined — user story implementation can now begin

---

## Phase 3: User Story 1 — Post Today's APOD to Social Platforms (Priority: P1) :dart: MVP

**Goal**: Fetch today's NASA APOD (image type) and post it to selected social media platforms with image attachment, alt text from explanation, and HD link in post text

**Independent Test**: Call endpoint with `{}` body → verifies APOD fetched from NASA, image attached, posted to platforms, response includes APOD metadata and per-platform results

### Tests for User Story 1

> **Write these tests FIRST, ensure they FAIL before implementation**

- [x] T010 [P] [US1] Create NasaApodClient_GetApodAsync_Tests with tests for successful image APOD fetch, API error handling (403, 429, 500), and response deserialization in tests/BarretApi.Infrastructure.UnitTests/Nasa/NasaApodClient_GetApodAsync_Tests.cs
- [x] T011 [P] [US1] Create SkiaImageResizer_ResizeToFit_Tests with tests for under-limit passthrough, JPEG quality reduction, dimension reduction fallback, and null/invalid input in tests/BarretApi.Infrastructure.UnitTests/Services/SkiaImageResizer_ResizeToFit_Tests.cs
- [x] T012 [P] [US1] Create NasaApodPostService_PostAsync_Tests with tests for successful image APOD posting, post text format (Title + HdUrl), alt text from explanation, platform fan-out, and NASA API failure propagation in tests/BarretApi.Core.UnitTests/Services/NasaApodPostService_PostAsync_Tests.cs
- [x] T013 [P] [US1] Create NasaApodPostEndpoint_HandleAsync_Tests with tests for successful 200 response, empty request defaults to today, platform selection, and 422 on NASA API failure in tests/BarretApi.Api.UnitTests/Features/NasaApod/NasaApodPostEndpoint_HandleAsync_Tests.cs

### Implementation for User Story 1

- [x] T014 [P] [US1] Implement NasaApodClient with private ApodApiResponse DTO, HttpClient integration, thumbs=True parameter, and mapping to ApodEntry in src/BarretApi.Infrastructure/Nasa/NasaApodClient.cs
- [x] T015 [P] [US1] Implement SkiaImageResizer with quality-first strategy (85→45) then dimension reduction fallback, always outputting JPEG, per research.md in src/BarretApi.Infrastructure/Services/SkiaImageResizer.cs
- [x] T016 [P] [US1] Create NasaApodPostRequest model with optional Date (string?) and optional Platforms (List\<string\>?) properties in src/BarretApi.Api/Features/NasaApod/NasaApodPostRequest.cs
- [x] T017 [P] [US1] Create NasaApodPostResponse model with Title, Date, MediaType, ImageUrl, HdImageUrl, Copyright, ImageAttached, ImageResized, Results, PostedAt properties in src/BarretApi.Api/Features/NasaApod/NasaApodPostResponse.cs
- [x] T018 [US1] Create NasaApodPostValidator with platform name validation (bluesky, mastodon, linkedin) using FluentValidation in src/BarretApi.Api/Features/NasaApod/NasaApodPostValidator.cs
- [x] T019 [US1] Implement NasaApodPostService orchestrator: fetch APOD via INasaApodClient, build SocialPost with image URL + explanation as alt text + post text (Title + HdUrl/Url), resize via IImageResizer per platform limit, call SocialPostService.PostAsync, return ApodPostResult in src/BarretApi.Core/Services/NasaApodPostService.cs
- [x] T020 [US1] Create NasaApodPostEndpoint (REPR pattern) handling POST /api/social-posts/nasa-apod, mapping request to service call, mapping ApodPostResult to response, returning 200/207/422/502 per contract in src/BarretApi.Api/Features/NasaApod/NasaApodPostEndpoint.cs
- [x] T021 [US1] Register NasaApodOptions (IOptions\<T\>), NasaApodClient (HttpClient + INasaApodClient), SkiaImageResizer (IImageResizer), and NasaApodPostService in src/BarretApi.Api/Program.cs

**Checkpoint**: POST /api/social-posts/nasa-apod works for today's image APOD end-to-end. Run `dotnet test BarretApi.slnx` — all tests pass.

---

## Phase 4: User Story 2 — Post a Specific Date's APOD (Priority: P2)

**Goal**: Allow callers to specify a date parameter to fetch any historical APOD, with validation that the date is valid and in range

**Independent Test**: Call endpoint with `{"date": "2026-02-14"}` → verifies APOD for that specific date is returned and posted

### Tests for User Story 2

- [x] T022 [P] [US2] Add date validation tests: future date rejected, date before 1995-06-16 rejected, invalid format rejected, valid past date accepted, null date defaults to today in tests/BarretApi.Api.UnitTests/Features/NasaApod/NasaApodPostEndpoint_HandleAsync_Tests.cs
- [x] T023 [P] [US2] Add specific-date posting tests: service passes date to INasaApodClient, returned ApodEntry.Date matches requested date in tests/BarretApi.Core.UnitTests/Services/NasaApodPostService_PostAsync_Tests.cs

### Implementation for User Story 2

- [x] T024 [US2] Add date range validation rules to NasaApodPostValidator: reject future dates, reject dates before 1995-06-16, validate YYYY-MM-DD format parsing in src/BarretApi.Api/Features/NasaApod/NasaApodPostValidator.cs
- [x] T025 [US2] Add date parsing in NasaApodPostEndpoint to convert string date to DateOnly? before passing to service in src/BarretApi.Api/Features/NasaApod/NasaApodPostEndpoint.cs

**Checkpoint**: Date parameter works with validation. Invalid dates return 400 with clear messages. Run `dotnet test BarretApi.slnx`.

---

## Phase 5: User Story 3 — Graceful Video APOD Handling (Priority: P2)

**Goal**: When the APOD is a video (not an image), use the video thumbnail as the post image if available; otherwise post text-only with the video URL

**Independent Test**: Call endpoint with a known video APOD date → verifies thumbnail used as image or text-only post sent with video link

### Tests for User Story 3

- [x] T026 [P] [US3] Add video APOD tests: video with thumbnail attaches thumbnail image, video without thumbnail posts text-only, video post text includes video URL, ImageAttached reflects actual state in tests/BarretApi.Core.UnitTests/Services/NasaApodPostService_PostAsync_Tests.cs

### Implementation for User Story 3

- [x] T027 [US3] Add video media type handling to NasaApodPostService: when MediaType is Video, use ThumbnailUrl for image (if non-null); when ThumbnailUrl is null, build text-only SocialPost with video URL; set ImageAttached accordingly in src/BarretApi.Core/Services/NasaApodPostService.cs

**Checkpoint**: Video APODs handled gracefully — thumbnail or text-only. Run `dotnet test BarretApi.slnx`.

---

## Phase 6: User Story 4 — Copyright Attribution (Priority: P3)

**Goal**: Include a credit line in the post text when the APOD has a copyright holder, respecting intellectual property

**Independent Test**: Call endpoint with a copyrighted APOD date → verifies post text includes "Credit: {copyright holder}"; call with public domain APOD → no credit line

### Tests for User Story 4

- [x] T028 [P] [US4] Add copyright attribution tests: copyrighted APOD includes "Credit: {holder}" in post text, public domain APOD (no copyright) has no credit line, copyright text is included after title and URL in tests/BarretApi.Core.UnitTests/Services/NasaApodPostService_PostAsync_Tests.cs

### Implementation for User Story 4

- [x] T029 [US4] Add copyright credit line to post text construction in NasaApodPostService: append "\nCredit: {Copyright}" when ApodEntry.Copyright is not null in src/BarretApi.Core/Services/NasaApodPostService.cs

**Checkpoint**: Copyright attribution works correctly. Run `dotnet test BarretApi.slnx`.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Documentation, validation, and final quality checks

- [x] T030 [P] Add error-scenario tests: NASA API timeout, image download failure fallback to text-only, all platforms fail returns 502, partial success returns 207 in tests/BarretApi.Core.UnitTests/Services/NasaApodPostService_PostAsync_Tests.cs
- [x] T031 Update README.md with NASA APOD endpoint documentation: endpoint URL, request/response structure, example payloads, setup instructions per quickstart.md
- [x] T032 Run quickstart.md validation: build solution, verify endpoint responds, confirm error responses match contract

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion — BLOCKS all user stories
- **User Stories (Phase 3–6)**: All depend on Foundational phase completion
  - User stories can proceed in priority order (P1 → P2 → P3)
  - US3 and US4 modify the same service file as US1 — implement sequentially
- **Polish (Phase 7)**: Depends on all user stories being complete

### User Story Dependencies

- **US1 (P1)**: Can start after Foundational (Phase 2) — No dependencies on other stories. This is the MVP.
- **US2 (P2)**: Can start after US1 (modifies validator and endpoint created in US1)
- **US3 (P2)**: Can start after US1 (extends NasaApodPostService created in US1)
- **US4 (P3)**: Can start after US1 (extends NasaApodPostService created in US1). Can run in parallel with US2 and US3 if editing different sections.

### Within Each User Story

- Tests MUST be written and FAIL before implementation
- Models before services
- Services before endpoints
- Core implementation before DI registration
- Story complete before moving to next priority

### Parallel Opportunities

Within **Phase 2** (Foundational):

- T004–T009 all create separate files — run ALL in parallel

Within **Phase 3** (US1):

- T010–T013 (tests) can all run in parallel
- T014, T015, T016, T017 (client, resizer, request model, response model) can run in parallel (separate files)
- T018 depends on T016 (validator needs request model)
- T019 depends on T014, T015 (service needs client and resizer implementations)
- T020 depends on T016, T017, T018, T019 (endpoint needs request, response, validator, service)
- T021 depends on T014, T015, T019, T020 (registration needs all services and endpoint)

Within **Phase 4** (US2):

- T022, T023 (tests) can run in parallel
- T024, T025 depend on their respective test tasks

Within **Phase 5** (US3):

- T026 (test) then T027 (implementation)

Within **Phase 6** (US4):

- T028 (test) then T029 (implementation)

---

## Parallel Example: User Story 1

```text
# Batch 1 — Tests (all parallel, all separate files):
T010: NasaApodClient_GetApodAsync_Tests.cs
T011: SkiaImageResizer_ResizeToFit_Tests.cs
T012: NasaApodPostService_PostAsync_Tests.cs
T013: NasaApodPostEndpoint_HandleAsync_Tests.cs

# Batch 2 — Infrastructure + API models (all parallel, separate files):
T014: NasaApodClient.cs
T015: SkiaImageResizer.cs
T016: NasaApodPostRequest.cs
T017: NasaApodPostResponse.cs

# Batch 3 — Sequential (file dependencies):
T018: NasaApodPostValidator.cs (needs T016)
T019: NasaApodPostService.cs (needs T014, T015)

# Batch 4 — Sequential:
T020: NasaApodPostEndpoint.cs (needs T016–T019)

# Batch 5 — Final:
T021: Program.cs registration (needs all above)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (3 tasks)
2. Complete Phase 2: Foundational — 6 core model/interface tasks (BLOCKS all stories)
3. Complete Phase 3: User Story 1 — 12 tasks (tests + implementation)
4. **STOP and VALIDATE**: `dotnet test BarretApi.slnx` — all tests pass, endpoint works for image APODs
5. Deploy/demo if ready — this is a fully functional MVP

### Incremental Delivery

1. Setup + Foundational → Foundation ready (9 tasks)
2. Add US1 → Test independently → Deploy/Demo (**MVP!** — image APODs, today's date)
3. Add US2 → Test independently → Deploy/Demo (adds date parameter with validation)
4. Add US3 → Test independently → Deploy/Demo (adds video APOD handling)
5. Add US4 → Test independently → Deploy/Demo (adds copyright attribution)
6. Polish → Final validation → Done

Each story adds value without breaking previous stories.

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable
- Tests MUST fail before implementing
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- Post text format: `{Title}\n{HdUrl ?? Url}` (US1) + `\nCredit: {Copyright}` (US4)
- Image alt text: APOD `explanation` truncated to platform max (Bluesky 1000, Mastodon 1500, LinkedIn 4086)
- Image resize: quality-first (85→45) then dimension reduction — per research.md
- Refer to data-model.md for entity definitions and integration points
- Refer to research.md for SkiaSharp patterns and NASA API details
- Refer to contracts/nasa-apod-post-endpoint.openapi.yaml for exact request/response schemas

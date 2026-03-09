# Tasks: NASA GIBS Ohio Satellite Image Social Posting

**Input**: Design documents from `/specs/003-nasa-gibs-post/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Included — plan.md project structure explicitly lists test files for all layers.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Configuration class, core domain models, and interface that all subsequent phases depend on

- [ ] T001 Create NasaGibsOptions configuration class with defaults, SupportedLayers array, BBOX properties, and LayerStartDates dictionary in src/BarretApi.Core/Configuration/NasaGibsOptions.cs
- [ ] T002 [P] Create GibsSnapshotEntry sealed record (ImageBytes, Date, Layer, Width, Height, ContentType) in src/BarretApi.Core/Models/GibsSnapshotEntry.cs
- [ ] T003 [P] Create OhioSatellitePostResult sealed record (Date, Layer, WorldviewUrl, ImageWidth, ImageHeight, ImageAttached, ImageResized, PlatformResults) in src/BarretApi.Core/Models/OhioSatellitePostResult.cs
- [ ] T004 [P] Create INasaGibsClient interface with GetSnapshotAsync method in src/BarretApi.Core/Interfaces/INasaGibsClient.cs

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Infrastructure client, Aspire configuration, and DI registration that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [ ] T005 Implement NasaGibsClient sealed class (HttpClient, URL construction with REQUEST=GetSnapshot&SERVICE=WMS&CRS=EPSG:4326&FORMAT=image/jpeg, Content-Type XML error detection, returns GibsSnapshotEntry) in src/BarretApi.Infrastructure/Nasa/NasaGibsClient.cs
- [ ] T006 Write NasaGibsClient_GetSnapshotAsync_Tests (success returns GibsSnapshotEntry, XML error response throws InvalidOperationException, HTTP failure throws, correct URL construction with BBOX and TIME params) in tests/BarretApi.Infrastructure.UnitTests/Nasa/NasaGibsClient_GetSnapshotAsync_Tests.cs
- [ ] T007 Add GIBS configuration parameters (gibs-base-url, gibs-default-layer, gibs-bbox-south/west/north/east, gibs-image-width, gibs-image-height) and WithEnvironment mappings (NasaGibs__*) to src/BarretApi.AppHost/Program.cs
- [ ] T008 Register NasaGibsOptions via Configure, NasaGibsClient HttpClient with base URL and timeout, INasaGibsClient singleton, and NasaGibsPostService singleton in src/BarretApi.Api/Program.cs

**Checkpoint**: Foundation ready — GIBS client can fetch snapshots, configuration is wired, DI is complete

---

## Phase 3: User Story 1 — Post Recent Ohio Satellite Image (Priority: P1) 🎯 MVP

**Goal**: A user can call the endpoint with no parameters (or just platforms) and the system fetches yesterday's Ohio satellite image using the default MODIS Terra layer, constructs a post with descriptive caption and NASA acknowledgement, and posts to selected platforms

**Independent Test**: Call POST /api/social-posts/ohio-satellite with empty body; verify a satellite image is fetched from GIBS for yesterday's date using MODIS_Terra_CorrectedReflectance_TrueColor and posted to configured platforms

### Implementation for User Story 1

- [ ] T009 [US1] Implement NasaGibsPostService (primary constructor with INasaGibsClient, SocialPostService, IOptions\<NasaGibsOptions\>, ILogger; PostAsync resolves default date to yesterday UTC, default layer from options, calls GetSnapshotAsync, builds Worldview URL with lon/lat order, builds post text with NASA GIBS acknowledgement and hashtags, builds alt text, creates SocialPost with ImageData, calls SocialPostService.PostAsync, returns OhioSatellitePostResult) in src/BarretApi.Core/Services/NasaGibsPostService.cs
- [ ] T010 [US1] Write NasaGibsPostService_PostAsync_Tests (default date resolves to yesterday, default layer from options, Worldview URL has correct lon/lat BBOX order, post text includes date and layer and acknowledgement, alt text includes date and instrument, ImageData wraps snapshot bytes, SocialPostService called with correct SocialPost, result maps platform results correctly, GIBS client error propagates) in tests/BarretApi.Core.UnitTests/Services/NasaGibsPostService_PostAsync_Tests.cs
- [ ] T011 [P] [US1] Create OhioSatellitePostRequest DTO with optional Date (string?), Layer (string?), and Platforms (List\<string\>?) properties in src/BarretApi.Api/Features/Nasa/OhioSatellitePostRequest.cs
- [ ] T012 [P] [US1] Create OhioSatellitePostResponse DTO with Date, Layer, WorldviewUrl, ImageWidth, ImageHeight, ImageAttached, ImageResized, Results (List\<PlatformResult\>), PostedAt properties in src/BarretApi.Api/Features/Nasa/OhioSatellitePostResponse.cs
- [ ] T013 [US1] Create OhioSatellitePostValidator with platforms validation rules (each platform must be bluesky, mastodon, or linkedin; case-insensitive) in src/BarretApi.Api/Features/Nasa/OhioSatellitePostValidator.cs
- [ ] T014 [US1] Implement OhioSatellitePostEndpoint (POST /api/social-posts/ohio-satellite, requires auth, parses Date to DateOnly?, calls NasaGibsPostService.PostAsync, maps OhioSatellitePostResult to response, returns 200 if all succeed, 207 if partial, 502 if all fail, catches InvalidOperationException from GIBS and returns 422) in src/BarretApi.Api/Features/Nasa/OhioSatellitePostEndpoint.cs
- [ ] T015 [US1] Write OhioSatellitePostEndpoint_HandleAsync_Tests (all platforms succeed returns 200, partial success returns 207, all fail returns 502, GIBS error returns 422, response contains correct date/layer/worldviewUrl/imageAttached, empty body uses defaults) in tests/BarretApi.Api.UnitTests/Features/Nasa/OhioSatellitePostEndpoint_HandleAsync_Tests.cs
- [ ] T016 [US1] Write OhioSatellitePostValidator_Tests (empty request is valid, valid platforms accepted, invalid platform rejected, mixed valid/invalid platforms rejected, null platforms accepted) in tests/BarretApi.Api.UnitTests/Features/Nasa/OhioSatellitePostValidator_Tests.cs

**Checkpoint**: User Story 1 fully functional — POST /api/social-posts/ohio-satellite with empty body fetches yesterday's MODIS Terra image of Ohio and posts to all configured platforms

---

## Phase 4: User Story 2 — Post a Specific Date's Ohio Satellite Image (Priority: P2)

**Goal**: A user can specify a date in the request to post a particular day's satellite image instead of yesterday's default

**Independent Test**: Call POST /api/social-posts/ohio-satellite with `{"date": "2026-02-14"}` and verify the response date matches and the GIBS image was fetched for that date

### Implementation for User Story 2

- [ ] T017 [US2] Add date validation rules to OhioSatellitePostValidator (if provided: must parse as valid date, must not be in the future, must not be before the selected layer's earliest date from NasaGibsOptions.LayerStartDates; inject IOptions\<NasaGibsOptions\> into validator) in src/BarretApi.Api/Features/Nasa/OhioSatellitePostValidator.cs
- [ ] T018 [US2] Add date validation tests to OhioSatellitePostValidator_Tests (future date rejected, date before MODIS Terra start rejected, date before VIIRS NOAA21 start rejected, valid past date accepted, null date accepted, invalid date format rejected) in tests/BarretApi.Api.UnitTests/Features/Nasa/OhioSatellitePostValidator_Tests.cs
- [ ] T019 [US2] Add date-specific service tests to NasaGibsPostService_PostAsync_Tests (explicit date passed to GIBS client, Worldview URL contains specified date, response date matches specified date) in tests/BarretApi.Core.UnitTests/Services/NasaGibsPostService_PostAsync_Tests.cs
- [ ] T020 [US2] Add date-specific endpoint tests to OhioSatellitePostEndpoint_HandleAsync_Tests (request with date parses correctly, response date matches request date) in tests/BarretApi.Api.UnitTests/Features/Nasa/OhioSatellitePostEndpoint_HandleAsync_Tests.cs

**Checkpoint**: User Stories 1 AND 2 both work — users can post yesterday's or a specific date's Ohio satellite image

---

## Phase 5: User Story 3 — Configurable Imagery Layer (Priority: P3)

**Goal**: A user can specify which satellite imagery layer to use instead of the default MODIS Terra true-color layer

**Independent Test**: Call POST /api/social-posts/ohio-satellite with `{"layer": "VIIRS_SNPP_CorrectedReflectance_TrueColor"}` and verify the response layer matches and the correct layer was used for the GIBS snapshot

### Implementation for User Story 3

- [ ] T021 [US3] Add layer validation rules to OhioSatellitePostValidator (if provided: must be in NasaGibsOptions.SupportedLayers; error message lists all supported layers) in src/BarretApi.Api/Features/Nasa/OhioSatellitePostValidator.cs
- [ ] T022 [US3] Add layer validation tests to OhioSatellitePostValidator_Tests (unsupported layer rejected with supported list in error, each supported layer accepted, null layer accepted) in tests/BarretApi.Api.UnitTests/Features/Nasa/OhioSatellitePostValidator_Tests.cs
- [ ] T023 [US3] Add layer-specific service tests to NasaGibsPostService_PostAsync_Tests (explicit layer passed to GIBS client, Worldview URL contains specified layer, null layer defaults to configured default) in tests/BarretApi.Core.UnitTests/Services/NasaGibsPostService_PostAsync_Tests.cs
- [ ] T024 [US3] Add layer-specific endpoint tests to OhioSatellitePostEndpoint_HandleAsync_Tests (request with layer passes through, response layer matches request layer) in tests/BarretApi.Api.UnitTests/Features/Nasa/OhioSatellitePostEndpoint_HandleAsync_Tests.cs

**Checkpoint**: All three user stories functional — users can post with default or specific date and default or specific layer

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Documentation, validation, and final verification

- [X] T025 [P] Update README.md with Ohio satellite endpoint documentation (POST /api/social-posts/ohio-satellite, request/response structure, example payloads for empty body and full options, supported layers table, configuration overrides table)
- [X] T026 Run quickstart.md validation — verify all documented requests and responses match the implemented API behavior
- [X] T027 Build verification — run dotnet build and dotnet test on BarretApi.slnx, ensure 0 errors, 0 warnings, all tests pass

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion — BLOCKS all user stories
- **User Stories (Phases 3–5)**: All depend on Foundational phase completion
  - User stories proceed sequentially in priority order (P1 → P2 → P3) because US2 and US3 add validation rules to files created in US1
- **Polish (Phase 6)**: Depends on all user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2) — creates all new API files (endpoint, request, response, validator, service)
- **User Story 2 (P2)**: Depends on User Story 1 — adds date validation rules to the validator created in US1 and adds tests to test files created in US1
- **User Story 3 (P3)**: Depends on User Story 1 — adds layer validation rules to the validator created in US1 and adds tests to test files created in US1; can run in parallel with US2 if coordinated carefully (different validation rules in same file)

### Within Each User Story

- Models/DTOs before services
- Services before endpoints
- Implementation before tests (tests validate the implementation)
- Validator rules before validator tests
- Story checkpoint before moving to next priority

### Parallel Opportunities

- **Phase 1**: T002, T003, T004 can all run in parallel (different files, no dependencies)
- **Phase 3 (US1)**: T011, T012 can run in parallel (request and response DTOs are independent files)
- **Phase 4 and Phase 5**: US2 and US3 touch the same files (validator, test files) — run sequentially unless coordinated
- **Phase 6**: T026 (README) can run in parallel with T027 (quickstart validation)

---

## Parallel Example: Phase 1

```bash
# Launch all independent model/interface files together:
Task T002: "Create GibsSnapshotEntry sealed record in src/BarretApi.Core/Models/GibsSnapshotEntry.cs"
Task T003: "Create OhioSatellitePostResult sealed record in src/BarretApi.Core/Models/OhioSatellitePostResult.cs"
Task T004: "Create INasaGibsClient interface in src/BarretApi.Core/Interfaces/INasaGibsClient.cs"
```

## Parallel Example: User Story 1

```bash
# Launch request and response DTOs together:
Task T011: "Create OhioSatellitePostRequest DTO in src/BarretApi.Api/Features/Nasa/OhioSatellitePostRequest.cs"
Task T012: "Create OhioSatellitePostResponse DTO in src/BarretApi.Api/Features/Nasa/OhioSatellitePostResponse.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (NasaGibsOptions, models, interface)
2. Complete Phase 2: Foundational (NasaGibsClient, AppHost config, DI registration)
3. Complete Phase 3: User Story 1 (service, endpoint, validator, tests)
4. **STOP and VALIDATE**: POST /api/social-posts/ohio-satellite with empty body → yesterday's MODIS Terra image posted
5. Deploy/demo if ready — endpoint works with all defaults

### Incremental Delivery

1. Setup + Foundational → Infrastructure ready
2. Add User Story 1 → Full endpoint works with defaults → Deploy/Demo (**MVP!**)
3. Add User Story 2 → Date validation rules + tests → Date parameter validated and functional
4. Add User Story 3 → Layer validation rules + tests → Layer parameter validated and functional
5. Polish → README docs + build verification
6. Each story adds validation depth without breaking previous functionality

---

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks
- [Story] label maps task to specific user story for traceability
- No API key needed for NASA GIBS — configuration is simpler than APOD
- The Worldview URL uses lon/lat order (minLon,minLat,maxLon,maxLat) — opposite of WMS BBOX (minLat,minLon,maxLat,maxLon)
- GIBS returns 200 OK with blank images for dates with no data — date validation prevents this for pre-layer-start dates and future dates
- All 5 CorrectedReflectance TrueColor layers tested and confirmed working via research
- JPEG snapshots at 1024×768 are typically 80–400 KB — well within all platform limits (Bluesky 1 MB, Mastodon 16 MB, LinkedIn 20 MB)
- Follows proven APOD feature pattern: sealed records, REPR endpoint, primary constructors with readonly fields
- Commit after each task or logical group
- Stop at any checkpoint to validate the story independently

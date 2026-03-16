# Tasks: DiceBear Random Avatar

**Input**: Design documents from `/specs/007-dicebear-avatar/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Included — plan.md explicitly defines test files for this feature.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create folder structure and shared model types used by all user stories

- [x] T001 Create Avatar feature folder at src/BarretApi.Api/Features/Avatar/
- [x] T002 Create DiceBear infrastructure folder at src/BarretApi.Infrastructure/DiceBear/
- [x] T003 [P] Create AvatarFormat enum with content type mapping in src/BarretApi.Core/Models/AvatarFormat.cs
- [x] T004 [P] Create AvatarStyle static class with all 32 style constants and validation in src/BarretApi.Core/Models/AvatarStyle.cs
- [x] T005 [P] Create AvatarResult domain model in src/BarretApi.Core/Models/AvatarResult.cs

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core interface and infrastructure client that ALL user stories depend on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T006 Create IDiceBearAvatarClient interface in src/BarretApi.Core/Interfaces/IDiceBearAvatarClient.cs
- [x] T007 Implement DiceBearAvatarClient with typed HttpClient in src/BarretApi.Infrastructure/DiceBear/DiceBearAvatarClient.cs
- [x] T008 Register IDiceBearAvatarClient and typed HttpClient in DI in src/BarretApi.Api/Program.cs
- [x] T009 Create GenerateAvatarRequest with optional Style, Format, and Seed properties in src/BarretApi.Api/Features/Avatar/GenerateAvatarRequest.cs
- [x] T010 Create GenerateAvatarResponse placeholder class in src/BarretApi.Api/Features/Avatar/GenerateAvatarResponse.cs

**Checkpoint**: Foundation ready — DiceBear client can fetch avatars; endpoint scaffolding in place

---

## Phase 3: User Story 1 — Generate a Random Avatar (Priority: P1) 🎯 MVP

**Goal**: Call `GET /api/avatars/random` with no parameters and receive a randomly generated avatar image (random style, random seed, SVG format)

**Independent Test**: `curl http://localhost:5000/api/avatars/random` returns a valid SVG image with `Content-Type: image/svg+xml`

### Tests for User Story 1

- [x] T011 [P] [US1] Create DiceBearAvatarClient_GetAvatarAsync_Tests class with test for successful avatar fetch in tests/BarretApi.Infrastructure.UnitTests/DiceBear/DiceBearAvatarClient_GetAvatarAsync_Tests.cs
- [x] T012 [P] [US1] Add test for upstream error handling (502 response) in tests/BarretApi.Infrastructure.UnitTests/DiceBear/DiceBearAvatarClient_GetAvatarAsync_Tests.cs
- [x] T013 [P] [US1] Create GenerateAvatarEndpoint_HandleAsync_Tests class with test for random avatar generation in tests/BarretApi.Api.UnitTests/Features/Avatar/GenerateAvatarEndpoint_HandleAsync_Tests.cs

### Implementation for User Story 1

- [x] T014 [US1] Implement random style selection and random seed generation in DiceBearAvatarClient in src/BarretApi.Infrastructure/DiceBear/DiceBearAvatarClient.cs
- [x] T015 [US1] Implement upstream URL construction and image byte fetching in DiceBearAvatarClient in src/BarretApi.Infrastructure/DiceBear/DiceBearAvatarClient.cs
- [x] T016 [US1] Implement upstream error handling (non-success status codes, HttpRequestException) in DiceBearAvatarClient in src/BarretApi.Infrastructure/DiceBear/DiceBearAvatarClient.cs
- [x] T017 [US1] Implement GenerateAvatarEndpoint with GET /api/avatars/random route and HandleAsync returning image bytes via SendBytesAsync in src/BarretApi.Api/Features/Avatar/GenerateAvatarEndpoint.cs
- [x] T018 [US1] Add structured logging for upstream requests and errors in DiceBearAvatarClient in src/BarretApi.Infrastructure/DiceBear/DiceBearAvatarClient.cs

**Checkpoint**: `GET /api/avatars/random` returns a random SVG avatar — MVP is functional

---

## Phase 4: User Story 2 — Choose a Specific Avatar Style (Priority: P2)

**Goal**: Call `GET /api/avatars/random?style=pixel-art` and receive an avatar in the specified style; invalid styles return 400 with valid style list

**Independent Test**: `curl "http://localhost:5000/api/avatars/random?style=pixel-art"` returns a pixel-art avatar; `curl "http://localhost:5000/api/avatars/random?style=invalid"` returns 400

### Tests for User Story 2

- [x] T019 [P] [US2] Create GenerateAvatarValidator_Tests class with test for valid style acceptance in tests/BarretApi.Api.UnitTests/Features/Avatar/GenerateAvatarValidator_Tests.cs
- [x] T020 [P] [US2] Add test for invalid style rejection with error listing valid styles in tests/BarretApi.Api.UnitTests/Features/Avatar/GenerateAvatarValidator_Tests.cs

### Implementation for User Story 2

- [x] T021 [US2] Create GenerateAvatarValidator with style validation rule (must be in AvatarStyle.All or null) in src/BarretApi.Api/Features/Avatar/GenerateAvatarValidator.cs
- [x] T022 [US2] Add endpoint logic to pass specified style to DiceBearAvatarClient when style parameter is provided in src/BarretApi.Api/Features/Avatar/GenerateAvatarEndpoint.cs
- [x] T023 [P] [US2] Create AvatarStyle_Tests class verifying All collection contains 32 styles and IsValid method works in tests/BarretApi.Core.UnitTests/Models/AvatarStyle_Tests.cs

**Checkpoint**: Style selection works; invalid styles return actionable 400 error

---

## Phase 5: User Story 3 — Choose an Image Format (Priority: P3)

**Goal**: Call `GET /api/avatars/random?format=png` and receive a PNG avatar with `Content-Type: image/png`; default to SVG; invalid formats return 400

**Independent Test**: `curl "http://localhost:5000/api/avatars/random?format=png" --output avatar.png` returns a valid PNG image

### Tests for User Story 3

- [x] T024 [P] [US3] Add test for valid format acceptance and default SVG in tests/BarretApi.Api.UnitTests/Features/Avatar/GenerateAvatarValidator_Tests.cs
- [x] T025 [P] [US3] Add test for invalid format rejection with error listing valid formats in tests/BarretApi.Api.UnitTests/Features/Avatar/GenerateAvatarValidator_Tests.cs
- [x] T026 [P] [US3] Add test for correct content type mapping per format in tests/BarretApi.Infrastructure.UnitTests/DiceBear/DiceBearAvatarClient_GetAvatarAsync_Tests.cs

### Implementation for User Story 3

- [x] T027 [US3] Add format validation rule to GenerateAvatarValidator (must be in supported formats or null) in src/BarretApi.Api/Features/Avatar/GenerateAvatarValidator.cs
- [x] T028 [US3] Update DiceBearAvatarClient to use format in URL path and set correct content type in AvatarResult in src/BarretApi.Infrastructure/DiceBear/DiceBearAvatarClient.cs
- [x] T029 [US3] Update GenerateAvatarEndpoint to pass format to client and set response Content-Type header accordingly in src/BarretApi.Api/Features/Avatar/GenerateAvatarEndpoint.cs

**Checkpoint**: Format selection works; correct content types returned; SVG default maintained

---

## Phase 6: User Story 4 — Reproducible Avatar via Seed (Priority: P4)

**Goal**: Call `GET /api/avatars/random?seed=john-doe&style=pixel-art` twice and receive identical avatars; seed > 256 chars returns 400

**Independent Test**: Two identical requests with `seed=test-user&style=bottts` return byte-identical responses

### Tests for User Story 4

- [x] T030 [P] [US4] Add test for seed length validation (max 256 characters) in tests/BarretApi.Api.UnitTests/Features/Avatar/GenerateAvatarValidator_Tests.cs
- [x] T031 [P] [US4] Add test for seed value passed to upstream URL in tests/BarretApi.Infrastructure.UnitTests/DiceBear/DiceBearAvatarClient_GetAvatarAsync_Tests.cs

### Implementation for User Story 4

- [x] T032 [US4] Add seed length validation rule to GenerateAvatarValidator (max 256 characters) in src/BarretApi.Api/Features/Avatar/GenerateAvatarValidator.cs
- [x] T033 [US4] Update DiceBearAvatarClient to pass seed as query parameter to upstream URL (URL-encode special characters) in src/BarretApi.Infrastructure/DiceBear/DiceBearAvatarClient.cs
- [x] T034 [US4] Update GenerateAvatarEndpoint to pass seed to client (use provided seed or generate random GUID) in src/BarretApi.Api/Features/Avatar/GenerateAvatarEndpoint.cs

**Checkpoint**: Seed-based reproducibility works; same seed+style = same avatar; seed validation enforced

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Final validation, documentation, and cleanup

- [x] T035 [P] Add Swagger/OpenAPI summary and description to GenerateAvatarEndpoint in src/BarretApi.Api/Features/Avatar/GenerateAvatarEndpoint.cs
- [x] T036 [P] Update README.md with avatar endpoint documentation (URL, parameters, examples, response types)
- [x] T037 Run `dotnet format` and fix any formatting violations
- [x] T038 Run `dotnet build` and verify zero errors and zero warnings
- [x] T039 Run `dotnet test` and verify all tests pass
- [x] T040 Run quickstart.md validation — execute all curl examples and verify expected behavior

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 (folder structure and models exist) — BLOCKS all user stories
- **User Story 1 (Phase 3)**: Depends on Phase 2 — delivers MVP
- **User Story 2 (Phase 4)**: Depends on Phase 2 — can run in parallel with US1 but shares endpoint file
- **User Story 3 (Phase 5)**: Depends on Phase 2 — can run in parallel with US1 but shares client and endpoint files
- **User Story 4 (Phase 6)**: Depends on Phase 2 — can run in parallel with US1 but shares validator and client files
- **Polish (Phase 7)**: Depends on all user stories being complete

### Within Each User Story

- Tests written first to establish expected behavior
- Models/infrastructure before endpoint logic
- Validation before endpoint integration
- Story complete before moving to next priority

### Parallel Opportunities

**Phase 1** (all [P] tasks):

```text
T003 (AvatarFormat) | T004 (AvatarStyle) | T005 (AvatarResult)
```

**Phase 3** (tests in parallel):

```text
T011 (client test) | T012 (error test) | T013 (endpoint test)
```

**Phase 4** (tests in parallel):

```text
T019 (valid style test) | T020 (invalid style test) | T023 (AvatarStyle tests)
```

**Phase 5** (tests in parallel):

```text
T024 (valid format test) | T025 (invalid format test) | T026 (content type test)
```

**Phase 6** (tests in parallel):

```text
T030 (seed length test) | T031 (seed URL test)
```

**Phase 7** (independent [P] tasks):

```text
T035 (Swagger) | T036 (README)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001–T005)
2. Complete Phase 2: Foundational (T006–T010)
3. Complete Phase 3: User Story 1 (T011–T018)
4. **STOP and VALIDATE**: `GET /api/avatars/random` returns a valid SVG avatar
5. Deploy/demo if ready — a fully functional random avatar endpoint

### Incremental Delivery

1. Setup + Foundational → Infrastructure ready
2. Add User Story 1 → Random avatar works → MVP deployed
3. Add User Story 2 → Style selection works → Enhanced
4. Add User Story 3 → Format selection works → Full flexibility
5. Add User Story 4 → Seed reproducibility works → Complete feature
6. Polish → Documentation, formatting, final validation

### Sequential Recommendation

Since all user stories share the same endpoint, client, and validator files, **sequential implementation in priority order (P1 → P2 → P3 → P4) is recommended** to avoid merge conflicts on shared files.

---

## Notes

- [P] tasks = different files, no dependencies between them
- [Story] label maps each task to its user story for traceability
- No new NuGet packages needed — all dependencies already in Directory.Packages.props
- No new projects needed — all code goes into existing projects
- No Aspire AppHost changes needed — DiceBear API uses no secrets or configuration
- Commit after each phase or logical group of tasks

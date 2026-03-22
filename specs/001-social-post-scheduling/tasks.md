# Tasks: Scheduled Social Post Publishing

**Input**: Design documents from `/specs/001-social-post-scheduling/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/, quickstart.md

**Tests**: No explicit TDD or test-first requirement was stated in spec.md, so test tasks are not mandated in this task list.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (`[US1]`, `[US2]`, `[US3]`)
- Every task includes concrete file path(s)

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Establish scheduling configuration and feature scaffolding.

- [X] T001 Add scheduled-post table settings section to src/BarretApi.AppHost/appsettings.json
- [X] T002 Add scheduled-post table settings section to src/BarretApi.AppHost/appsettings.Development.json
- [X] T003 Add ScheduledSocialPostOptions configuration model in src/BarretApi.Core/Configuration/ScheduledSocialPostOptions.cs
- [X] T004 [P] Add processing endpoint contract documentation updates in specs/001-social-post-scheduling/contracts/scheduled-social-post-api.md

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Build core scheduling domain and persistence abstractions required by all stories.

**CRITICAL**: No user story implementation should begin before this phase is complete.

- [X] T005 Create scheduled status enum in src/BarretApi.Core/Models/ScheduledPostStatus.cs
- [X] T006 Create scheduled record model in src/BarretApi.Core/Models/ScheduledSocialPostRecord.cs
- [X] T007 [P] Create scheduled failure model in src/BarretApi.Core/Models/ScheduledPostFailureDetails.cs
- [X] T008 [P] Create processing summary model in src/BarretApi.Core/Models/ScheduledPostProcessingSummary.cs
- [X] T009 Create scheduled repository interface in src/BarretApi.Core/Interfaces/IScheduledSocialPostRepository.cs
- [X] T010 [P] Create scheduled processor interface in src/BarretApi.Core/Interfaces/IScheduledSocialPostProcessor.cs
- [X] T011 Implement Azure Table scheduled repository in src/BarretApi.Infrastructure/Services/AzureTableScheduledSocialPostRepository.cs
- [X] T012 Register scheduled options and repository dependencies in src/BarretApi.Api/Program.cs

**Checkpoint**: Foundational scheduling types and persistence are available for story work.

---

## Phase 3: User Story 1 - Schedule a Post for Later (Priority: P1) 🎯 MVP

**Goal**: Allow both create social post APIs to accept optional `scheduledFor` and persist future-dated posts instead of posting immediately.

**Independent Test**: Submit one JSON and one multipart request with future `scheduledFor`; verify both are stored as pending and no platform publish occurs during create call.

### Implementation for User Story 1

- [X] T013 [US1] Add optional scheduledFor to JSON request DTO in src/BarretApi.Api/Features/SocialPost/CreateSocialPostRequest.cs
- [X] T014 [US1] Add optional scheduledFor to multipart request DTO in src/BarretApi.Api/Features/SocialPost/CreateSocialPostUploadEndpoint.cs
- [X] T015 [US1] Add scheduledFor future validation rule to JSON validator in src/BarretApi.Api/Features/SocialPost/CreateSocialPostValidator.cs
- [X] T016 [US1] Add scheduled create result fields to response DTO in src/BarretApi.Api/Features/SocialPost/CreateSocialPostResponse.cs
- [X] T017 [US1] Implement JSON create branching for immediate vs scheduled in src/BarretApi.Api/Features/SocialPost/CreateSocialPostEndpoint.cs
- [X] T018 [US1] Implement multipart create branching for immediate vs scheduled in src/BarretApi.Api/Features/SocialPost/CreateSocialPostUploadEndpoint.cs
- [X] T019 [US1] Add scheduled post creation method to service in src/BarretApi.Core/Services/SocialPostService.cs
- [X] T020 [US1] Extend social post model with schedule metadata in src/BarretApi.Core/Models/SocialPost.cs
- [X] T021 [US1] Wire scheduled record mapping and save logic in src/BarretApi.Core/Services/SocialPostService.cs
- [X] T022 [US1] Update quickstart request/response examples for scheduled create in specs/001-social-post-scheduling/quickstart.md

**Checkpoint**: Scheduled creation works independently while preserving immediate post behavior.

---

## Phase 4: User Story 2 - Publish Due Scheduled Posts (Priority: P2)

**Goal**: Add a trigger endpoint that publishes all due scheduled posts and updates durable status to prevent duplicate posting.

**Independent Test**: Seed due and non-due scheduled records, invoke processing endpoint, verify only due records are attempted and successful records are marked published.

### Implementation for User Story 2

- [X] T023 [US2] Add due-query and claim/update methods to repository interface in src/BarretApi.Core/Interfaces/IScheduledSocialPostRepository.cs
- [X] T024 [US2] Implement due-query and claim/update methods in src/BarretApi.Infrastructure/Services/AzureTableScheduledSocialPostRepository.cs
- [X] T025 [US2] Create processing request DTO in src/BarretApi.Api/Features/SocialPost/ProcessScheduledPostsRequest.cs
- [X] T026 [US2] Create processing response DTO in src/BarretApi.Api/Features/SocialPost/ProcessScheduledPostsResponse.cs
- [X] T027 [US2] Create processing request validator in src/BarretApi.Api/Features/SocialPost/ProcessScheduledPostsValidator.cs
- [X] T028 [US2] Implement scheduled processor orchestration in src/BarretApi.Core/Services/ScheduledSocialPostProcessor.cs
- [X] T029 [US2] Register scheduled processor dependency in src/BarretApi.Api/Program.cs
- [X] T030 [US2] Implement due-processing endpoint in src/BarretApi.Api/Features/SocialPost/ProcessScheduledPostsEndpoint.cs
- [X] T031 [US2] Add endpoint summary and response codes in src/BarretApi.Api/Features/SocialPost/ProcessScheduledPostsEndpoint.cs

**Checkpoint**: Due scheduled posts can be processed and published via dedicated endpoint with idempotent status transitions.

---

## Phase 5: User Story 3 - Track Processing Outcomes (Priority: P3)

**Goal**: Return detailed run metrics and failure diagnostics so operators can understand what happened in each processing run.

**Independent Test**: Run processing where at least one scheduled post fails; verify response includes attempted/succeeded/failed/skipped counts and per-post failure details.

### Implementation for User Story 3

- [X] T032 [US3] Extend processing summary model with full run counters in src/BarretApi.Core/Models/ScheduledPostProcessingSummary.cs
- [X] T033 [US3] Capture per-post failure details in processor flow in src/BarretApi.Core/Services/ScheduledSocialPostProcessor.cs
- [X] T034 [US3] Map processor summary to API response in src/BarretApi.Api/Features/SocialPost/ProcessScheduledPostsEndpoint.cs
- [X] T035 [US3] Return 502 when all due attempts fail and 200 otherwise in src/BarretApi.Api/Features/SocialPost/ProcessScheduledPostsEndpoint.cs
- [X] T036 [US3] Add structured run logging for counts and failures in src/BarretApi.Core/Services/ScheduledSocialPostProcessor.cs
- [X] T037 [US3] Update quickstart expected outcome matrix for failure visibility in specs/001-social-post-scheduling/quickstart.md

**Checkpoint**: Processing endpoint returns complete operational outcome details for each run.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Harden the feature across stories and ensure implementation readiness.

- [X] T038 [P] Add XML docs and inline clarifying comments for new scheduling models in src/BarretApi.Core/Models/ScheduledSocialPostRecord.cs
- [X] T039 [P] Refine API examples to show scheduled and immediate response parity in src/BarretApi.Api/Features/SocialPost/CreateSocialPostEndpoint.cs
- [X] T040 Validate quickstart end-to-end against implemented endpoints in specs/001-social-post-scheduling/quickstart.md
- [X] T041 Run full test suite and fix regressions in tests/BarretApi.Api.UnitTests/ and tests/BarretApi.Core.UnitTests/ and tests/BarretApi.Infrastructure.UnitTests/
- [X] T042 Update feature section documentation for scheduled posting in README.md

---

## Dependencies & Execution Order

### Phase Dependencies

- Setup (Phase 1) has no dependencies and starts immediately.
- Foundational (Phase 2) depends on Setup and blocks all user story phases.
- User Story phases (Phase 3, 4, 5) depend on Phase 2 completion.
- Polish (Phase 6) depends on completion of the stories in scope.

### User Story Dependencies

- **US1 (P1)**: Depends only on Foundational phase; provides MVP scheduling value.
- **US2 (P2)**: Depends on Foundational phase and uses scheduled records created by US1.
- **US3 (P3)**: Depends on US2 processing flow being in place.

### Task Ordering Notes

- In US1, request/validator/response updates (T013-T016) precede endpoint/service branching (T017-T021).
- In US2, repository capabilities (T023-T024) precede processor/endpoint (T028-T031).
- In US3, model and processor enrichment (T032-T033) precede endpoint response/status mapping (T034-T035).

---

## Parallel Opportunities

- **Setup**: T004 can run in parallel with T001-T003.
- **Foundational**: T007, T008, and T010 can run in parallel after T005 starts the model baseline.
- **US1**: T013, T014, and T015 can run in parallel; T016 can start once DTO shape is finalized.
- **US2**: T025, T026, and T027 can run in parallel while repository methods are being implemented.
- **US3**: T036 can run in parallel with T034-T035 after processor summary fields exist.
- **Polish**: T038 and T039 can run in parallel.

## Parallel Example: User Story 1

```bash
# Parallel DTO/validation work
Task T013: Add optional scheduledFor to JSON request DTO
Task T014: Add optional scheduledFor to multipart request DTO
Task T015: Add scheduledFor validation rule
```

## Parallel Example: User Story 2

```bash
# Parallel endpoint contract work while repository is being finalized
Task T025: Create processing request DTO
Task T026: Create processing response DTO
Task T027: Create processing validator
```

## Parallel Example: User Story 3

```bash
# Parallel observability and response refinements
Task T034: Map processor summary to API response
Task T036: Add structured run logging
```

---

## Implementation Strategy

### MVP First (US1)

1. Complete Phase 1 and Phase 2.
2. Complete Phase 3 (US1) to deliver schedule-on-create capability.
3. Validate independent test criteria for US1 before moving on.

### Incremental Delivery

1. Deliver US1 (scheduled creation).
2. Deliver US2 (due processing endpoint).
3. Deliver US3 (detailed run outcomes and failure visibility).
4. Finish with Phase 6 polish and full verification.

### Parallel Team Strategy

1. One engineer handles Core models/interfaces/repository (T005-T012).
2. One engineer handles API create-flow changes (T013-T018).
3. After foundational completion, split processing orchestration (T028-T031) and observability/reporting (T032-T037).

---

## Notes

- All tasks use concrete repo file paths.
- `[US1]`, `[US2]`, `[US3]` tags map directly to prioritized stories in spec.md.
- No mandatory test-first tasks are included because the feature spec did not explicitly request TDD.
- Keep commits small and aligned to task boundaries.

# Tasks: LinkedIn Posting Support

**Input**: Design documents from `/specs/001-linkedin-posting/`  
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/, quickstart.md

**Tests**: Test tasks are intentionally omitted because the feature specification did not explicitly request TDD or test-first implementation.

**Organization**: Tasks are grouped by user story so each story can be implemented and validated independently.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Prepare baseline files and configuration scaffolding for LinkedIn integration.

- [X] T001 Create LinkedIn configuration type in `src/BarretApi.Core/Configuration/LinkedInOptions.cs`
- [X] T002 Add LinkedIn parameter declarations for local orchestration in `src/BarretApi.AppHost/Program.cs`
- [X] T003 [P] Add LinkedIn environment variable wiring from AppHost to API in `src/BarretApi.AppHost/Program.cs`
- [X] T004 [P] Create LinkedIn infrastructure folder and initial file structure in `src/BarretApi.Infrastructure/LinkedIn/LinkedInClient.cs`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core integration points that must exist before user stories can be completed.

**⚠️ CRITICAL**: No user story work should begin until this phase is complete.

- [X] T005 Create LinkedIn API transport models in `src/BarretApi.Infrastructure/LinkedIn/Models/LinkedInModels.cs`
- [X] T006 Implement `ISocialPlatformClient` skeleton for LinkedIn in `src/BarretApi.Infrastructure/LinkedIn/LinkedInClient.cs`
- [X] T007 Bind LinkedIn options and register startup validation in `src/BarretApi.Api/Program.cs`
- [X] T008 Register LinkedIn HttpClient configuration in `src/BarretApi.Api/Program.cs`
- [X] T009 Register LinkedIn client as `ISocialPlatformClient` in `src/BarretApi.Api/Program.cs`
- [X] T010 Update allowed platform validation to include `linkedin` in `src/BarretApi.Api/Features/SocialPost/CreateSocialPostValidator.cs`
- [X] T011 Update social post endpoint examples to include LinkedIn platform usage in `src/BarretApi.Api/Features/SocialPost/CreateSocialPostEndpoint.cs`

**Checkpoint**: Foundation complete — user stories can now be implemented.

---

## Phase 3: User Story 1 - Publish to LinkedIn with Existing Post Flow (Priority: P1) 🎯 MVP

**Goal**: Allow callers to publish to LinkedIn through the existing `/api/social-posts` request flow.

**Independent Test**: Submit a valid request with `platforms: ["linkedin"]` and verify a LinkedIn success result is returned in the existing response shape.

### Implementation for User Story 1

- [X] T012 [US1] Implement LinkedIn `GetConfigurationAsync` platform limits in `src/BarretApi.Infrastructure/LinkedIn/LinkedInClient.cs`
- [X] T013 [US1] Implement LinkedIn text post request/response mapping in `src/BarretApi.Infrastructure/LinkedIn/LinkedInClient.cs`
- [X] T014 [US1] Implement LinkedIn image upload handling in `src/BarretApi.Infrastructure/LinkedIn/LinkedInClient.cs`
- [X] T015 [P] [US1] Add LinkedIn-specific media and post payload DTOs in `src/BarretApi.Infrastructure/LinkedIn/Models/LinkedInModels.cs`
- [X] T016 [US1] Map LinkedIn publish success to `PlatformPostResult` fields in `src/BarretApi.Infrastructure/LinkedIn/LinkedInClient.cs`
- [X] T017 [US1] Align API contract examples with LinkedIn request/response behavior in `specs/001-linkedin-posting/contracts/social-post-linkedin.openapi.yaml`

**Checkpoint**: User Story 1 is independently functional and provides MVP value.

---

## Phase 4: User Story 2 - Handle LinkedIn Failures Without Blocking Other Platforms (Priority: P2)

**Goal**: Ensure LinkedIn-specific failures are isolated and reported while other platforms continue posting.

**Independent Test**: Trigger a LinkedIn failure with at least one other valid platform and verify a 207 response with independent per-platform results.

### Implementation for User Story 2

- [X] T018 [US2] Implement LinkedIn error payload parsing with safe fallback behavior in `src/BarretApi.Infrastructure/LinkedIn/LinkedInClient.cs`
- [X] T019 [US2] Implement LinkedIn HTTP status to shared error-code mapping in `src/BarretApi.Infrastructure/LinkedIn/LinkedInClient.cs`
- [X] T020 [US2] Add exception handling paths returning normalized failures in `src/BarretApi.Infrastructure/LinkedIn/LinkedInClient.cs`
- [X] T021 [US2] Add structured LinkedIn failure logging without credential leakage in `src/BarretApi.Infrastructure/LinkedIn/LinkedInClient.cs`
- [X] T022 [US2] Verify and preserve partial-success aggregation semantics in `src/BarretApi.Core/Services/SocialPostService.cs`
- [X] T023 [US2] Update endpoint status-code documentation for LinkedIn partial/all-failure cases in `src/BarretApi.Api/Features/SocialPost/CreateSocialPostEndpoint.cs`

**Checkpoint**: User Stories 1 and 2 both work with resilient multi-platform behavior.

---

## Phase 5: User Story 3 - Configure LinkedIn Credentials per Environment (Priority: P3)

**Goal**: Support secure LinkedIn configuration across local and production environments using existing AppHost patterns.

**Independent Test**: Start with valid LinkedIn settings and verify posting works; remove a required setting and verify LinkedIn fails with clear non-secret error output.

### Implementation for User Story 3

- [X] T024 [US3] Add LinkedIn required fields and validation helpers in `src/BarretApi.Core/Configuration/LinkedInOptions.cs`
- [X] T025 [US3] Enforce LinkedIn options validation rules at API startup in `src/BarretApi.Api/Program.cs`
- [X] T026 [US3] Add LinkedIn AppHost parameters for token, author URN, and API base URL in `src/BarretApi.AppHost/Program.cs`
- [X] T027 [US3] Wire LinkedIn AppHost parameters into API environment variables in `src/BarretApi.AppHost/Program.cs`
- [X] T028 [US3] Document secure local/production LinkedIn configuration steps in `specs/001-linkedin-posting/quickstart.md`
- [X] T029 [US3] Add LinkedIn configuration and rollout notes in `README.md`

**Checkpoint**: All user stories are operational with environment-specific configuration support.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final consistency checks, docs hardening, and implementation readiness tasks.

- [X] T030 [P] Reconcile final endpoint contract examples with implementation details in `specs/001-linkedin-posting/contracts/social-post-linkedin.openapi.yaml`
- [X] T031 [P] Add LinkedIn auth/rate-limit troubleshooting guidance in `specs/001-linkedin-posting/research.md`
- [X] T032 Add final verification command checklist for build/run/manual API checks in `specs/001-linkedin-posting/quickstart.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies.
- **Phase 2 (Foundational)**: Depends on Phase 1 and blocks all user stories.
- **Phase 3 (US1)**: Depends on Phase 2 and is the MVP.
- **Phase 4 (US2)**: Depends on Phase 2 and builds on US1 platform execution.
- **Phase 5 (US3)**: Depends on Phase 2 and can proceed in parallel with US2 after core LinkedIn client exists.
- **Phase 6 (Polish)**: Depends on completion of selected user stories.

### User Story Dependencies

- **US1 (P1)**: No dependency on other stories after foundational completion.
- **US2 (P2)**: Depends on US1 LinkedIn publish path to validate failure isolation.
- **US3 (P3)**: Depends on foundational configuration wiring; can be developed alongside US2 once US1 baseline client exists.

### Parallel Opportunities

- Setup tasks `T003` and `T004` can run in parallel after `T001`/`T002` begin.
- Foundational registration tasks `T008` and `T009` can run in parallel with validator/docs updates `T010` and `T011` after `T006` starts.
- In US1, `T015` can run in parallel with `T012`.
- In US2, `T021` can run in parallel with `T019` once error mapping direction is set.
- In US3, `T028` and `T029` can run in parallel after `T026`/`T027` are defined.

---

## Parallel Example: User Story 1

- Run in parallel: `T012` and `T015`
- Then sequence: `T013` → `T014` → `T016` → `T017`

## Parallel Example: User Story 2

- Run in parallel: `T019` and `T021`
- Then sequence: `T018` → `T020` → `T022` → `T023`

## Parallel Example: User Story 3

- Run in parallel: `T028` and `T029`
- Then sequence: `T024` → `T025` → `T026` → `T027`

---

## Implementation Strategy

### MVP First (US1 only)

1. Complete Phase 1 and Phase 2.
2. Complete Phase 3 (US1).
3. Validate US1 independently against spec acceptance criteria.
4. Demo/deploy MVP before adding failure-hardening and operator configuration refinements.

### Incremental Delivery

1. Deliver US1 (LinkedIn publish path on existing endpoint).
2. Deliver US2 (failure isolation and error normalization).
3. Deliver US3 (environment configuration hardening and operator docs).
4. Complete Phase 6 polish tasks and final verification.

### Parallel Team Strategy

1. Team completes Setup + Foundational together.
2. After Phase 2:
   - Developer A: US1 (`T012`-`T017`)
   - Developer B: US2 (`T018`-`T023`)
   - Developer C: US3 (`T024`-`T029`)
3. Merge all stories, then finish polish (`T030`-`T032`).

# Tasks: RSS Blog Post Promotion

**Input**: Design documents from `/specs/001-rss-blog-posting/`  
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/, quickstart.md

**Tests**: Test tasks are intentionally omitted because the feature specification did not explicitly request TDD or test-first task generation.

**Organization**: Tasks are grouped by user story so each story can be implemented and validated independently.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Add required dependencies and baseline project wiring.

- [X] T001 Add Azure and RSS package versions to `Directory.Packages.props`
- [X] T002 Add package references for Azure Table and RSS parsing in `src/BarretApi.Infrastructure/BarretApi.Infrastructure.csproj`
- [X] T003 [P] Add configuration placeholders for blog promotion settings in `src/BarretApi.AppHost/Program.cs`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core abstractions and infrastructure required before any user story work.

**⚠️ CRITICAL**: No user story work should begin until this phase is complete.

- [X] T004 Create blog promotion options class in `src/BarretApi.Core/Configuration/BlogPromotionOptions.cs`
- [X] T005 Create promotion tracking entity and status enums in `src/BarretApi.Core/Models/BlogPostPromotionRecord.cs`
- [X] T006 [P] Create run summary and failure models in `src/BarretApi.Core/Models/PromotionRunSummary.cs`
- [X] T007 Create RSS feed reader abstraction in `src/BarretApi.Core/Interfaces/IBlogFeedReader.cs`
- [X] T008 [P] Create promotion tracking repository abstraction in `src/BarretApi.Core/Interfaces/IBlogPostPromotionRepository.cs`
- [X] T009 Create orchestration service abstraction in `src/BarretApi.Core/Interfaces/IBlogPromotionOrchestrator.cs`
- [X] T010 Implement Azure Table promotion repository in `src/BarretApi.Infrastructure/Services/AzureTableBlogPostPromotionRepository.cs`
- [X] T011 [P] Implement RSS feed reader adapter in `src/BarretApi.Infrastructure/Services/RssBlogFeedReader.cs`
- [X] T012 Register options and foundational services in `src/BarretApi.Api/Program.cs`

**Checkpoint**: Foundation ready — user story implementation can proceed.

---

## Phase 3: User Story 1 - Post Newly Published Blog Entries (Priority: P1) 🎯 MVP

**Goal**: Trigger one endpoint call that posts qualifying new RSS entries and records initial posting state.

**Independent Test**: Invoke endpoint with a feed containing in-window and out-of-window entries and verify only unposted in-window entries are posted and tracked.

### Implementation for User Story 1

- [X] T013 [P] [US1] Create endpoint response DTOs for run summaries in `src/BarretApi.Api/Features/SocialPost/TriggerRssPromotionResponse.cs`
- [X] T014 [US1] Implement new-entry eligibility filtering by configurable day window in `src/BarretApi.Core/Services/BlogPromotionOrchestrator.cs`
- [X] T015 [US1] Implement initial-post publishing and tracking updates in `src/BarretApi.Core/Services/BlogPromotionOrchestrator.cs`
- [X] T016 [US1] Add trigger endpoint for `POST /api/social-posts/rss-promotion` in `src/BarretApi.Api/Features/SocialPost/TriggerRssPromotionEndpoint.cs`
- [X] T017 [US1] Map orchestrator initial-pass results to API response contract in `src/BarretApi.Api/Features/SocialPost/TriggerRssPromotionEndpoint.cs`

**Checkpoint**: User Story 1 is independently functional and delivers MVP value.

---

## Phase 4: User Story 2 - Send Delayed Reminder Posts (Priority: P2)

**Goal**: Post one optional delayed reminder for eligible previously posted entries with the required leader text.

**Independent Test**: Seed a tracked entry with successful initial post timestamp, enable reminders, wait past delay, invoke endpoint, and verify exactly one reminder post is created.

### Implementation for User Story 2

- [X] T018 [US2] Implement reminder eligibility rules using delay-hours and toggle options in `src/BarretApi.Core/Services/BlogPromotionOrchestrator.cs`
- [X] T019 [US2] Implement reminder message composition with `Did you miss it earlier?` prefix in `src/BarretApi.Core/Services/BlogPromotionOrchestrator.cs`
- [X] T020 [US2] Persist reminder-attempt and reminder-success state transitions in `src/BarretApi.Infrastructure/Services/AzureTableBlogPostPromotionRepository.cs`
- [X] T021 [US2] Extend endpoint response mapping for reminder attempt/success/failure counts in `src/BarretApi.Api/Features/SocialPost/TriggerRssPromotionResponse.cs`

**Checkpoint**: User Stories 1 and 2 both work, including delayed reminder behavior without duplicates.

---

## Phase 5: User Story 3 - Enforce Posting Order in One Endpoint Run (Priority: P3)

**Goal**: Guarantee all new-post processing completes before any reminder processing begins in each run.

**Independent Test**: Seed both new-entry and reminder-eligible data, invoke endpoint, and verify all initial-post actions complete before first reminder action.

### Implementation for User Story 3

- [X] T022 [US3] Refactor orchestrator into explicit two-pass execution flow in `src/BarretApi.Core/Services/BlogPromotionOrchestrator.cs`
- [X] T023 [US3] Add structured logs that mark start/end of initial and reminder phases in `src/BarretApi.Core/Services/BlogPromotionOrchestrator.cs`
- [X] T024 [US3] Ensure endpoint surfaces ordered run metadata and failures in `src/BarretApi.Api/Features/SocialPost/TriggerRssPromotionEndpoint.cs`

**Checkpoint**: All user stories are functional with deterministic execution order.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Documentation and operational readiness across all stories.

- [X] T025 [P] Document endpoint usage and HTTPS requirement in `README.md`
- [X] T026 [P] Update feature quickstart with final request/response examples in `specs/001-rss-blog-posting/quickstart.md`
- [X] T027 Harden option validation defaults and guard clauses in `src/BarretApi.Core/Configuration/BlogPromotionOptions.cs`
- [X] T028 Add troubleshooting notes for feed and table connectivity failures in `specs/001-rss-blog-posting/research.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies.
- **Phase 2 (Foundational)**: Depends on Phase 1; blocks all user stories.
- **Phase 3 (US1)**: Depends on Phase 2; MVP starts here.
- **Phase 4 (US2)**: Depends on Phase 2 and integrates with US1 promotion records.
- **Phase 5 (US3)**: Depends on Phase 2 and requires US1 + US2 behaviors to verify ordering across both passes.
- **Phase 6 (Polish)**: Depends on completion of the user stories selected for delivery.

### User Story Dependencies

- **US1 (P1)**: No dependency on other user stories after foundational completion.
- **US2 (P2)**: Uses shared tracking model and builds on initial-post lifecycle from US1.
- **US3 (P3)**: Verifies and enforces ordering across initial and reminder flows; depends on US1 and US2 behaviors.

### Parallel Opportunities

- Setup tasks `T003` can run alongside `T001`/`T002`.
- Foundational tasks `T006`, `T008`, and `T011` can run in parallel after `T004`/`T005` begin.
- In US1, `T013` can run in parallel with `T014`.
- Polish tasks `T025` and `T026` can run in parallel.

---

## Parallel Example: User Story 1

- Run in parallel: `T013` and `T014`
- Then sequence: `T015` → `T016` → `T017`

## Parallel Example: User Story 2

- Run in parallel: `T018` and `T020`
- Then sequence: `T019` → `T021`

## Parallel Example: User Story 3

- Run in parallel: `T023` and endpoint output formatting draft for `T024`
- Then finalize ordering contract in `T022` and complete `T024`

---

## Implementation Strategy

### MVP First (US1 only)

1. Complete Phases 1 and 2.
2. Complete Phase 3 (US1).
3. Validate independent US1 behavior against the spec scenarios.
4. Demo/deploy MVP before adding reminder functionality.

### Incremental Delivery

1. Deliver US1 for immediate value.
2. Add US2 for delayed reminder automation.
3. Add US3 for deterministic two-pass ordering guarantees.
4. Finish with Phase 6 polish and documentation updates.

### Parallel Team Strategy

1. Team completes Setup + Foundational together.
2. After Phase 2:
   - Developer A: US1 (`T013`-`T017`)
   - Developer B: US2 (`T018`-`T021`)
3. Developer C (or A/B after merge): US3 (`T022`-`T024`) and polish (`T025`-`T028`).

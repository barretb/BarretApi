# Tasks: RSS Reminder Post Header Update

**Input**: Design documents from `/specs/005-rss-reminder-header/`
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, quickstart.md ✅, contracts/ (empty — no external interface changes)

**Tests**: Included — explicitly required by spec (SC-003) and plan (Constitution Principle III).

**Organization**: Single user story (US1), so tasks flow linearly with test-first approach.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[US1]**: User Story 1 — Updated Reminder Post Header Text

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: No setup tasks required. All project infrastructure, dependencies, and test projects already exist.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: No foundational tasks required. The target source file (`BlogPromotionOrchestrator.cs`) and test project (`BarretApi.Core.UnitTests`) already exist with all necessary dependencies.

---

## Phase 3: User Story 1 — Updated Reminder Post Header Text (Priority: P1) 🎯 MVP

**Goal**: Change reminder post text from `"Did you miss it earlier? {Title}\n{URL}"` to `"In case you missed it earlier...\n\n{Title}\n{URL}"` while leaving initial posts unchanged.

**Independent Test**: Trigger a reminder post for an already-promoted blog entry and verify the resulting text starts with `"In case you missed it earlier...\n\n"` followed by the entry title and canonical URL.

### Tests for User Story 1 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [x] T001 [P] [US1] Write unit test `GeneratesCorrectReminderText_GivenEligibleRecord` verifying reminder post text starts with "In case you missed it earlier...\n\n" followed by title and URL in tests/BarretApi.Core.UnitTests/Services/BlogPromotionOrchestrator_BuildReminderPostText_Tests.cs
- [x] T002 [P] [US1] Write unit test `UsesThreeAsciiPeriods_GivenReminderPost` verifying the ellipsis in the header is three literal ASCII period characters (U+002E) not Unicode ellipsis in tests/BarretApi.Core.UnitTests/Services/BlogPromotionOrchestrator_BuildReminderPostText_Tests.cs
- [x] T003 [P] [US1] Write unit test `DoesNotAlterInitialPostText_GivenNewEntry` verifying initial (non-reminder) post format remains `"{Title}\n{URL}"` with no header prepended in tests/BarretApi.Core.UnitTests/Services/BlogPromotionOrchestrator_BuildReminderPostText_Tests.cs

**Checkpoint**: All three tests written and confirmed FAILING (red phase of TDD)

### Implementation for User Story 1

- [x] T004 [US1] Modify `BuildReminderPostText` method in src/BarretApi.Core/Services/BlogPromotionOrchestrator.cs to return `$"In case you missed it earlier...\n\n{record.Title}\n{record.CanonicalUrl}"`
- [x] T005 [US1] Verify all three unit tests now PASS (green phase of TDD)

**Checkpoint**: User Story 1 is fully functional — reminder posts use the new header, initial posts are unchanged, all tests pass

---

## Phase 4: Polish & Cross-Cutting Concerns

**Purpose**: Documentation updates to keep specs and README in sync with the implementation

- [x] T006 [P] Update reminder post text description in README.md to reflect the new "In case you missed it earlier..." header format
- [x] T007 [P] Update FR-009 in specs/001-rss-blog-posting/spec.md to reference the new reminder header text instead of "Did you miss it earlier?"
- [x] T008 Run quickstart.md validation steps from specs/005-rss-reminder-header/quickstart.md to confirm end-to-end behavior

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: N/A — no tasks
- **Foundational (Phase 2)**: N/A — no tasks
- **User Story 1 (Phase 3)**: Can start immediately — no blocking prerequisites
- **Polish (Phase 4)**: Depends on Phase 3 completion (T004 must be done before documenting)

### Within User Story 1

```text
T001 ─┐
T002 ─┤ (all parallel — same file, independent test methods)
T003 ─┘
       │
       ▼
     T004 (implementation — must wait for tests to be written and confirmed failing)
       │
       ▼
     T005 (verification — run tests, confirm green)
```

### Within Polish

```text
T006 ─┐ (parallel — different files)
T007 ─┘
       │
       ▼
     T008 (quickstart validation — after docs are updated)
```

---

## Parallel Example: User Story 1

```bash
# Step 1: Write all tests in parallel (T001, T002, T003)
# All go into the same new test file — can be authored together
# Confirm tests FAIL (no implementation yet)

# Step 2: Implement (T004)
# Single-line change in BlogPromotionOrchestrator.cs

# Step 3: Verify (T005)
# Run tests — all should pass
```

---

## Implementation Strategy

- **MVP Scope**: User Story 1 (Phase 3) delivers all user-facing value
- **Incremental Delivery**: Phase 3 → Phase 4; no intermediate releases needed
- **Risk**: Minimal — single static method change with no side effects
- **Estimated Tasks**: 8 total (3 test, 2 implementation/verification, 3 polish)

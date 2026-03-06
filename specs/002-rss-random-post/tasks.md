# Tasks: RSS Random Post

**Input**: Design documents from `/specs/002-rss-random-post/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Included — plan.md explicitly lists test files and constitution requires Test-Driven Quality Assurance.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3, US4)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Verify the existing project builds cleanly before making changes

- [X] T001 Verify solution builds with zero warnings by running `dotnet build` from repository root

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Interface extension and shared models that ALL user stories depend on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T002 Add `ReadEntriesAsync(string feedUrl, CancellationToken)` overload to `IBlogFeedReader` interface in src/BarretApi.Core/Interfaces/IBlogFeedReader.cs
- [X] T003 Implement `ReadEntriesAsync(string feedUrl, CancellationToken)` overload in `RssBlogFeedReader` by extracting shared parsing logic into a private method that both overloads call in src/BarretApi.Infrastructure/Services/RssBlogFeedReader.cs
- [X] T004 [P] Create `RssRandomPostRequest` model with all properties (`FeedUrl`, `Platforms`, `ExcludeTags`, `MaxAgeDays`) in src/BarretApi.Api/Features/SocialPost/RssRandomPostRequest.cs
- [X] T005 [P] Create `RssRandomPostResponse` model with `SelectedTitle`, `SelectedUrl`, `Results`, `PostedAt` in src/BarretApi.Api/Features/SocialPost/RssRandomPostResponse.cs
- [X] T006 [P] Create `RssRandomPostResult` internal model with `SelectedEntry` and `PlatformResults` in src/BarretApi.Core/Models/RssRandomPostResult.cs

**Checkpoint**: Foundation ready — IBlogFeedReader has URL overload, all models exist, user story implementation can begin

---

## Phase 3: User Story 1 — Post a Random RSS Entry to All Platforms (Priority: P1) 🎯 MVP

**Goal**: Provide an RSS feed URL, pick one random entry, and post it to all configured social platforms

**Independent Test**: Invoke endpoint with only `feedUrl`, verify one entry is randomly selected and posted to all platforms, response includes `selectedTitle`, `selectedUrl`, and per-platform success results

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T007 [P] [US1] Create unit tests for `RssRandomPostValidator` feedUrl validation rules (required, valid absolute URL, http/https scheme only) in tests/BarretApi.Api.UnitTests/Features/SocialPost/RssRandomPostValidator_Tests.cs
- [X] T008 [P] [US1] Create unit tests for `RssRandomPostService.SelectAndPostAsync` core logic (random selection from entries, empty feed returns error, builds SocialPost with title+URL text and tags as hashtags, includes hero image, delegates to SocialPostService) in tests/BarretApi.Core.UnitTests/Services/RssRandomPostService_SelectAndPostAsync_Tests.cs

### Implementation for User Story 1

- [X] T009 [P] [US1] Implement `RssRandomPostValidator` with feedUrl rules (not empty, valid absolute URI, http/https scheme only) in src/BarretApi.Api/Features/SocialPost/RssRandomPostValidator.cs
- [X] T010 [US1] Implement `RssRandomPostService.SelectAndPostAsync` — fetch feed via `IBlogFeedReader.ReadEntriesAsync(feedUrl)`, validate entries exist, select random entry via `Random.Shared.Next()`, build `SocialPost` (text = `"{Title}\n{CanonicalUrl}"`, hashtags = entry tags, images = hero image), delegate to `SocialPostService.PostAsync`, return `RssRandomPostResult` in src/BarretApi.Core/Services/RssRandomPostService.cs
- [X] T011 [US1] Create `RssRandomPostEndpoint` — configure route `POST /api/social-posts/rss-random` with API key auth, call `RssRandomPostService.SelectAndPostAsync`, map `RssRandomPostResult` to `RssRandomPostResponse`, return 200 (all success) / 207 (partial) / 422 (no eligible entries) / 502 (all failed or feed error), and register `RssRandomPostService` in DI in src/BarretApi.Api/Features/SocialPost/RssRandomPostEndpoint.cs

**Checkpoint**: User Story 1 fully functional — `POST /api/social-posts/rss-random` with `feedUrl` only works end-to-end, posting to all platforms

---

## Phase 4: User Story 2 — Filter by Target Platforms (Priority: P2)

**Goal**: Optionally specify which social platforms to post to instead of posting to all

**Independent Test**: Invoke endpoint with `feedUrl` and `platforms: ["bluesky"]`, verify post published only to Bluesky with no posts to Mastodon or LinkedIn

### Tests for User Story 2

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T012 [P] [US2] Add unit tests for platform validation rules (each value must be `bluesky`/`mastodon`/`linkedin`, case-insensitive, reject unknown platforms) in tests/BarretApi.Api.UnitTests/Features/SocialPost/RssRandomPostValidator_Tests.cs
- [X] T013 [P] [US2] Add unit tests for platform targeting (posts only to specified platforms when provided, posts to all when omitted) in tests/BarretApi.Core.UnitTests/Services/RssRandomPostService_SelectAndPostAsync_Tests.cs

### Implementation for User Story 2

- [X] T014 [US2] Add platform validation rules to `RssRandomPostValidator` — each value in `Platforms` must be a supported platform name in src/BarretApi.Api/Features/SocialPost/RssRandomPostValidator.cs
- [X] T015 [US2] Add platform targeting logic to `RssRandomPostService.SelectAndPostAsync` — pass specified platforms to `SocialPostService.PostAsync` instead of all configured platforms when `Platforms` is provided in src/BarretApi.Core/Services/RssRandomPostService.cs

**Checkpoint**: User Stories 1 AND 2 both work independently — platform targeting restricts output without breaking default all-platforms behavior

---

## Phase 5: User Story 3 — Exclude Posts by Tag (Priority: P2)

**Goal**: Optionally exclude feed entries that carry specified tags from the random selection pool

**Independent Test**: Invoke endpoint with `feedUrl` and `excludeTags: ["personal"]`, verify the selected entry does not carry the "personal" tag; verify case-insensitive matching

### Tests for User Story 3

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T016 [P] [US3] Add unit tests for tag exclusion filtering (entries with excluded tags removed, case-insensitive matching, entries without tags not excluded, all entries filtered returns error, hashtags exclude excluded tags) in tests/BarretApi.Core.UnitTests/Services/RssRandomPostService_SelectAndPostAsync_Tests.cs

### Implementation for User Story 3

- [X] T017 [US3] Add tag exclusion filtering to `RssRandomPostService.SelectAndPostAsync` — after fetching entries, remove entries where any tag matches `ExcludeTags` (case-insensitive), then validate at least one entry remains in src/BarretApi.Core/Services/RssRandomPostService.cs
- [X] T018 [US3] Update hashtag construction in `RssRandomPostService` to exclude tags present in the `ExcludeTags` list when building the `SocialPost.Hashtags` collection in src/BarretApi.Core/Services/RssRandomPostService.cs

**Checkpoint**: User Stories 1, 2, AND 3 all work independently — tag exclusion filters entries and hashtags without breaking previous behavior

---

## Phase 6: User Story 4 — Filter by Recency (Priority: P3)

**Goal**: Optionally limit the selection pool to entries published within the last X days

**Independent Test**: Invoke endpoint with `feedUrl` and `maxAgeDays: 7`, verify the selected entry was published within the last 7 days; verify entries without a publication date are excluded when `maxAgeDays` is provided

### Tests for User Story 4

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T019 [P] [US4] Add unit tests for maxAgeDays validation rules (must be > 0 when provided, null is valid) in tests/BarretApi.Api.UnitTests/Features/SocialPost/RssRandomPostValidator_Tests.cs
- [X] T020 [P] [US4] Add unit tests for recency filtering (entries older than maxAgeDays excluded, entries without publication date excluded when maxAgeDays set, all entries filtered returns error, no filtering when maxAgeDays omitted) in tests/BarretApi.Core.UnitTests/Services/RssRandomPostService_SelectAndPostAsync_Tests.cs

### Implementation for User Story 4

- [X] T021 [US4] Add maxAgeDays validation rule to `RssRandomPostValidator` — must be greater than 0 when provided in src/BarretApi.Api/Features/SocialPost/RssRandomPostValidator.cs
- [X] T022 [US4] Add recency filtering to `RssRandomPostService.SelectAndPostAsync` — after tag exclusion, remove entries whose `PublishedAtUtc` is older than `maxAgeDays` from now and entries with no publication date, then validate at least one entry remains in src/BarretApi.Core/Services/RssRandomPostService.cs

**Checkpoint**: All four user stories work independently — full filtering pipeline (tags + recency) composes correctly

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Observability, validation, and documentation improvements across all stories

- [X] T023 [P] Add structured logging to `RssRandomPostService` (feed fetch, filter counts, selected entry, posting outcome) in src/BarretApi.Core/Services/RssRandomPostService.cs
- [X] T024 [P] Add structured logging to `RssRandomPostEndpoint` (request received, response status code) in src/BarretApi.Api/Features/SocialPost/RssRandomPostEndpoint.cs
- [X] T025 Run quickstart.md smoke test validation — execute all curl examples from specs/002-rss-random-post/quickstart.md against running API
- [X] T026 Verify solution builds with zero warnings by running `dotnet build` and `dotnet test` from repository root

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion — BLOCKS all user stories
- **User Stories (Phase 3–6)**: All depend on Foundational phase completion
  - Stories MUST be sequential (US1 → US2 → US3 → US4) because they modify the same files (`RssRandomPostService.cs`, `RssRandomPostValidator.cs`, test files)
- **Polish (Phase 7)**: Depends on all user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2) — No dependencies on other stories
- **User Story 2 (P2)**: Depends on US1 completion — extends validator and service in the same files
- **User Story 3 (P2)**: Depends on US2 completion — extends service in the same file
- **User Story 4 (P3)**: Depends on US3 completion — extends validator and service in the same files

### Within Each User Story

- Tests MUST be written and FAIL before implementation
- Validator before service (validator defines input contract)
- Service before endpoint (endpoint delegates to service)
- Core implementation before integration
- Story complete before moving to next priority

### Parallel Opportunities

- **Phase 2**: T004, T005, T006 (models) can run in parallel with each other and alongside T002–T003 (interface)
- **Phase 3 (US1)**: T007 + T008 (tests) can run in parallel; T009 (validator) parallel with T010 (service) after tests
- **Phase 4 (US2)**: T012 + T013 (tests) in parallel; T014 (validator) independent of T015 (service) structurally but both modify different files so can be parallel
- **Phase 5 (US3)**: T016 (tests) first; T017 and T018 modify the same file so must be sequential
- **Phase 6 (US4)**: T019 + T020 (tests) in parallel; T021 (validator) parallel with T022 (service)
- **Phase 7**: T023 + T024 (logging) in parallel

---

## Parallel Example: User Story 1

```bash
# Step 1: Launch tests for User Story 1 together (should FAIL):
Task: T007 "RssRandomPostValidator feedUrl tests" in tests/BarretApi.Api.UnitTests/Features/SocialPost/RssRandomPostValidator_Tests.cs
Task: T008 "RssRandomPostService core logic tests" in tests/BarretApi.Core.UnitTests/Services/RssRandomPostService_SelectAndPostAsync_Tests.cs

# Step 2: Launch validator and service in parallel (different files):
Task: T009 "RssRandomPostValidator implementation" in src/BarretApi.Api/Features/SocialPost/RssRandomPostValidator.cs
Task: T010 "RssRandomPostService implementation" in src/BarretApi.Core/Services/RssRandomPostService.cs

# Step 3: Endpoint (depends on validator + service):
Task: T011 "RssRandomPostEndpoint" in src/BarretApi.Api/Features/SocialPost/RssRandomPostEndpoint.cs

# Step 4: Verify tests now PASS
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (verify build)
2. Complete Phase 2: Foundational (interface overload + models)
3. Complete Phase 3: User Story 1 (core random post to all platforms)
4. **STOP and VALIDATE**: Run tests, test via quickstart curl commands
5. Deploy/demo if ready — core value is delivered

### Incremental Delivery

1. Complete Setup + Foundational → Foundation ready
2. Add User Story 1 → Test independently → Deploy/Demo (**MVP!**)
3. Add User Story 2 → Test platform targeting → Deploy/Demo
4. Add User Story 3 → Test tag exclusion → Deploy/Demo
5. Add User Story 4 → Test recency filtering → Deploy/Demo
6. Each story adds filtering capability without breaking previous behavior

### Sequential Execution (Recommended)

Since all user stories modify the same core files (`RssRandomPostService.cs`, `RssRandomPostValidator.cs`), sequential execution in priority order is recommended:

1. Team completes Setup + Foundational together
2. US1 (P1): Core random post → validates end-to-end flow
3. US2 (P2): Platform targeting → extends validator + service
4. US3 (P2): Tag exclusion → extends service filtering pipeline
5. US4 (P3): Recency filtering → extends service filtering pipeline
6. Polish: Logging, smoke tests, final build verification

---

## Notes

- [P] tasks = different files, no dependencies on in-progress tasks
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable after the previous story
- Verify tests fail before implementing (TDD within each story)
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- All 4 user stories modify `RssRandomPostService.cs` — sequential execution prevents merge conflicts
- No new NuGet packages or projects needed — all work extends existing structure

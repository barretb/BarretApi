# Tasks: Standard Atom/RSS Feed Support

**Input**: Design documents from `/specs/006-atom-feed-support/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/rss-random-post.md, quickstart.md

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- **Source**: `src/` at repository root
- **Tests**: `tests/` at repository root
- Paths match plan.md project structure

---

## Phase 1: Foundational (Blocking Prerequisites)

**Purpose**: Request contract changes, tag eligibility changes, and header prepend logic that enable ALL user stories.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T001 [P] Add optional `Header` property (`string?`) to `RssRandomPostRequest` in `src/BarretApi.Api/Features/SocialPost/RssRandomPostRequest.cs`
- [X] T002 [P] Add optional `Header` property (`string?`) to `RssRandomPostQuery` in `src/BarretApi.Core/Models/RssRandomPostQuery.cs`
- [X] T003 Map `Header` from request to query in endpoint handler in `src/BarretApi.Api/Features/SocialPost/RssRandomPostEndpoint.cs`
- [X] T004 [P] Remove tag-required filter (`Tags.Count == 0` exclusion at lines 27–35) from `SelectAndPostAsync` in `src/BarretApi.Core/Services/RssRandomPostService.cs`
- [X] T005 [P] Remove tag-required filter (`Tags.Count == 0` skip at lines 71–73) from `RunAsync` in `src/BarretApi.Core/Services/BlogPromotionOrchestrator.cs`
- [X] T006 Implement header prepend logic in `BuildSocialPost` — when `query.Header` is non-empty, insert header + newline between leader text and entry title in `src/BarretApi.Core/Services/RssRandomPostService.cs`

**Checkpoint**: Foundation ready — Header field flows through request pipeline, tagless entries are eligible, header is prepended to post text.

---

## Phase 2: User Story 1 — Post from a Standard Atom/RSS Feed (Priority: P1) 🎯 MVP

**Goal**: Parse standard Atom/RSS feeds without custom namespace extensions and post eligible entries to social platforms.

**Independent Test**: Invoke endpoint with a publicly available standard Atom or RSS feed (no custom extensions) and verify a post is created from an eligible entry.

### Implementation for User Story 1

- [X] T007 [US1] Add summary fallback to `SyndicationItem.Content` (cast to `TextSyndicationContent`) when `Summary` is null in `ParseFeed` in `src/BarretApi.Infrastructure/Services/RssBlogFeedReader.cs`
- [X] T008 [US1] Add private HTML-stripping method using AngleSharp (`BrowsingContext` → `IHtmlParser.ParseDocument` → remove script/style/noscript/svg/head → `Body.TextContent`) and apply it to summary extraction in `src/BarretApi.Infrastructure/Services/RssBlogFeedReader.cs`
- [X] T009 [US1] Extend `ReadHeroImageUrl` with three-tier fallback: (1) existing custom `<hero>` element, (2) `SyndicationItem.Links` where `RelationshipType == "enclosure"` and `MediaType` starts with `image/`, (3) `ElementExtensions` for `<media:thumbnail>` and `<media:content>` in `http://search.yahoo.com/mrss/` namespace — return first valid absolute HTTP/HTTPS URL in `src/BarretApi.Infrastructure/Services/RssBlogFeedReader.cs`

### Tests for User Story 1

- [X] T010 [P] [US1] Create unit tests for standard feed parsing: summary from Atom `<content>` fallback, HTML stripping to plain text, and hero image from enclosure link in `tests/BarretApi.Infrastructure.UnitTests/Services/RssBlogFeedReader_ParseFeed_Tests.cs`
- [X] T011 [P] [US1] Add unit tests for tagless entry eligibility and header prepend to existing test class in `tests/BarretApi.Core.UnitTests/Services/RssRandomPostService_SelectAndPostAsync_Tests.cs`

**Checkpoint**: Standard Atom/RSS feeds can be parsed and posted. Entries without tags are eligible. Header prepend works.

---

## Phase 3: User Story 2 — Continue Supporting Custom Blog Feed (Priority: P2)

**Goal**: Ensure existing custom blog feed with namespace extensions (hero image, tags) continues to work identically — zero regressions.

**Independent Test**: Invoke endpoint with existing custom blog RSS feed and verify hero images and tags are extracted and used in filtering and posting.

### Implementation for User Story 2

No production code changes required — backward compatibility is maintained by the precedence logic in the fallback implementation (custom extensions checked first in T009 and T015).

### Tests for User Story 2

- [X] T012 [US2] Add unit tests verifying custom tag extensions take precedence over standard categories when both are present in `tests/BarretApi.Infrastructure.UnitTests/Services/RssBlogFeedReader_ParseFeed_Tests.cs`
- [X] T013 [US2] Add unit tests verifying custom hero image takes precedence over enclosure and media fallbacks when both are present in `tests/BarretApi.Infrastructure.UnitTests/Services/RssBlogFeedReader_ParseFeed_Tests.cs`
- [X] T014 [US2] Add unit tests for mixed-extension feed — entries with custom extensions and entries without in the same feed document in `tests/BarretApi.Infrastructure.UnitTests/Services/RssBlogFeedReader_ParseFeed_Tests.cs`

**Checkpoint**: All existing custom feed behavior verified via tests. No regressions.

---

## Phase 4: User Story 3 — Use Standard Categories as Tags Fallback (Priority: P3)

**Goal**: Read standard RSS/Atom `<category>` elements as tags when custom tag extensions are absent, enabling tag-based filtering for any feed.

**Independent Test**: Provide a standard feed with `<category>` elements, invoke endpoint with tag exclusions matching one category, and verify that entry is excluded.

### Implementation for User Story 3

- [X] T015 [US3] Extend `ReadTags` to fall back to `SyndicationItem.Categories` (using `category.Name`) when custom tag extensions are absent in `src/BarretApi.Infrastructure/Services/RssBlogFeedReader.cs`

### Tests for User Story 3

- [X] T016 [P] [US3] Add unit tests for category-to-tag fallback and custom-tags-precedence in `tests/BarretApi.Infrastructure.UnitTests/Services/RssBlogFeedReader_ParseFeed_Tests.cs`
- [X] T017 [P] [US3] Add unit tests for tag exclusion filtering with category-sourced tags in `tests/BarretApi.Core.UnitTests/Services/RssRandomPostService_SelectAndPostAsync_Tests.cs`

**Checkpoint**: Standard categories work as tags. Tag exclusion filters work with both custom and standard tag sources.

---

## Phase 5: Polish & Cross-Cutting Concerns

**Purpose**: Code quality, documentation, and validation

- [X] T018 Run `dotnet format` across the solution from repository root
- [X] T019 Run `dotnet build` and resolve any warnings or errors
- [X] T020 [P] Update README.md with standard feed support and header field documentation
- [ ] T021 Run quickstart.md validation scenarios against running API

---

## Dependencies & Execution Order

### Phase Dependencies

- **Foundational (Phase 1)**: No dependencies — start immediately. BLOCKS all user stories.
- **User Story 1 (Phase 2)**: Depends on Phase 1 completion.
- **User Story 2 (Phase 3)**: Depends on Phase 2 (tests verify US1 fallback code).
- **User Story 3 (Phase 4)**: Depends on Phase 1 completion. Modifies same file as US1, so sequence after US1 recommended.
- **Polish (Phase 5)**: Depends on all implementation phases.

### User Story Dependencies

- **US1 (P1)**: Depends only on Foundational phase. Core MVP.
- **US2 (P2)**: Depends on US1 (tests verify the fallback code written in US1).
- **US3 (P3)**: Depends on Foundational phase. Modifies same file as US1 (`RssBlogFeedReader.cs`), so sequence after US1 recommended.

### Within Each Phase

- T001 + T002 + T004 + T005: Parallel (four different files, no dependencies)
- T003: After T001 + T002 (needs Header on both request and query)
- T006: After T002 (needs Header on query); same file as T004 so sequence after T004
- T007 → T008 → T009: Sequential (same file, each builds on prior)
- T010 + T011: Parallel (different test projects)
- T012 → T013 → T014: Sequential (same test file, adding methods)
- T015: After T009 (same file as US1 feed reader changes)
- T016 + T017: Parallel (different test projects)

---

## Parallel Example: Phase 1 (Foundational)

```text
Parallel batch 1:  T001, T002, T004, T005  (4 different files)
Sequential:        T003                     (depends on T001 + T002)
Sequential:        T006                     (depends on T002, same file as T004)
```

## Parallel Example: User Story 1

```text
Sequential:        T007 → T008 → T009      (same file, builds incrementally)
Parallel batch:    T010, T011               (different test projects)
```

## Parallel Example: User Story 3

```text
Sequential:        T015                     (same file as US1 feed reader)
Parallel batch:    T016, T017               (different test projects)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Foundational (Header field + tagless eligibility)
2. Complete Phase 2: User Story 1 (standard feed parsing + fallbacks)
3. **STOP and VALIDATE**: Test with a real standard Atom/RSS feed
4. Deploy/demo if ready — basic standard feed support is functional

### Incremental Delivery

1. Phase 1 → Foundation ready
2. Phase 2 → US1 complete → **MVP: standard feeds work** → Validate
3. Phase 3 → US2 complete → Backward compatibility verified
4. Phase 4 → US3 complete → Category-based tag filtering works
5. Phase 5 → Polish → Documentation updated, all quality gates pass

### Suggested MVP Scope

User Story 1 only (Phases 1 + 2, tasks T001–T011). This delivers the core value: any standard Atom/RSS feed can be used with the endpoint, with header prepend and tagless eligibility.

---

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks
- [Story] label maps task to specific user story for traceability
- All feed reader changes are in one file (`RssBlogFeedReader.cs`) — tasks within a story are sequential
- Custom extension code is NOT removed — fallback logic checks custom first, then standard
- Header is a foundational change, not story-specific (applies to all feed types)
- `BlogPromotionOrchestrator` needs tag filter removal (T005) but does NOT need header changes (scheduled job, no request)
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently

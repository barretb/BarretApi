# Tasks: Social Media Post API

**Input**: Design documents from `/specs/001-social-post-api/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/api-endpoints.md, quickstart.md

**Tests**: Not included — not explicitly requested in the feature specification. Test projects are scaffolded in Phase 1 for future use. Run `/speckit.tasks` with a TDD flag to generate test tasks.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3, US4)
- Exact file paths included in every description

---

## Phase 1: Setup (Project Initialization)

**Purpose**: Create solution structure, configure build settings and package management

- [X] T001 Create solution file BarretApi.slnx with all 8 projects and project references per plan.md Project Structure
- [X] T002 [P] Configure Directory.Build.props with net10.0 target, TreatWarningsAsErrors, nullable, implicit usings, NoWarn 1591, tab indentation
- [X] T003 [P] Configure Directory.Packages.props with all NuGet package versions (FastEndpoints, FluentValidation, Aspire.AppHost, Aspire.ServiceDefaults, Microsoft.Extensions.Http.Resilience, xUnit, NSubstitute, Shouldly)

**Project .csproj files created in T001**:

- src/BarretApi.AppHost/BarretApi.AppHost.csproj
- src/BarretApi.ServiceDefaults/BarretApi.ServiceDefaults.csproj
- src/BarretApi.Api/BarretApi.Api.csproj (references Core + Infrastructure)
- src/BarretApi.Core/BarretApi.Core.csproj (no project references)
- src/BarretApi.Infrastructure/BarretApi.Infrastructure.csproj (references Core)
- tests/BarretApi.Core.UnitTests/BarretApi.Core.UnitTests.csproj (references Core)
- tests/BarretApi.Api.UnitTests/BarretApi.Api.UnitTests.csproj (references Api)
- tests/BarretApi.Integration.Tests/BarretApi.Integration.Tests.csproj (references Api)

**Checkpoint**: `dotnet build` succeeds with zero errors and zero warnings

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can begin

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T004 Implement Aspire ServiceDefaults extensions (OpenTelemetry, health checks, resilience handler, service discovery) in src/BarretApi.ServiceDefaults/Extensions.cs
- [X] T005 [P] Configure Aspire AppHost with secret parameters (Bluesky__Handle, Bluesky__AppPassword, Mastodon__InstanceUrl, Mastodon__AccessToken, Auth__ApiKey) and API project reference in src/BarretApi.AppHost/Program.cs
- [X] T006 [P] Create configuration options classes (BlueskyOptions.cs, MastodonOptions.cs, ApiKeyOptions.cs) in src/BarretApi.Core/Configuration/
- [X] T007 [P] Create ISocialPlatformClient interface with PostAsync, UploadImageAsync, GetConfigurationAsync methods in src/BarretApi.Core/Interfaces/ISocialPlatformClient.cs
- [X] T008 [P] Create domain value objects (SocialPost.cs, ImageData.cs, PlatformConfiguration.cs, PlatformPostResult.cs, UploadedImage.cs) in src/BarretApi.Core/Models/
- [X] T009 [P] Create API request/response DTOs (CreateSocialPostRequest.cs, CreateSocialPostResponse.cs with PlatformResult) in src/BarretApi.Api/Features/SocialPost/
- [X] T010 [P] Implement ApiKeyAuthHandler (custom AuthenticationHandler reading Auth__ApiKey from IOptions) in src/BarretApi.Api/Auth/ApiKeyAuthHandler.cs
- [X] T011 Configure API Program.cs with FastEndpoints, ApiKey authentication scheme, IOptions<T> bindings, and AddServiceDefaults() in src/BarretApi.Api/Program.cs

**Checkpoint**: `dotnet build` succeeds; API starts and returns 401 for requests without X-Api-Key header

---

## Phase 3: User Story 1 — Post a Text Message to Both Platforms (Priority: P1) 🎯 MVP

**Goal**: Submit a text post through a single JSON request and have it published to both Bluesky and Mastodon simultaneously, with per-platform success/failure reporting

**Independent Test**: Send a text-only POST to `/api/social-posts` with a valid API key and verify the post appears on both platforms with correct content; verify partial failure when one platform has bad credentials

### Implementation for User Story 1

- [X] T012 [P] [US1] Create Bluesky internal models (BlueskySession.cs, BlueskyCreateRecordRequest.cs, BlueskyCreateRecordResponse.cs) in src/BarretApi.Infrastructure/Bluesky/Models/
- [X] T013 [P] [US1] Create Mastodon internal models (MastodonInstanceConfig.cs, MastodonStatus.cs, MastodonError.cs) in src/BarretApi.Infrastructure/Mastodon/Models/
- [X] T014 [P] [US1] Implement BlueskyClient with session management (createSession, refreshSession) and text posting (createRecord) in src/BarretApi.Infrastructure/Bluesky/BlueskyClient.cs
- [X] T015 [P] [US1] Implement MastodonClient with instance config caching (GET /api/v2/instance) and text posting (POST /api/v1/statuses) in src/BarretApi.Infrastructure/Mastodon/MastodonClient.cs
- [X] T016 [US1] Implement SocialPostService orchestration (parallel platform dispatch, per-platform result aggregation, error isolation) in src/BarretApi.Core/Services/SocialPostService.cs
- [X] T017 [US1] Implement CreateSocialPostEndpoint with POST /api/social-posts route, JSON binding, and 200/207/502 response logic in src/BarretApi.Api/Features/SocialPost/CreateSocialPostEndpoint.cs
- [X] T018 [US1] Implement CreateSocialPostValidator with text-required-when-no-images and platform-name validation in src/BarretApi.Api/Features/SocialPost/CreateSocialPostValidator.cs
- [X] T019 [US1] Register HttpClientFactory named clients (Bluesky, Mastodon), ISocialPlatformClient implementations, and SocialPostService in src/BarretApi.Api/Program.cs

**Checkpoint**: Text-only posts publish to both platforms; partial failure returns 207; all-fail returns 502; missing API key returns 401; empty text returns 400

---

## Phase 4: User Story 2 — Automatic Post Length Shortening (Priority: P2)

**Goal**: Automatically shorten post text to fit within each platform's character limit (Bluesky: 300 grapheme clusters; Mastodon: dynamic from instance config), truncating at word boundaries with a Unicode ellipsis

**Independent Test**: Submit posts of various lengths (below, at, and above each platform's limit) and verify Bluesky receives shortened text while Mastodon receives full text when only Bluesky's limit is exceeded

### Implementation for User Story 2

- [X] T020 [P] [US2] Create ITextShorteningService interface in src/BarretApi.Core/Interfaces/ITextShorteningService.cs
- [X] T021 [US2] Implement TextShorteningService with word-boundary truncation, Unicode ellipsis (U+2026), and grapheme-cluster-aware counting via StringInfo in src/BarretApi.Core/Services/TextShorteningService.cs
- [X] T022 [US2] Integrate text shortening into SocialPostService per-platform flow (shorten text to platform's MaxCharacters before posting) in src/BarretApi.Core/Services/SocialPostService.cs

**Checkpoint**: Posts exceeding Bluesky's 300-grapheme limit are shortened with "…" at word boundary; Mastodon receives full text when under its limit; both platforms receive shortened text when both limits exceeded

---

## Phase 5: User Story 3 — Attach Images with Required Alt Text (Priority: P3)

**Goal**: Attach up to 4 images (via URL or file upload) to posts with mandatory alt text, uploading to each platform's media API before creating the post

**Independent Test**: Submit a post with one image (both via URL and multipart upload) with alt text and verify the image appears on both platforms; verify validation rejects missing/blank alt text and unsupported formats

### Implementation for User Story 3

- [X] T023 [P] [US3] Create IImageDownloadService interface in src/BarretApi.Core/Interfaces/IImageDownloadService.cs
- [X] T024 [P] [US3] Implement ImageDownloadService with HttpClient URL fetch, Content-Type validation, and configurable size limit in src/BarretApi.Infrastructure/Services/ImageDownloadService.cs
- [X] T025 [P] [US3] Add image upload support to BlueskyClient (POST /xrpc/com.atproto.repo.uploadBlob, app.bsky.embed.images embed) in src/BarretApi.Infrastructure/Bluesky/BlueskyClient.cs
- [X] T026 [P] [US3] Add image upload support to MastodonClient (POST /api/v2/media with description, media_ids[] in status) in src/BarretApi.Infrastructure/Mastodon/MastodonClient.cs
- [X] T027 [US3] Extend SocialPostService with image processing flow (download URL images, upload to platforms, attach to posts) in src/BarretApi.Core/Services/SocialPostService.cs
- [X] T028 [US3] Add image validation rules to CreateSocialPostValidator (alt text required/non-whitespace, max 4 images, JPEG/PNG/GIF/WebP only, max 1 MB, max 1500 char alt text) in src/BarretApi.Api/Features/SocialPost/CreateSocialPostValidator.cs
- [X] T029 [US3] Implement CreateSocialPostUploadEndpoint with POST /api/social-posts/upload route, AllowFileUploads, multipart binding in src/BarretApi.Api/Features/SocialPost/CreateSocialPostUploadEndpoint.cs
- [X] T030 [US3] Register IImageDownloadService and update DI for multipart endpoint in src/BarretApi.Api/Program.cs

**Checkpoint**: Images upload and attach on both platforms via JSON (URL) and multipart (file) endpoints; alt text appears correctly; missing alt text returns 400; unsupported format returns 400; max 4 images enforced

---

## Phase 6: User Story 4 — Include Hashtags in Posts (Priority: P4)

**Goal**: Support hashtags provided inline in text and as a separate list, with de-duplication, auto-prefixing, and platform-specific rich text (Bluesky facets with UTF-8 byte offsets)

**Independent Test**: Submit a post with inline hashtags and a separate hashtag list and verify clickable hashtags appear on both platforms; verify de-duplication and auto-prefixing work correctly

### Implementation for User Story 4

- [X] T031 [P] [US4] Create IHashtagService interface in src/BarretApi.Core/Interfaces/IHashtagService.cs
- [X] T032 [US4] Implement HashtagService (parse inline hashtags via regex, merge with separate list, case-insensitive de-duplication, auto-prefix with #) in src/BarretApi.Core/Services/HashtagService.cs
- [X] T033 [P] [US4] Implement BlueskyFacetBuilder (detect hashtags in text, compute UTF-8 byte offsets, generate app.bsky.richtext.facet#tag facets) in src/BarretApi.Infrastructure/Bluesky/BlueskyFacetBuilder.cs
- [X] T034 [US4] Integrate HashtagService into SocialPostService orchestration (process hashtags before shortening and posting) in src/BarretApi.Core/Services/SocialPostService.cs
- [X] T035 [US4] Update TextShorteningService to preserve trailing hashtags during shortening per FR-007 (remove hashtags from end first, then truncate body text) in src/BarretApi.Core/Services/TextShorteningService.cs
- [X] T036 [US4] Integrate BlueskyFacetBuilder into BlueskyClient to include facets array in createRecord requests in src/BarretApi.Infrastructure/Bluesky/BlueskyClient.cs

**Checkpoint**: Hashtags from both inline text and separate list appear as clickable tags on both platforms; duplicates removed; `#` auto-prefixed; Bluesky facets use correct UTF-8 byte offsets; shortening removes trailing hashtags before body text

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Quality improvements that affect multiple user stories

- [X] T037 [P] Add structured logging with message templates and correlation IDs across SocialPostService, BlueskyClient, and MastodonClient
- [X] T038 [P] Verify error code consistency (AUTH_FAILED, RATE_LIMITED, VALIDATION_FAILED, IMAGE_UPLOAD_FAILED, IMAGE_DOWNLOAD_FAILED, PLATFORM_ERROR) across all error paths in platform clients
- [X] T039 Run dotnet format across entire solution and resolve any code style violations
- [X] T040 Validate quickstart.md end-to-end flow (build, configure secrets, run, post via curl) against actual API behavior

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 completion — **BLOCKS all user stories**
- **User Story 1 (Phase 3)**: Depends on Phase 2 completion — **This is the MVP**
- **User Story 2 (Phase 4)**: Depends on Phase 3 (needs working platform posting to integrate shortening)
- **User Story 3 (Phase 5)**: Depends on Phase 3 (needs working platform clients to add image upload)
- **User Story 4 (Phase 6)**: Depends on Phase 4 (needs text shortening to integrate hashtag preservation)
- **Polish (Phase 7)**: Depends on all user stories being complete

### User Story Dependencies

```text
Phase 1 (Setup)
  └─→ Phase 2 (Foundational)
        └─→ Phase 3: US1 - Text Posting (P1) 🎯 MVP
              ├─→ Phase 4: US2 - Auto Shortening (P2)
              │     └─→ Phase 6: US4 - Hashtags (P4)
              └─→ Phase 5: US3 - Images (P3)
                        └─→ Phase 7: Polish
```

- **US2 depends on US1**: Shortening integrates into the existing posting flow
- **US3 depends on US1**: Image upload extends the existing platform clients
- **US4 depends on US2**: Hashtag preservation during shortening requires the shortening service
- **US2 and US3 are independent**: Can run in parallel after US1

### Within Each User Story

1. Models / interfaces before service implementations
2. Infrastructure (platform clients) before Core orchestration integration
3. Core services before API endpoints
4. Validators alongside or after endpoints
5. DI registration after all components exist

### Parallel Opportunities Within Phases

**Phase 2** — T004 through T010 are all [P] (different files, no interdependencies); T011 depends on all of them:

```text
T004 ─┐
T005 ─┤
T006 ─┤
T007 ─┼─→ T011
T008 ─┤
T009 ─┤
T010 ─┘
```

**Phase 3** — Models in parallel, then clients in parallel, then sequential:

```text
T012 ─┬─→ T014 ─┐
T013 ─┴─→ T015 ─┼─→ T016 ─→ T017 ─→ T018 ─→ T019
                 │
```

**Phase 5** — Interface + download service + platform uploads in parallel:

```text
T023 ─┬─→ T024 ─┐
T025 ─┤         ├─→ T027 ─→ T028 ─→ T029 ─→ T030
T026 ─┘         │
```

**Phase 6** — Interface + facet builder in parallel, then sequential:

```text
T031 ─→ T032 ─┐
T033 ─────────┼─→ T034 ─→ T035 ─→ T036
```

---

## Parallel Execution Examples

### User Story 1 — Models Phase

```text
# Launch both platform model tasks together:
T012: Create Bluesky internal models in src/BarretApi.Infrastructure/Bluesky/Models/
T013: Create Mastodon internal models in src/BarretApi.Infrastructure/Mastodon/Models/

# Then launch both client implementations together:
T014: Implement BlueskyClient in src/BarretApi.Infrastructure/Bluesky/BlueskyClient.cs
T015: Implement MastodonClient in src/BarretApi.Infrastructure/Mastodon/MastodonClient.cs
```

### User Story 3 — Image Infrastructure

```text
# Launch interface, download service, and both platform upload tasks together:
T023: Create IImageDownloadService in src/BarretApi.Core/Interfaces/
T024: Implement ImageDownloadService in src/BarretApi.Infrastructure/Services/
T025: Add image upload to BlueskyClient in src/BarretApi.Infrastructure/Bluesky/
T026: Add image upload to MastodonClient in src/BarretApi.Infrastructure/Mastodon/
```

### Cross-Story Parallelism (after US1 complete)

```text
# US2 and US3 can run in parallel since they modify different files:
Developer A: T020 → T021 → T022 (Text shortening)
Developer B: T023+T024+T025+T026 → T027 → T028 → T029 → T030 (Images)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (`dotnet build` passes)
2. Complete Phase 2: Foundational (API starts, auth works)
3. Complete Phase 3: User Story 1 (text posts publish to both platforms)
4. **STOP and VALIDATE**: Test text posting via curl per quickstart.md
5. Deploy/demo if ready — this is a working API

### Incremental Delivery

1. **Setup + Foundational** → Build compiles, API starts with auth
2. **Add US1** → Text posting works → **Deploy (MVP!)**
3. **Add US2** → Long posts auto-shorten → Deploy
4. **Add US3** → Images attach with alt text → Deploy
5. **Add US4** → Hashtags are clickable on both platforms → Deploy
6. **Polish** → Logging, error consistency, format check → Final deploy

Each increment adds value without breaking previous functionality.

### Parallel Team Strategy

With two developers after Phase 2:

1. Both complete Setup + Foundational together
2. Both implement US1 together (it's the MVP)
3. After US1:
   - **Developer A**: US2 (shortening) → US4 (hashtags — depends on US2)
   - **Developer B**: US3 (images) → Polish
4. Merge and validate all stories together

---

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks in same group
- [US*] label maps task to a specific user story for traceability
- Each user story is verifiable at its checkpoint before proceeding
- Commit after each task or logical group for incremental progress
- Bluesky character counting uses grapheme clusters (`StringInfo.LengthInTextElements`), NOT `string.Length`
- Bluesky facets use UTF-8 byte offsets, NOT character indices
- Mastodon character limit is dynamic — must query `GET /api/v2/instance`
- All configuration flows through Aspire AppHost — no appsettings.json in other projects
- File size validation uses 1 MB (Bluesky's limit) as the lower bound, not Mastodon's 16 MB

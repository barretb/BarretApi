# Tasks: GitHub REST API Integration

**Input**: Design documents from `/specs/008-github-rest-api/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Included — plan.md defines test files and Constitution Principle III mandates Test-Driven Quality Assurance.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Aspire AppHost configuration for GitHub OAuth parameters and environment variables

- [X] T001 Configure Aspire AppHost with GitHub parameters and environment variables in src/BarretApi.AppHost/Program.cs

> **T001 Details**: Add Aspire parameters for `github-client-id`, `github-client-secret`, `github-api-base-url`, `github-oauth-base-url`, `github-token-storage-account-endpoint`, `github-token-storage-table-name`, `github-repo-storage-account-endpoint`, `github-repo-storage-table-name`. Map to environment variables `GitHub__ClientId`, `GitHub__ClientSecret`, `GitHub__ApiBaseUrl`, `GitHub__OAuthBaseUrl`, `GitHub__TokenStorage__AccountEndpoint`, `GitHub__TokenStorage__TableName`, `GitHub__RepoStorage__AccountEndpoint`, `GitHub__RepoStorage__TableName`. Follow the existing LinkedIn parameter pattern. Add `github-client-id` and `github-client-secret` as required secret parameters. Set default values for optional parameters (ApiBaseUrl: `https://api.github.com`, OAuthBaseUrl: `https://github.com`, token table: `githubtokens`, repo table: `githubrepositories`). Wire ConnectionString environment variables from the Azurite storage resource.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core models, interfaces, configuration, and shared infrastructure that ALL user stories depend on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T002 [P] Create GitHubOptions, GitHubTokenStorageOptions, and GitHubRepoStorageOptions configuration classes in src/BarretApi.Core/Configuration/GitHubOptions.cs

> **T002 Details**: Strongly-typed configuration bound via `IOptions<GitHubOptions>`. Properties: `ClientId` (string, required), `ClientSecret` (string, required), `ApiBaseUrl` (string, default `https://api.github.com`), `OAuthBaseUrl` (string, default `https://github.com`), `TokenStorage` (GitHubTokenStorageOptions), `RepoStorage` (GitHubRepoStorageOptions). Nested classes have `ConnectionString`, `AccountEndpoint`, `TableName` (with defaults `githubtokens` / `githubrepositories`). Follow the existing LinkedIn options pattern. Use file-scoped namespace, sealed classes.

- [X] T003 [P] Create GitHubTokenRecord model in src/BarretApi.Core/Models/GitHubTokenRecord.cs

> **T003 Details**: Properties: `AccessToken` (string), `Username` (string), `Scope` (string), `UpdatedAtUtc` (DateTimeOffset). This is a POCO for cross-layer transfer. Does NOT inherit from `ITableEntity` — the Infrastructure layer handles table mapping. Sealed class, file-scoped namespace.

- [X] T004 [P] Create GitHubRepositoryRecord model in src/BarretApi.Core/Models/GitHubRepositoryRecord.cs

> **T004 Details**: Properties: `Name` (string), `FullName` (string), `Description` (string?), `IsPrivate` (bool), `DefaultBranch` (string), `HtmlUrl` (string), `UpdatedAtUtc` (DateTimeOffset), `SyncedAtUtc` (DateTimeOffset). Sealed class, file-scoped namespace.

- [X] T005 [P] Create GitHubIssueResult model in src/BarretApi.Core/Models/GitHubIssueResult.cs

> **T005 Details**: Properties: `Number` (int), `Title` (string), `HtmlUrl` (string), `State` (string). Transient result object — not persisted. Sealed class, file-scoped namespace.

- [X] T006 [P] Create IGitHubTokenStore interface in src/BarretApi.Core/Interfaces/IGitHubTokenStore.cs

> **T006 Details**: Methods: `Task<GitHubTokenRecord?> GetTokenAsync(CancellationToken)`, `Task SaveTokenAsync(GitHubTokenRecord, CancellationToken)`. Follow the existing `ILinkedInTokenStore` pattern. File-scoped namespace.

- [X] T007 [P] Create IGitHubRepositoryStore interface in src/BarretApi.Core/Interfaces/IGitHubRepositoryStore.cs

> **T007 Details**: Methods: `Task<IReadOnlyList<GitHubRepositoryRecord>> GetAllAsync(CancellationToken)`, `Task<GitHubRepositoryRecord?> GetByNameAsync(string name, CancellationToken)`, `Task ReplaceAllAsync(string username, IReadOnlyList<GitHubRepositoryRecord> repositories, CancellationToken)`. File-scoped namespace.

- [X] T008 [P] Create IGitHubClient interface in src/BarretApi.Core/Interfaces/IGitHubClient.cs

> **T008 Details**: Methods: `Task<GitHubTokenRecord> ExchangeCodeForTokenAsync(string code, CancellationToken)`, `Task<IReadOnlyList<GitHubRepositoryRecord>> GetRepositoriesAsync(CancellationToken)`, `Task<GitHubIssueResult> CreateIssueAsync(string owner, string repo, string title, string? body, IReadOnlyList<string>? labels, CancellationToken)`. File-scoped namespace.

- [X] T009 [P] Create GitHubApiModels for internal JSON deserialization in src/BarretApi.Infrastructure/GitHub/Models/GitHubApiModels.cs

> **T009 Details**: Internal record types for deserializing GitHub API JSON responses. Include: `GitHubTokenResponse` (access_token, token_type, scope), `GitHubUserResponse` (login), `GitHubRepoResponse` (name, full_name, description, private, default_branch, html_url, updated_at), `GitHubIssueResponse` (number, title, html_url, state). Use `System.Text.Json.Serialization.JsonPropertyName` attributes for snake_case mapping. Internal visibility — not exposed outside Infrastructure.

- [X] T010 Create GitHubTokenProvider in src/BarretApi.Infrastructure/GitHub/GitHubTokenProvider.cs

> **T010 Details**: Reads token from `IGitHubTokenStore` on first use, caches in memory. Method: `Task<string> GetAccessTokenAsync(CancellationToken)` — returns the access token string or throws if no token stored. No refresh logic needed (GitHub tokens don't expire). No `SemaphoreSlim` needed. If downstream gets 401, caller clears provider cache via `void ClearCache()`. Follow research.md decision §5. Sealed class, primary constructor with `IGitHubTokenStore` dependency.

- [X] T011 [P] Create GitHubOptions_Validate_Tests in tests/BarretApi.Core.UnitTests/Configuration/GitHubOptions_Validate_Tests.cs

> **T011 Details**: Test that GitHubOptions defaults are correct (ApiBaseUrl, OAuthBaseUrl, table names). Test that nested options have expected defaults. Use xUnit `[Fact]` and Shouldly assertions. Follow naming convention: `ReturnsCorrectDefaults_GivenNewInstance`, etc.

- [X] T012 [P] Create GitHubTokenProvider_Tests in tests/BarretApi.Infrastructure.UnitTests/GitHub/GitHubTokenProvider_Tests.cs

> **T012 Details**: Test token caching behavior: first call reads from store, second call returns cached. Test `GetAccessTokenAsync` throws when no token stored. Test `ClearCache` causes next call to re-read from store. Use NSubstitute for `IGitHubTokenStore`. Use xUnit + Shouldly. Follow naming: `ReturnsToken_GivenTokenInStore`, `ThrowsException_GivenNoTokenInStore`, `ReReadsFromStore_GivenCacheCleared`.

**Checkpoint**: Foundation ready — all models, interfaces, configuration, and shared infrastructure in place. User story implementation can now begin.

---

## Phase 3: User Story 1 — Authenticate with GitHub (Priority: P1) 🎯 MVP

**Goal**: Complete OAuth flow — initiate authorization, handle callback, exchange code for token, store token, verify connection status.

**Independent Test**: Initiate OAuth flow at `/api/github/auth`, complete callback, then verify connection via `/api/github/profile` returns the authenticated username.

**Requirements covered**: FR-001, FR-002, FR-003, FR-004, FR-005, FR-006, FR-018

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T013 [P] [US1] Create AzureTableGitHubTokenStore_Tests in tests/BarretApi.Infrastructure.UnitTests/GitHub/AzureTableGitHubTokenStore_Tests.cs

> **T013 Details**: Test `SaveTokenAsync` writes to Azure Table with correct partition key (`"github-tokens"`) and row key (`"current"`). Test `GetTokenAsync` returns stored token. Test `GetTokenAsync` returns null when no token exists. Use NSubstitute to mock `TableClient`. Use xUnit + Shouldly. Follow naming: `SavesToken_GivenValidTokenRecord`, `ReturnsToken_GivenTokenExists`, `ReturnsNull_GivenNoTokenExists`.

- [X] T014 [P] [US1] Create GitHubAuthCallbackEndpoint_Tests in tests/BarretApi.Api.UnitTests/Features/GitHub/GitHubAuthCallbackEndpoint_Tests.cs

> **T014 Details**: Test successful callback exchanges code and stores token. Test callback with error parameter returns 400. Test callback with mismatched state returns 400. Use NSubstitute for `IGitHubClient` and `IGitHubTokenStore`. Use xUnit + Shouldly. Follow naming: `ReturnsConnectedStatus_GivenValidAuthCode`, `ReturnsBadRequest_GivenOAuthError`, `ReturnsBadRequest_GivenInvalidState`.

### Implementation for User Story 1

- [X] T015 [US1] Implement AzureTableGitHubTokenStore in src/BarretApi.Infrastructure/GitHub/AzureTableGitHubTokenStore.cs

> **T015 Details**: Implements `IGitHubTokenStore`. Uses `TableClient` from Azure.Data.Tables. Partition key: `"github-tokens"`, row key: `"current"`. `SaveTokenAsync`: upserts a `TableEntity` with AccessToken, Username, Scope, UpdatedAtUtc properties. `GetTokenAsync`: gets entity, maps to `GitHubTokenRecord`, returns null if not found. Configure `TableClient` from `IOptions<GitHubOptions>` using TokenStorage settings. Create table if not exists. Sealed class, primary constructor. Follow existing `AzureTableLinkedInTokenStore` pattern.

- [X] T016 [US1] Implement GitHubClient with OAuth methods in src/BarretApi.Infrastructure/GitHub/GitHubClient.cs

> **T016 Details**: Implements `IGitHubClient` (partial — OAuth methods only in this task). Uses typed `HttpClient` injected via constructor. `ExchangeCodeForTokenAsync`: POST to `{OAuthBaseUrl}/login/oauth/access_token` with client_id, client_secret, code; set `Accept: application/json` header; deserialize `GitHubTokenResponse`; then GET `/user` with Bearer token to get username; return `GitHubTokenRecord` with access token, username, scope, current UTC timestamp. Add `X-GitHub-Api-Version: 2022-11-28` and `User-Agent` headers. Sealed class, primary constructor with `HttpClient`, `IOptions<GitHubOptions>`, `GitHubTokenProvider`, `ILogger<GitHubClient>`. Leave `GetRepositoriesAsync` and `CreateIssueAsync` as `throw new NotImplementedException()` stubs for now.

- [X] T017 [P] [US1] Create GitHubAuthEndpoint in src/BarretApi.Api/Features/GitHub/GitHubAuthEndpoint.cs

> **T017 Details**: GET `/api/github/auth`. Anonymous access (no API key). Generates a random state GUID, stores it (in-memory or cache for validation on callback). Builds GitHub authorization URL: `{OAuthBaseUrl}/login/oauth/authorize?client_id={clientId}&redirect_uri={callbackUrl}&scope=repo&state={state}`. Returns 200 with `{ "authUrl": "..." }` for API clients. Follow FastEndpoints REPR pattern with `EndpointWithoutRequest`. Use `IOptions<GitHubOptions>` for client ID and OAuth base URL.

- [X] T018 [US1] Create GitHubAuthCallbackEndpoint with request, response, and validator in src/BarretApi.Api/Features/GitHub/

> **T018 Details**: Create 4 files: `GitHubAuthCallbackRequest.cs` (properties: Code, State, Error, ErrorDescription — bound from query string), `GitHubAuthCallbackResponse.cs` (properties: Username, Status, Scope), `GitHubAuthCallbackValidator.cs` (validates State is required; Code required when Error is absent), `GitHubAuthCallbackEndpoint.cs` (GET `/api/github/auth/callback`, anonymous access). Endpoint logic: validate state matches stored state (FR-004); if error param present, return 400 with error message; exchange code via `IGitHubClient.ExchangeCodeForTokenAsync`; save token via `IGitHubTokenStore.SaveTokenAsync`; return 200 with username, "connected" status, scope. Handle exchange failures with 400/502 responses.

- [X] T019 [P] [US1] Create GitHubProfileEndpoint and response in src/BarretApi.Api/Features/GitHub/

> **T019 Details**: Create 2 files: `GitHubProfileResponse.cs` (properties: Username, Connected (bool), Scope, ConnectedAtUtc), `GitHubProfileEndpoint.cs` (GET `/api/github/profile`, anonymous access). Reads stored token from `IGitHubTokenStore.GetTokenAsync`. If token exists, returns 200 with username, connected=true, scope, and UpdatedAtUtc. If no token, returns 200 with connected=false and null fields. Follow FastEndpoints REPR pattern.

- [X] T020 [US1] Register GitHub authentication services and configure HttpClient in src/BarretApi.Api/Program.cs

> **T020 Details**: Bind `GitHubOptions` from configuration section `"GitHub"`. Register `IGitHubTokenStore` → `AzureTableGitHubTokenStore` (scoped). Register `GitHubTokenProvider` (singleton). Register `IGitHubClient` → `GitHubClient` as typed HttpClient via `AddHttpClient<IGitHubClient, GitHubClient>` with base address from `GitHubOptions.ApiBaseUrl` and resilience handler. Follow existing LinkedIn service registration pattern. Ensure HttpClient has default headers: `User-Agent: BarretApi`, `Accept: application/vnd.github+json`, `X-GitHub-Api-Version: 2022-11-28`.

**Checkpoint**: User Story 1 complete — OAuth flow functional. Can authenticate with GitHub and verify connection status.

---

## Phase 4: User Story 2 — Retrieve and Store Repository List (Priority: P2)

**Goal**: Sync repositories from GitHub, store locally, list and query stored repos.

**Independent Test**: After authenticating (US1), trigger POST `/api/github/repos/sync`, then GET `/api/github/repos` returns the synced repository list. GET `/api/github/repos/{name}` returns a single repo.

**Requirements covered**: FR-007, FR-008, FR-009, FR-010, FR-011, FR-012, FR-018, FR-019, FR-020

### Tests for User Story 2

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T021 [P] [US2] Create AzureTableGitHubRepositoryStore_Tests in tests/BarretApi.Infrastructure.UnitTests/GitHub/AzureTableGitHubRepositoryStore_Tests.cs

> **T021 Details**: Test `ReplaceAllAsync` deletes existing rows and inserts new ones. Test `GetAllAsync` returns all stored repositories. Test `GetByNameAsync` returns matching repo or null. Use NSubstitute to mock `TableClient`. Use xUnit + Shouldly. Follow naming: `ReplacesAllRepositories_GivenNewList`, `ReturnsAllRepositories_GivenRepositoriesExist`, `ReturnsNull_GivenRepositoryNotFound`.

- [X] T022 [P] [US2] Create GitHubClient_GetRepositoriesAsync_Tests in tests/BarretApi.Infrastructure.UnitTests/GitHub/GitHubClient_GetRepositoriesAsync_Tests.cs

> **T022 Details**: Test single page of repos returns correct list. Test pagination (multiple pages via Link header) aggregates all repos. Test 401 response throws appropriate exception. Test rate limit (429) returns error with reset time. Use NSubstitute with mock `HttpMessageHandler` or `MockHttpMessageHandler`. Use xUnit + Shouldly. Follow naming: `ReturnsRepositories_GivenSinglePage`, `ReturnsAllRepositories_GivenMultiplePages`, `ThrowsUnauthorized_GivenRevokedToken`.

- [X] T023 [P] [US2] Create GitHubRepoSyncEndpoint_Tests in tests/BarretApi.Api.UnitTests/Features/GitHub/GitHubRepoSyncEndpoint_Tests.cs

> **T023 Details**: Test successful sync returns count and timestamp. Test sync without token returns 401. Test sync with GitHub API error returns 502. Use NSubstitute for `IGitHubClient`, `IGitHubRepositoryStore`, `IGitHubTokenStore`. Use xUnit + Shouldly. Follow naming: `ReturnsSyncCount_GivenSuccessfulSync`, `ReturnsUnauthorized_GivenNoToken`, `ReturnsBadGateway_GivenGitHubApiError`.

### Implementation for User Story 2

- [X] T024 [US2] Implement AzureTableGitHubRepositoryStore in src/BarretApi.Infrastructure/GitHub/AzureTableGitHubRepositoryStore.cs

> **T024 Details**: Implements `IGitHubRepositoryStore`. Uses `TableClient` from Azure.Data.Tables. `GetAllAsync`: query all entities in the table, map to `GitHubRepositoryRecord` list. `GetByNameAsync`: query by partition key (username from stored token) and row key (repo name), return mapped record or null. `ReplaceAllAsync`: delete all existing entities for the username partition, then batch-insert new entities. Configure `TableClient` from `IOptions<GitHubOptions>` using RepoStorage settings. Create table if not exists. Sealed class, primary constructor.

- [X] T025 [US2] Add GetRepositoriesAsync with pagination to GitHubClient in src/BarretApi.Infrastructure/GitHub/GitHubClient.cs

> **T025 Details**: Replace the `NotImplementedException` stub. GET `/user/repos?type=owner&per_page=100&page={n}`. Use Bearer token from `GitHubTokenProvider.GetAccessTokenAsync`. Parse `Link` response header for `rel="next"` to handle pagination. Deserialize each page as `List<GitHubRepoResponse>`. Map to `GitHubRepositoryRecord` list with `SyncedAtUtc` set to current UTC. Handle 401 (clear token provider cache, throw), 429 (parse `X-RateLimit-Reset`, throw with reset time), other errors (throw with status code and message). Add structured logging for sync start, page fetched, sync complete.

- [X] T026 [US2] Create GitHubRepoSyncEndpoint and response in src/BarretApi.Api/Features/GitHub/

> **T026 Details**: Create 2 files: `GitHubRepoSyncResponse.cs` (properties: Count (int), SyncedAtUtc (DateTimeOffset), Username (string)), `GitHubRepoSyncEndpoint.cs` (POST `/api/github/repos/sync`, requires API key auth). Endpoint logic: get token from store (return 401 if missing); call `IGitHubClient.GetRepositoriesAsync`; call `IGitHubRepositoryStore.ReplaceAllAsync` with username and results; return 200 with count, timestamp, username. Handle exceptions: token revoked → 401, rate limit → 429, GitHub API error → 502. Follow FastEndpoints REPR pattern.

- [X] T027 [P] [US2] Create GitHubRepoListEndpoint and response in src/BarretApi.Api/Features/GitHub/

> **T027 Details**: Create 2 files: `GitHubRepoListResponse.cs` (properties: Repositories (list of repo summary objects with Name, FullName, Description, IsPrivate, DefaultBranch, HtmlUrl, UpdatedAtUtc), Count (int), SyncedAtUtc (DateTimeOffset?)), `GitHubRepoListEndpoint.cs` (GET `/api/github/repos`, requires API key auth). Reads all repos from `IGitHubRepositoryStore.GetAllAsync`. Returns 200 with mapped list even if empty. Follow FastEndpoints REPR pattern.

- [X] T028 [P] [US2] Create GitHubRepoDetailEndpoint, request, and response in src/BarretApi.Api/Features/GitHub/

> **T028 Details**: Create 3 files: `GitHubRepoDetailRequest.cs` (property: Name — bound from route parameter), `GitHubRepoDetailResponse.cs` (properties: Name, FullName, Description, IsPrivate, DefaultBranch, HtmlUrl, UpdatedAtUtc, SyncedAtUtc), `GitHubRepoDetailEndpoint.cs` (GET `/api/github/repos/{name}`, requires API key auth). Calls `IGitHubRepositoryStore.GetByNameAsync`. Returns 200 with repo details or 404 with message "Repository '{name}' not found. Run POST /api/github/repos/sync to refresh." Follow FastEndpoints REPR pattern.

- [X] T029 [US2] Register GitHub repository store service in src/BarretApi.Api/Program.cs

> **T029 Details**: Add `IGitHubRepositoryStore` → `AzureTableGitHubRepositoryStore` (scoped) to the existing GitHub DI registrations added in T020.

**Checkpoint**: User Stories 1 AND 2 both work independently. Can authenticate, sync repos, list repos, and get repo details.

---

## Phase 5: User Story 3 — Create a GitHub Issue (Priority: P3)

**Goal**: Create issues on synced repositories via the GitHub API.

**Independent Test**: After authenticating (US1) and syncing repos (US2), POST `/api/github/repos/{name}/issues` with a title creates an issue and returns the issue number and URL.

**Requirements covered**: FR-013, FR-014, FR-015, FR-016, FR-017, FR-018, FR-019, FR-020

### Tests for User Story 3

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T030 [P] [US3] Create GitHubClient_CreateIssueAsync_Tests in tests/BarretApi.Infrastructure.UnitTests/GitHub/GitHubClient_CreateIssueAsync_Tests.cs

> **T030 Details**: Test successful issue creation returns correct result. Test with optional body and labels sends all fields. Test 401 response throws unauthorized. Test 422 response (e.g., issues disabled) throws with message. Use NSubstitute with mock `HttpMessageHandler`. Use xUnit + Shouldly. Follow naming: `ReturnsIssueResult_GivenValidRequest`, `SendsAllFields_GivenBodyAndLabels`, `ThrowsUnauthorized_GivenRevokedToken`, `ThrowsApiError_GivenValidationFailed`.

- [X] T031 [P] [US3] Create GitHubCreateIssueEndpoint_Tests in tests/BarretApi.Api.UnitTests/Features/GitHub/GitHubCreateIssueEndpoint_Tests.cs

> **T031 Details**: Test successful creation returns 201 with issue details. Test repo not found in store returns 404. Test no token returns 401. Use NSubstitute for `IGitHubClient`, `IGitHubRepositoryStore`, `IGitHubTokenStore`. Use xUnit + Shouldly. Follow naming: `ReturnsCreated_GivenValidIssueRequest`, `ReturnsNotFound_GivenUnknownRepository`, `ReturnsUnauthorized_GivenNoToken`.

- [X] T032 [P] [US3] Create GitHubCreateIssueValidator_Tests in tests/BarretApi.Api.UnitTests/Features/GitHub/GitHubCreateIssueValidator_Tests.cs

> **T032 Details**: Test title is required (empty/null fails). Test repo name is required (empty/null fails). Test valid request passes. Test optional body and labels are accepted. Use xUnit + Shouldly. Instantiate validator directly, call `ValidateAsync`. Follow naming: `FailsValidation_GivenEmptyTitle`, `FailsValidation_GivenEmptyRepoName`, `PassesValidation_GivenValidRequest`, `PassesValidation_GivenOptionalFields`.

### Implementation for User Story 3

- [X] T033 [US3] Add CreateIssueAsync to GitHubClient in src/BarretApi.Infrastructure/GitHub/GitHubClient.cs

> **T033 Details**: Replace the `NotImplementedException` stub. POST `/repos/{owner}/{repo}/issues` with JSON body: `{ "title": "...", "body": "...", "labels": [...] }`. Use Bearer token from `GitHubTokenProvider.GetAccessTokenAsync`. Deserialize response as `GitHubIssueResponse`. Map to `GitHubIssueResult`. Handle 401 (clear cache, throw), 422 (throw with error details), 429 (rate limit), other errors. Add structured logging.

- [X] T034 [US3] Create GitHubCreateIssueEndpoint with request, response, and validator in src/BarretApi.Api/Features/GitHub/

> **T034 Details**: Create 4 files: `GitHubCreateIssueRequest.cs` (properties: Name (from route), Title (string), Body (string?), Labels (List<string>?)), `GitHubCreateIssueResponse.cs` (properties: Number (int), Title (string), HtmlUrl (string), State (string)), `GitHubCreateIssueValidator.cs` (FluentValidation: Title not empty, Name not empty), `GitHubCreateIssueEndpoint.cs` (POST `/api/github/repos/{name}/issues`, requires API key auth). Endpoint logic: get token (401 if missing); lookup repo in store by name (404 if not found, per FR-017); call `IGitHubClient.CreateIssueAsync` with owner from repo's FullName, repo name, title, body, labels; return 201 with issue result. Handle exceptions: token revoked → 401, rate limit → 429, GitHub API error → 502.

**Checkpoint**: All three user stories complete and independently functional. Full GitHub integration operational.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Documentation, formatting, and end-to-end validation

- [X] T035 [P] Update README.md with GitHub REST API feature documentation
- [X] T036 [P] Run dotnet format and verify clean build with no warnings
- [X] T037 Validate quickstart.md scenarios end-to-end

> **T035 Details**: Add a "GitHub REST API Integration" section to README.md covering: feature description, 7 endpoints with methods/paths/auth requirements, configuration parameters table (Config Key / Aspire Parameter / Environment Variable / Required / Default / Description), example request/response for each endpoint. Follow existing README formatting conventions.

> **T036 Details**: Run `dotnet format` across the solution. Run `dotnet build` and verify zero errors and zero warnings. Fix any formatting or analyzer issues.

> **T037 Details**: Follow quickstart.md step by step: configure User Secrets, run AppHost, complete OAuth flow, verify profile, sync repos, list repos, get repo detail, create issue. Verify each response matches the contract specifications. Fix any issues found.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion — BLOCKS all user stories
- **User Story 1 (Phase 3)**: Depends on Foundational (Phase 2) — No dependencies on other stories
- **User Story 2 (Phase 4)**: Depends on Foundational (Phase 2) — Requires US1 authentication at runtime but can be coded independently
- **User Story 3 (Phase 5)**: Depends on Foundational (Phase 2) — Requires US1 auth and US2 repo store at runtime but can be coded independently
- **Polish (Phase 6)**: Depends on all user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: No dependencies on other stories. Provides authentication infrastructure used by US2 and US3 at runtime.
- **User Story 2 (P2)**: Can be coded in parallel with US1 (uses interfaces). At runtime, requires a stored token from US1.
- **User Story 3 (P3)**: Can be coded in parallel with US1/US2 (uses interfaces). At runtime, requires stored token (US1) and stored repos (US2) for repository validation (FR-017).

### Within Each User Story

- Tests MUST be written and FAIL before implementation
- Infrastructure implementations before API endpoints
- GitHubClient methods before endpoints that call them
- DI registration after implementations are complete
- Story checkpoint validation before moving to next priority

### Parallel Opportunities

- All Foundational tasks T002–T012 marked [P] can run in parallel
- Within US1: T013 and T014 (tests) can run in parallel; T017 and T019 (separate endpoints) can run in parallel
- Within US2: T021, T022, T023 (tests) can run in parallel; T027 and T028 (list/detail endpoints) can run in parallel
- Within US3: T030, T031, T032 (tests) can run in parallel
- Polish tasks T035 and T036 can run in parallel

---

## Parallel Example: Foundational Phase

```text
# All foundational tasks can run in parallel (different files):
T002: GitHubOptions configuration
T003: GitHubTokenRecord model
T004: GitHubRepositoryRecord model
T005: GitHubIssueResult model
T006: IGitHubTokenStore interface
T007: IGitHubRepositoryStore interface
T008: IGitHubClient interface
T009: GitHubApiModels
T011: GitHubOptions_Validate_Tests
T012: GitHubTokenProvider_Tests

# Then sequentially (depends on interfaces):
T010: GitHubTokenProvider (depends on T006 IGitHubTokenStore)
```

## Parallel Example: User Story 1

```text
# Tests first (parallel):
T013: AzureTableGitHubTokenStore_Tests
T014: GitHubAuthCallbackEndpoint_Tests

# Infrastructure (sequential — client depends on token store pattern):
T015: AzureTableGitHubTokenStore
T016: GitHubClient (OAuth methods)

# Endpoints (T017 and T019 parallel, T018 sequential after T016):
T017: GitHubAuthEndpoint (independent — just builds URL)
T018: GitHubAuthCallbackEndpoint (depends on T016 client)
T019: GitHubProfileEndpoint (independent — reads store only)

# DI last:
T020: Register services
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001)
2. Complete Phase 2: Foundational (T002–T012)
3. Complete Phase 3: User Story 1 (T013–T020)
4. **STOP and VALIDATE**: Test OAuth flow end-to-end
5. Deploy/demo if ready — GitHub authentication is functional

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. Add User Story 1 → Test OAuth independently → Deploy/Demo (MVP!)
3. Add User Story 2 → Test repo sync independently → Deploy/Demo
4. Add User Story 3 → Test issue creation independently → Deploy/Demo
5. Each story adds value without breaking previous stories

### Parallel Team Strategy

With multiple developers:

1. Team completes Setup + Foundational together
2. Once Foundational is done:
   - Developer A: User Story 1 (authentication)
   - Developer B: User Story 2 (repositories — code against interfaces)
   - Developer C: User Story 3 (issues — code against interfaces)
3. Integration testing after all stories merge

---

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks
- [Story] label maps task to specific user story for traceability
- Each user story is independently completable and testable
- Tests must fail before implementing
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- GitHubClient is built incrementally: OAuth methods (US1) → GetRepositoriesAsync (US2) → CreateIssueAsync (US3)
- All endpoints follow FastEndpoints REPR pattern with separate Request/Response/Validator files
- All infrastructure follows existing LinkedIn vertical slice pattern

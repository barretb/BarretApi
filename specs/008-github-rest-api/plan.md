# Implementation Plan: GitHub REST API Integration

**Branch**: `008-github-rest-api` | **Date**: 2026-03-25 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/008-github-rest-api/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

Add GitHub REST API integration to BarretApi, enabling OAuth-based authentication, repository listing with local storage, and issue creation. The implementation follows the existing LinkedIn OAuth pattern: FastEndpoints REPR endpoints in the API layer, interfaces in Core, and typed HttpClient + Azure Table Storage implementations in Infrastructure. Configuration is managed via Aspire AppHost parameters.

## Technical Context

**Language/Version**: C# / .NET 10.0 (`net10.0`)
**Primary Dependencies**: FastEndpoints 8.x, Aspire 13, Azure.Data.Tables, Microsoft.Extensions.Http.Resilience
**Storage**: Azure Table Storage (via Aspire-provisioned Azurite in development) — for OAuth tokens and repository snapshots
**Testing**: xUnit, NSubstitute, Shouldly
**Target Platform**: Linux server (container) via Aspire
**Project Type**: Web service (additional endpoints on existing API)
**Performance Goals**: < 5 seconds for repository sync (typical account); < 3 seconds for issue creation
**Constraints**: GitHub API rate limit (5,000 req/hr authenticated); OAuth requires HTTPS callback URL; single-user model (API owner only)
**Scale/Scope**: 6 new endpoints added to existing API; no new projects needed; follows established LinkedIn OAuth vertical slice pattern

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| # | Principle | Status | Notes |
|---|-----------|--------|-------|
| I | Code Quality & Consistency | PASS | All naming conventions followed: `_httpClient` fields, `PascalCase` constants, `Async` suffixes, file-scoped namespaces, Allman braces. `dotnet format` before merge. |
| II | Clean Architecture & REPR Design | PASS | REPR pattern for all endpoints with `*Request`/`*Response` suffixes. Interfaces in Core (`IGitHubTokenStore`, `IGitHubClient`, `IGitHubRepositoryStore`), implementations in Infrastructure. DTOs for cross-layer transfer. |
| III | Test-Driven Quality Assurance | PASS | xUnit + NSubstitute + Shouldly. Tests for validators, endpoint logic, token store, client, and repository store. Class naming: `GitHubClient_GetRepositoriesAsync_Tests`, etc. |
| IV | Centralized Configuration via Aspire | PASS | GitHub OAuth credentials (client ID, client secret) via Aspire parameters and User Secrets. Table names and connection strings via AppHost config. No appsettings in API project. |
| V | Secure by Design | PASS | OAuth state parameter validated (CSRF protection). Tokens stored securely. Input validation via FluentValidation. Proper HTTP status codes (400, 401, 404, 502). |
| VI | Observability & Structured Logging | PASS | Structured logging for OAuth flow, API calls, errors. No sensitive data (tokens/secrets) in logs. |
| VII | Simplicity & Maintainability | PASS | Follows existing LinkedIn pattern exactly. No new projects, no over-engineering. Methods < 20 lines. Async throughout. YAGNI respected. |

## Project Structure

### Documentation (this feature)

```text
specs/008-github-rest-api/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output (API contracts)
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
src/
├── BarretApi.Api/
│   └── Features/
│       └── GitHub/
│           ├── GitHubAuthEndpoint.cs                  # GET /api/github/auth — initiate OAuth flow
│           ├── GitHubAuthCallbackEndpoint.cs           # GET /api/github/auth/callback — OAuth callback
│           ├── GitHubAuthCallbackRequest.cs            # Request: code, state, error
│           ├── GitHubAuthCallbackResponse.cs           # Response: username, status
│           ├── GitHubAuthCallbackValidator.cs          # Validates callback params
│           ├── GitHubProfileEndpoint.cs                # GET /api/github/profile — connection status
│           ├── GitHubProfileResponse.cs                # Response: username, connected status
│           ├── GitHubRepoSyncEndpoint.cs               # POST /api/github/repos/sync — trigger sync
│           ├── GitHubRepoSyncResponse.cs               # Response: count synced
│           ├── GitHubRepoListEndpoint.cs               # GET /api/github/repos — list stored repos
│           ├── GitHubRepoListResponse.cs               # Response: list of repo summaries
│           ├── GitHubRepoDetailEndpoint.cs             # GET /api/github/repos/{name} — single repo
│           ├── GitHubRepoDetailRequest.cs              # Request: name path param
│           ├── GitHubRepoDetailResponse.cs             # Response: repo details
│           ├── GitHubCreateIssueEndpoint.cs            # POST /api/github/repos/{name}/issues — create issue
│           ├── GitHubCreateIssueRequest.cs             # Request: title, body, labels
│           ├── GitHubCreateIssueResponse.cs            # Response: issue number, URL, state
│           └── GitHubCreateIssueValidator.cs           # Validates title required, repo name
├── BarretApi.Core/
│   ├── Configuration/
│   │   └── GitHubOptions.cs                           # Options: ClientId, ClientSecret, TokenStorage, RepoStorage
│   ├── Interfaces/
│   │   ├── IGitHubTokenStore.cs                       # Save/get GitHub tokens
│   │   ├── IGitHubRepositoryStore.cs                  # Save/get repository snapshots
│   │   └── IGitHubClient.cs                           # Fetch repos, create issues from GitHub API
│   └── Models/
│       ├── GitHubTokenRecord.cs                       # Token data for persistence
│       ├── GitHubRepositoryRecord.cs                  # Repository snapshot data
│       └── GitHubIssueResult.cs                       # Issue creation result
├── BarretApi.Infrastructure/
│   └── GitHub/
│       ├── AzureTableGitHubTokenStore.cs              # IGitHubTokenStore implementation
│       ├── AzureTableGitHubRepositoryStore.cs         # IGitHubRepositoryStore implementation
│       ├── GitHubClient.cs                            # IGitHubClient implementation (typed HttpClient)
│       ├── GitHubTokenProvider.cs                     # Token caching + refresh detection
│       └── Models/
│           └── GitHubApiModels.cs                     # Internal deserialization models for GitHub API responses
└── BarretApi.AppHost/
    └── Program.cs                                     # Add GitHub parameters and environment variables

tests/
├── BarretApi.Core.UnitTests/
│   └── Configuration/
│       └── GitHubOptions_Validate_Tests.cs            # Options validation tests
├── BarretApi.Infrastructure.UnitTests/
│   └── GitHub/
│       ├── AzureTableGitHubTokenStore_Tests.cs        # Token store tests
│       ├── AzureTableGitHubRepositoryStore_Tests.cs   # Repo store tests
│       ├── GitHubClient_GetRepositoriesAsync_Tests.cs # API client tests
│       ├── GitHubClient_CreateIssueAsync_Tests.cs     # API client tests
│       └── GitHubTokenProvider_Tests.cs               # Token caching tests
└── BarretApi.Api.UnitTests/
    └── Features/
        └── GitHub/
            ├── GitHubAuthCallbackEndpoint_Tests.cs    # Callback endpoint tests
            ├── GitHubCreateIssueEndpoint_Tests.cs     # Issue creation endpoint tests
            ├── GitHubCreateIssueValidator_Tests.cs    # Validator tests
            └── GitHubRepoSyncEndpoint_Tests.cs        # Sync endpoint tests
```

**Structure Decision**: This feature adds a new `GitHub/` folder under `Features/` (API layer) and a new `GitHub/` folder under Infrastructure, following the same pattern as the existing `LinkedIn/`, `Nasa/`, `Bluesky/`, and `DiceBear/` integrations. Models and interfaces are added to existing Core folders. No new projects are needed.

## Complexity Tracking

No constitution violations. The feature follows the established LinkedIn OAuth vertical slice pattern directly. The three concerns (auth, repos, issues) are all part of a single GitHub integration and share authentication infrastructure, so they belong together rather than in separate features.

## Constitution Re-Check (Post Phase 1 Design)

*Re-evaluated after completing data model, contracts, and quickstart.*

| # | Principle | Status | Post-Design Notes |
|---|-----------|--------|-------------------|
| I | Code Quality & Consistency | PASS | All new files follow naming conventions: `_httpClient`, `_tokenStore` (underscore-prefixed private fields), `PascalCase` for constants, `GetRepositoriesAsync` / `CreateIssueAsync` (async suffixes), file-scoped namespaces, sealed classes throughout. |
| II | Clean Architecture & REPR Design | PASS | REPR confirmed for all 7 endpoints. Request/Response suffixes on all DTOs. Interfaces in Core (`IGitHubTokenStore`, `IGitHubRepositoryStore`, `IGitHubClient`), implementations in Infrastructure (`AzureTableGitHubTokenStore`, `AzureTableGitHubRepositoryStore`, `GitHubClient`). `GitHubRepositoryRecord` is a model, not exposed as a domain entity. |
| III | Test-Driven Quality Assurance | PASS | Test classes defined for all layers: validators, endpoints, token store, repo store, client. xUnit + NSubstitute + Shouldly. Naming: `GitHubClient_GetRepositoriesAsync_Tests`, `GitHubCreateIssueValidator_Tests`, etc. |
| IV | Centralized Configuration via Aspire | PASS | `GitHubOptions` with nested `GitHubTokenStorageOptions` and `GitHubRepoStorageOptions`. All secrets via Aspire parameters. No appsettings in API project. Connection strings and table names configurable via AppHost. |
| V | Secure by Design | PASS | OAuth state parameter validated against CSRF. Tokens stored securely in Azure Table Storage. Input validated via FluentValidation (title required, repo name required). HTTP 400/401/404/429/502 status codes mapped. No sensitive data in responses. |
| VI | Observability & Structured Logging | PASS | Structured logging for OAuth flow steps, API calls, sync operations, and errors. No tokens or secrets in log output. Correlation via Aspire defaults. |
| VII | Simplicity & Maintainability | PASS | No new projects, no new NuGet packages. Methods designed < 20 lines. Async throughout. Simplified token provider (no refresh logic — YAGNI for non-expiring GitHub tokens). Full-replace sync strategy is simple and correct. |

**Conclusion**: All 7 gates pass on both pre-research and post-design checks. No violations to justify. Design is ready for task generation via `/speckit.tasks`.

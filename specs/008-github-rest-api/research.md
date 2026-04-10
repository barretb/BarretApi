# Research: GitHub REST API Integration

**Feature**: 008-github-rest-api
**Date**: 2026-03-25

## 1. GitHub OAuth Web Application Flow

### Decision: Use GitHub OAuth App (Web Application Flow)

**Rationale**: The spec calls for a standard OAuth authorization code flow. GitHub supports two types of OAuth: OAuth Apps and GitHub Apps. OAuth Apps are simpler, grant user-level access, and align with the existing LinkedIn OAuth pattern in the codebase. GitHub Apps are designed for installations across multiple repositories/organizations and are more complex than needed for a single-user API owner scenario.

**Alternatives considered**:

- **GitHub App**: More granular permissions via installation tokens, but adds complexity (webhook-based setup, JWT authentication, installation tokens). Overkill for a single-user, owner-only use case.
- **Personal Access Token (PAT)**: Simplest approach — just a static token configured as a secret. No OAuth flow needed. Rejected because the spec explicitly requires an OAuth flow with authorization endpoints, and PATs don't support the callback-based user experience that matches the existing LinkedIn pattern.

### OAuth Flow Details

1. **Authorization URL**: `https://github.com/login/oauth/authorize`
   - Parameters: `client_id`, `redirect_uri`, `scope`, `state`
   - Scope needed: `repo` (full control of private repositories, includes issue write access)

2. **Token Exchange URL**: `https://github.com/login/oauth/access_token`
   - POST with `client_id`, `client_secret`, `code`, `redirect_uri`
   - Set `Accept: application/json` header to receive JSON response
   - Response: `access_token`, `token_type`, `scope`

3. **Key difference from LinkedIn**: GitHub OAuth tokens do **not expire** by default (no refresh token flow). Tokens remain valid until the user revokes them on GitHub. This simplifies the token provider — no refresh logic needed, just revocation detection via 401 responses.

4. **State parameter**: Random GUID for CSRF protection, same pattern as LinkedIn implementation.

## 2. GitHub REST API Endpoints

### Decision: Use GitHub REST API v3 with `Accept: application/vnd.github+json` header

**Rationale**: The REST API is stable, well-documented, and covers all three use cases (user info, repos, issues). GraphQL is an alternative but adds unnecessary complexity for these straightforward read/write operations.

**Alternatives considered**:

- **GraphQL API (v4)**: More efficient for complex nested queries, but our operations are simple list/create calls. GraphQL adds query construction complexity without benefit.
- **Octokit .NET SDK**: Official .NET client library. Rejected because the codebase consistently uses typed `HttpClient` with direct HTTP calls for external APIs (LinkedIn, NASA, DiceBear). Using Octokit would introduce an inconsistent pattern and a heavy dependency.

### Endpoints Used

| Operation | Method | URL | Auth |
| --------- | ------ | --- | ---- |
| Get authenticated user | GET | `/user` | Bearer token |
| List user repositories | GET | `/user/repos?type=owner&per_page=100&page={n}` | Bearer token |
| Create issue | POST | `/repos/{owner}/{repo}/issues` | Bearer token |

### Pagination

GitHub uses Link header-based pagination. The `Link` response header contains `rel="next"` URLs when more pages exist. The `per_page` parameter maxes at 100. For the repository list, iterate pages until no `next` link is present.

### Rate Limiting

Authenticated requests: 5,000/hour. Rate limit info is in response headers:

- `X-RateLimit-Limit`: Total allowed
- `X-RateLimit-Remaining`: Remaining
- `X-RateLimit-Reset`: UTC epoch seconds for reset time

When `X-RateLimit-Remaining` reaches 0, return 429 with the reset time to the caller.

### API Version Header

GitHub recommends sending `X-GitHub-Api-Version: 2022-11-28` header to pin the API version and avoid breaking changes.

## 3. Token Storage Pattern

### Decision: Follow LinkedIn token store pattern with Azure Table Storage

**Rationale**: The codebase has an established pattern (`AzureTableLinkedInTokenStore`) that uses a single table with a fixed partition key and row key for single-user token storage. The GitHub token store should follow this exact pattern for consistency.

**Alternatives considered**:

- **In-memory only**: Simpler but tokens are lost on restart, requiring reauthentication. Rejected.
- **File-based storage**: Would work but breaks the established Azure Table Storage pattern. Rejected.

### Table Design

- **Table name**: Configurable via Aspire parameter (default: `githubtokens`)
- **Partition key**: `"github-tokens"` (constant)
- **Row key**: `"current"` (constant, single-user model)
- **Properties**: `AccessToken`, `Username`, `Scope`, `UpdatedAtUtc`

## 4. Repository Storage Pattern

### Decision: Use Azure Table Storage with one entity per repository

**Rationale**: Repositories need to be queryable individually by name and as a full list. Azure Table Storage supports this with partition-key-based queries. Each repository becomes a row in the table, with the owner as partition key and repository name as row key.

**Alternatives considered**:

- **Single JSON blob**: Store all repos as one JSON document. Simpler write but harder to query individual repos. Rejected because FR-011 requires single-repo lookup.
- **In-memory cache only**: Fast but lost on restart. Rejected — the sync operation should persist so repos are available immediately after restart without needing a fresh sync.

### Table Design

- **Table name**: Configurable via Aspire parameter (default: `githubrepositories`)
- **Partition key**: GitHub username (owner) — natural partition for all repos belonging to one user
- **Row key**: Repository name (unique within an owner)
- **Properties**: `FullName`, `Description`, `IsPrivate`, `DefaultBranch`, `HtmlUrl`, `UpdatedAtUtc`, `SyncedAtUtc`

### Sync Strategy

Full replace on each sync: delete all existing rows for the partition, then insert new rows. This matches FR-012 ("replace the previously stored repository list on each sync"). Use batch operations for efficiency.

## 5. Token Provider Pattern

### Decision: Simplified token provider without refresh logic

**Rationale**: Unlike LinkedIn tokens, GitHub OAuth tokens do not expire on a schedule. They remain valid until explicitly revoked by the user. Therefore, the token provider is simpler than `LinkedInTokenProvider` — it reads from the store and returns the token. Revocation is detected downstream when the GitHub API returns a 401.

**Alternatives considered**:

- **Full refresh pattern like LinkedIn**: Would add unused complexity since GitHub tokens don't expire. YAGNI principle applies.
- **Pre-validation on every request**: Call `/user` to verify the token before every operation. Rejected — adds latency and wastes rate limit quota.

### Implementation

- Read token from store on first use
- Cache in memory (no expiration check needed)
- If a downstream call gets 401, clear cache and return an error suggesting reauthentication
- No `SemaphoreSlim` needed since there's no concurrent refresh scenario

## 6. Configuration Structure

### Decision: Follow LinkedIn options pattern with `GitHubOptions` class

**Rationale**: Matches the established `LinkedInOptions` pattern with nested storage options classes, Aspire parameter mapping, and `IOptions<T>` binding.

### Configuration Map

| Config Key | Aspire Parameter | Environment Variable | Required | Description |
| ---------- | ---------------- | -------------------- | -------- | ----------- |
| GitHub:ClientId | github-client-id | GitHub__ClientId | Yes | OAuth App client ID |
| GitHub:ClientSecret | github-client-secret | GitHub__ClientSecret | Yes | OAuth App client secret |
| GitHub:ApiBaseUrl | github-api-base-url | GitHub__ApiBaseUrl | No | Default: `https://api.github.com` |
| GitHub:OAuthBaseUrl | github-oauth-base-url | GitHub__OAuthBaseUrl | No | Default: `https://github.com` |
| GitHub:TokenStorage:ConnectionString | — | GitHub__TokenStorage__ConnectionString | No | Azurite connection string |
| GitHub:TokenStorage:AccountEndpoint | github-token-storage-account-endpoint | GitHub__TokenStorage__AccountEndpoint | No | Azure Table endpoint |
| GitHub:TokenStorage:TableName | github-token-storage-table-name | GitHub__TokenStorage__TableName | No | Default: `githubtokens` |
| GitHub:RepoStorage:ConnectionString | — | GitHub__RepoStorage__ConnectionString | No | Azurite connection string |
| GitHub:RepoStorage:AccountEndpoint | github-repo-storage-account-endpoint | GitHub__RepoStorage__AccountEndpoint | No | Azure Table endpoint |
| GitHub:RepoStorage:TableName | github-repo-storage-table-name | GitHub__RepoStorage__TableName | No | Default: `githubrepositories` |

## 7. Error Handling Strategy

### Decision: Consistent error responses with problem-specific messages

**Rationale**: FR-018 through FR-020 require clear, actionable error messages. Follow the existing pattern of returning meaningful HTTP status codes with descriptive messages.

### Error Mapping

| Scenario | HTTP Status | Message Pattern |
| -------- | ----------- | --------------- |
| No GitHub token stored | 401 | "GitHub authentication required. Visit /api/github/auth to connect." |
| Token revoked (GitHub 401) | 401 | "GitHub token is no longer valid. Please reauthenticate via /api/github/auth." |
| Rate limit exceeded | 429 | "GitHub API rate limit exceeded. Resets at {resetTime}." |
| Repository not found in store | 404 | "Repository '{name}' not found. Run POST /api/github/repos/sync to refresh." |
| GitHub API error | 502 | "GitHub API error: {statusCode} — {message}" |
| OAuth callback error | 400 | "GitHub authentication failed: {error_description}" |
| Invalid OAuth state | 400 | "Invalid OAuth state parameter. Please restart the authentication flow." |

## 8. Existing Package Compatibility

### Decision: No new NuGet packages required

**Rationale**: All necessary packages are already in `Directory.Packages.props`:

- `Azure.Data.Tables` — for token and repository table storage
- `Azure.Identity` — for `DefaultAzureCredential` in production
- `FastEndpoints` — for REPR endpoint pattern
- `Microsoft.Extensions.Http.Resilience` — for resilient HTTP client
- `Microsoft.Extensions.Logging.Abstractions` — for structured logging
- `Microsoft.Extensions.Options` — for `IOptions<T>` pattern

No additional dependencies needed. The GitHub REST API uses standard HTTP with JSON, which is handled by the built-in `System.Net.Http.HttpClient` and `System.Text.Json`.

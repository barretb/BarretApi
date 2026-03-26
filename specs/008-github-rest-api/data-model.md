# Data Model: GitHub REST API Integration

**Feature**: 008-github-rest-api
**Date**: 2026-03-25

## Entities

### GitHubTokenRecord

Represents the stored OAuth token for the authenticated GitHub user.

| Field | Type | Required | Description |
| ----- | ---- | -------- | ----------- |
| AccessToken | string | Yes | OAuth access token for API calls |
| Username | string | Yes | Authenticated GitHub username |
| Scope | string | Yes | OAuth scopes granted (e.g., "repo") |
| UpdatedAtUtc | DateTimeOffset | Yes | Timestamp when tokens were last saved |

**Storage**: Azure Table Storage

- Table: configurable (default `githubtokens`)
- Partition Key: `"github-tokens"` (constant)
- Row Key: `"current"` (constant — single-user model)

**Relationships**: None (standalone entity)

**State Transitions**: Created on first OAuth callback → Updated on re-authentication → Implicitly invalidated when user revokes on GitHub

---

### GitHubRepositoryRecord

Represents a snapshot of a repository owned by the authenticated user, synced from the GitHub API.

| Field | Type | Required | Description |
| ----- | ---- | -------- | ----------- |
| Name | string | Yes | Repository name (e.g., "my-repo") |
| FullName | string | Yes | Full name including owner (e.g., "owner/my-repo") |
| Description | string | No | Repository description (may be null) |
| IsPrivate | bool | Yes | Whether the repository is private |
| DefaultBranch | string | Yes | Default branch name (e.g., "main") |
| HtmlUrl | string | Yes | URL to view the repository on GitHub |
| UpdatedAtUtc | DateTimeOffset | Yes | Last updated timestamp from GitHub |
| SyncedAtUtc | DateTimeOffset | Yes | Timestamp when this record was synced |

**Storage**: Azure Table Storage

- Table: configurable (default `githubrepositories`)
- Partition Key: GitHub username (owner)
- Row Key: Repository name

**Relationships**: Belongs to a GitHub Connection (via shared username/owner)

**Validation Rules**:

- Name must not be empty
- FullName must follow `{owner}/{name}` format
- HtmlUrl must be a valid URL

---

### GitHubIssueResult

Represents the result of creating an issue on GitHub. This is a transient object returned from the API call — not persisted.

| Field | Type | Required | Description |
| ----- | ---- | -------- | ----------- |
| Number | int | Yes | Issue number assigned by GitHub |
| Title | string | Yes | Issue title as created |
| HtmlUrl | string | Yes | URL to view the issue on GitHub |
| State | string | Yes | Issue state (always "open" on creation) |

**Storage**: Not persisted — returned inline from the create issue API call

**Relationships**: Associated with a GitHubRepositoryRecord (by repository name)

---

## Configuration Entities

### GitHubOptions

Strongly-typed configuration class bound via `IOptions<GitHubOptions>`.

| Field | Type | Required | Default | Description |
| ----- | ---- | -------- | ------- | ----------- |
| ClientId | string | Yes | — | GitHub OAuth App client ID |
| ClientSecret | string | Yes | — | GitHub OAuth App client secret |
| ApiBaseUrl | string | No | `https://api.github.com` | GitHub REST API base URL |
| OAuthBaseUrl | string | No | `https://github.com` | GitHub OAuth base URL |
| TokenStorage | GitHubTokenStorageOptions | No | (defaults) | Token table storage configuration |
| RepoStorage | GitHubRepoStorageOptions | No | (defaults) | Repository table storage configuration |

### GitHubTokenStorageOptions

| Field | Type | Required | Default | Description |
| ----- | ---- | -------- | ------- | ----------- |
| ConnectionString | string | No | — | Azure Table Storage connection string |
| AccountEndpoint | string | No | — | Azure Table Storage account endpoint |
| TableName | string | No | `githubtokens` | Table name for token storage |

### GitHubRepoStorageOptions

| Field | Type | Required | Default | Description |
| ----- | ---- | -------- | ------- | ----------- |
| ConnectionString | string | No | — | Azure Table Storage connection string |
| AccountEndpoint | string | No | — | Azure Table Storage account endpoint |
| TableName | string | No | `githubrepositories` | Table name for repository storage |

## Entity Relationship Diagram

```
┌─────────────────────┐
│  GitHubTokenRecord   │
│─────────────────────│
│  AccessToken         │
│  Username ───────────┼──┐
│  Scope               │  │
│  UpdatedAtUtc        │  │
└─────────────────────┘  │
                          │  (username = owner)
┌─────────────────────┐  │
│ GitHubRepositoryRecord│  │
│─────────────────────│  │
│  Name                │  │
│  FullName            │  │
│  Description         │  │
│  IsPrivate           │  │
│  DefaultBranch       │  │
│  HtmlUrl             │  │
│  UpdatedAtUtc        │  │
│  SyncedAtUtc         │  │
│  [PK: Owner] ────────┼──┘
│  [RK: Name]          │
└────────┬────────────┘
         │ (repo name)
         │
┌────────┴────────────┐
│  GitHubIssueResult   │
│─────────────────────│
│  Number              │  (transient — not stored)
│  Title               │
│  HtmlUrl             │
│  State               │
└─────────────────────┘
```

# API Contract: GitHub Repositories

**Feature**: 008-github-rest-api
**Date**: 2026-03-25

## Endpoints

### POST /api/github/repos/sync — Sync Repositories

Fetches all repositories owned by the authenticated GitHub user and stores them locally, replacing any previously stored data.

#### Request

No request body. Requires API key via `X-Api-Key` header.

#### Response

##### Success (200 OK)

```json
{
  "count": 42,
  "syncedAtUtc": "2026-03-25T14:35:00Z",
  "username": "octocat"
}
```

##### Not Authenticated (401 Unauthorized)

```json
{
  "statusCode": 401,
  "message": "GitHub authentication required. Visit /api/github/auth to connect."
}
```

##### GitHub API Error (502 Bad Gateway)

```json
{
  "statusCode": 502,
  "message": "GitHub API error: 500 — Internal Server Error"
}
```

##### Rate Limit Exceeded (429 Too Many Requests)

```json
{
  "statusCode": 429,
  "message": "GitHub API rate limit exceeded. Resets at 2026-03-25T15:00:00Z."
}
```

---

### GET /api/github/repos — List Repositories

Returns all locally stored GitHub repositories.

#### Request

No parameters. Requires API key via `X-Api-Key` header.

#### Response

##### Success (200 OK)

```json
{
  "repositories": [
    {
      "name": "my-repo",
      "fullName": "octocat/my-repo",
      "description": "A sample repository",
      "isPrivate": false,
      "defaultBranch": "main",
      "htmlUrl": "https://github.com/octocat/my-repo",
      "updatedAtUtc": "2026-03-20T10:00:00Z"
    },
    {
      "name": "private-project",
      "fullName": "octocat/private-project",
      "description": null,
      "isPrivate": true,
      "defaultBranch": "develop",
      "htmlUrl": "https://github.com/octocat/private-project",
      "updatedAtUtc": "2026-03-24T18:30:00Z"
    }
  ],
  "count": 2,
  "syncedAtUtc": "2026-03-25T14:35:00Z"
}
```

##### No Repositories Synced (200 OK)

```json
{
  "repositories": [],
  "count": 0,
  "syncedAtUtc": null
}
```

##### Not Authenticated (401 Unauthorized)

```json
{
  "statusCode": 401,
  "message": "GitHub authentication required. Visit /api/github/auth to connect."
}
```

---

### GET /api/github/repos/{name} — Get Repository Details

Returns details for a single stored repository by name.

#### Request

##### Path Parameters

| Parameter | Type | Required | Description |
| --------- | ---- | -------- | ----------- |
| name | string | Yes | Repository name (e.g., "my-repo") |

##### Example Requests

```
GET /api/github/repos/my-repo
GET /api/github/repos/private-project
```

#### Response

##### Success (200 OK)

```json
{
  "name": "my-repo",
  "fullName": "octocat/my-repo",
  "description": "A sample repository",
  "isPrivate": false,
  "defaultBranch": "main",
  "htmlUrl": "https://github.com/octocat/my-repo",
  "updatedAtUtc": "2026-03-20T10:00:00Z",
  "syncedAtUtc": "2026-03-25T14:35:00Z"
}
```

##### Not Found (404 Not Found)

```json
{
  "statusCode": 404,
  "message": "Repository 'unknown-repo' not found. Run POST /api/github/repos/sync to refresh."
}
```

##### Not Authenticated (401 Unauthorized)

```json
{
  "statusCode": 401,
  "message": "GitHub authentication required. Visit /api/github/auth to connect."
}
```

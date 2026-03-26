# API Contract: GitHub Issues

**Feature**: 008-github-rest-api
**Date**: 2026-03-25

## Endpoints

### POST /api/github/repos/{name}/issues — Create Issue

Creates a new issue on the specified GitHub repository.

#### Request

##### Path Parameters

| Parameter | Type | Required | Description |
| --------- | ---- | -------- | ----------- |
| name | string | Yes | Repository name (e.g., "my-repo") |

##### Request Body (JSON)

| Field | Type | Required | Description |
| ----- | ---- | -------- | ----------- |
| title | string | Yes | Issue title |
| body | string | No | Issue body (Markdown supported) |
| labels | string[] | No | List of label names to apply |

Requires API key via `X-Api-Key` header.

##### Example Requests

**Minimal (title only)**:

```http
POST /api/github/repos/my-repo/issues
Content-Type: application/json
X-Api-Key: your-api-key

{
  "title": "Fix login page styling"
}
```

**Full (all fields)**:

```http
POST /api/github/repos/my-repo/issues
Content-Type: application/json
X-Api-Key: your-api-key

{
  "title": "Add dark mode support",
  "body": "## Description\n\nUsers have requested a dark mode option.\n\n## Acceptance Criteria\n- Toggle in settings\n- Persists across sessions",
  "labels": ["enhancement", "ui"]
}
```

#### Response

##### Success (201 Created)

```json
{
  "number": 42,
  "title": "Add dark mode support",
  "htmlUrl": "https://github.com/octocat/my-repo/issues/42",
  "state": "open"
}
```

##### Validation Error (400 Bad Request)

```json
{
  "statusCode": 400,
  "message": "One or more validation errors occurred.",
  "errors": {
    "title": ["'title' must not be empty."]
  }
}
```

##### Repository Not Found (404 Not Found)

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

##### Token Revoked (401 Unauthorized)

```json
{
  "statusCode": 401,
  "message": "GitHub token is no longer valid. Please reauthenticate via /api/github/auth."
}
```

##### GitHub API Error (502 Bad Gateway)

```json
{
  "statusCode": 502,
  "message": "GitHub API error: 422 — Validation Failed (Issues are disabled for this repository)"
}
```

##### Rate Limit Exceeded (429 Too Many Requests)

```json
{
  "statusCode": 429,
  "message": "GitHub API rate limit exceeded. Resets at 2026-03-25T15:00:00Z."
}
```

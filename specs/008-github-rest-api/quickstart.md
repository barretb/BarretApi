# Quickstart: GitHub REST API Integration

**Feature**: 008-github-rest-api
**Date**: 2026-03-25

## Prerequisites

1. .NET 10 SDK installed
2. Docker Desktop running (for Azurite table storage)
3. A GitHub OAuth App created at [https://github.com/settings/developers](https://github.com/settings/developers)
   - **Authorization callback URL**: `https://localhost:{port}/api/github/auth/callback`
   - Note the **Client ID** and **Client Secret**

## Configuration

### 1. Set GitHub OAuth secrets in Aspire AppHost User Secrets

```bash
cd src/BarretApi.AppHost
dotnet user-secrets set "Parameters:github-client-id" "your-github-client-id"
dotnet user-secrets set "Parameters:github-client-secret" "your-github-client-secret"
```

### 2. Run the application

```bash
dotnet run --project src/BarretApi.AppHost/BarretApi.AppHost.csproj
```

## Usage Walkthrough

### Step 1: Authenticate with GitHub

Open in a browser:

```
GET https://localhost:{port}/api/github/auth
```

This redirects to GitHub's authorization page. After authorizing, you'll be redirected back to the callback URL. The response confirms your connection:

```json
{
  "username": "your-github-username",
  "status": "connected",
  "scope": "repo"
}
```

### Step 2: Verify connection

```bash
curl https://localhost:{port}/api/github/profile
```

```json
{
  "username": "your-github-username",
  "connected": true,
  "scope": "repo",
  "connectedAtUtc": "2026-03-25T14:30:00Z"
}
```

### Step 3: Sync repositories

```bash
curl -X POST https://localhost:{port}/api/github/repos/sync \
  -H "X-Api-Key: your-api-key"
```

```json
{
  "count": 42,
  "syncedAtUtc": "2026-03-25T14:35:00Z",
  "username": "your-github-username"
}
```

### Step 4: List repositories

```bash
curl https://localhost:{port}/api/github/repos \
  -H "X-Api-Key: your-api-key"
```

```json
{
  "repositories": [
    {
      "name": "my-repo",
      "fullName": "your-username/my-repo",
      "description": "A sample repository",
      "isPrivate": false,
      "defaultBranch": "main",
      "htmlUrl": "https://github.com/your-username/my-repo",
      "updatedAtUtc": "2026-03-20T10:00:00Z"
    }
  ],
  "count": 1,
  "syncedAtUtc": "2026-03-25T14:35:00Z"
}
```

### Step 5: Get a specific repository

```bash
curl https://localhost:{port}/api/github/repos/my-repo \
  -H "X-Api-Key: your-api-key"
```

### Step 6: Create an issue

```bash
curl -X POST https://localhost:{port}/api/github/repos/my-repo/issues \
  -H "X-Api-Key: your-api-key" \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Add dark mode support",
    "body": "Users have requested a dark mode option.",
    "labels": ["enhancement"]
  }'
```

```json
{
  "number": 42,
  "title": "Add dark mode support",
  "htmlUrl": "https://github.com/your-username/my-repo/issues/42",
  "state": "open"
}
```

## Configuration Reference

| Config Key | Aspire Parameter | Environment Variable | Required | Default |
| ---------- | ---------------- | -------------------- | -------- | ------- |
| GitHub:ClientId | github-client-id | GitHub__ClientId | Yes | — |
| GitHub:ClientSecret | github-client-secret | GitHub__ClientSecret | Yes | — |
| GitHub:ApiBaseUrl | github-api-base-url | GitHub__ApiBaseUrl | No | `https://api.github.com` |
| GitHub:OAuthBaseUrl | github-oauth-base-url | GitHub__OAuthBaseUrl | No | `https://github.com` |
| GitHub:TokenStorage:TableName | github-token-storage-table-name | GitHub__TokenStorage__TableName | No | `githubtokens` |
| GitHub:RepoStorage:TableName | github-repo-storage-table-name | GitHub__RepoStorage__TableName | No | `githubrepositories` |

## Endpoint Summary

| Method | URL | Auth | Description |
| ------ | --- | ---- | ----------- |
| GET | /api/github/auth | Anonymous | Initiate GitHub OAuth flow |
| GET | /api/github/auth/callback | Anonymous | OAuth callback (receive code) |
| GET | /api/github/profile | Anonymous | Check connection status |
| POST | /api/github/repos/sync | API Key | Sync repositories from GitHub |
| GET | /api/github/repos | API Key | List stored repositories |
| GET | /api/github/repos/{name} | API Key | Get single repository details |
| POST | /api/github/repos/{name}/issues | API Key | Create issue on repository |

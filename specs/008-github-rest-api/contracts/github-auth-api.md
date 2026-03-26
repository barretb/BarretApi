# API Contract: GitHub Authentication

**Feature**: 008-github-rest-api
**Date**: 2026-03-25

## Endpoints

### GET /api/github/auth — Initiate OAuth Flow

Redirects the user to GitHub's authorization page to begin the OAuth flow.

#### Request

No parameters.

#### Response

##### Browser Request (302 Found)

Redirects to GitHub authorization URL:

```
https://github.com/login/oauth/authorize?client_id={clientId}&redirect_uri={callbackUrl}&scope=repo&state={randomState}
```

##### API Request (200 OK)

Returns the authorization URL for the client to handle the redirect.

```json
{
  "authUrl": "https://github.com/login/oauth/authorize?client_id=abc123&redirect_uri=https://localhost:5001/api/github/auth/callback&scope=repo&state=a1b2c3d4"
}
```

---

### GET /api/github/auth/callback — OAuth Callback

Receives the authorization code from GitHub after user consent, exchanges it for an access token, and stores the token.

#### Request

##### Query Parameters

| Parameter | Type | Required | Description |
| --------- | ---- | -------- | ----------- |
| code | string | Conditional | Authorization code from GitHub (present on success) |
| state | string | Yes | CSRF protection state parameter |
| error | string | Conditional | Error code from GitHub (present on failure) |
| error_description | string | No | Human-readable error description |

##### Example Requests

```
GET /api/github/auth/callback?code=abc123def456&state=a1b2c3d4
GET /api/github/auth/callback?error=access_denied&error_description=The+user+denied+access&state=a1b2c3d4
```

#### Response

##### Success (200 OK)

```json
{
  "username": "octocat",
  "status": "connected",
  "scope": "repo"
}
```

##### OAuth Error from GitHub (400 Bad Request)

```json
{
  "statusCode": 400,
  "message": "GitHub authentication failed: The user denied access."
}
```

##### Invalid State Parameter (400 Bad Request)

```json
{
  "statusCode": 400,
  "message": "Invalid OAuth state parameter. Please restart the authentication flow."
}
```

##### Token Exchange Failed (502 Bad Gateway)

```json
{
  "statusCode": 502,
  "message": "GitHub API error: Failed to exchange authorization code for access token."
}
```

---

### GET /api/github/profile — Connection Status

Returns the current GitHub authentication status and connected user information.

#### Request

No parameters.

#### Response

##### Connected (200 OK)

```json
{
  "username": "octocat",
  "connected": true,
  "scope": "repo",
  "connectedAtUtc": "2026-03-25T14:30:00Z"
}
```

##### Not Connected (200 OK)

```json
{
  "username": null,
  "connected": false,
  "scope": null,
  "connectedAtUtc": null
}
```

## Authentication

The OAuth endpoints (`/api/github/auth`, `/api/github/auth/callback`, `/api/github/profile`) are **anonymous** — they do not require the API key. This matches the LinkedIn OAuth endpoint pattern.

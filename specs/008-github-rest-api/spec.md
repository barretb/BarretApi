# Feature Specification: GitHub REST API Integration

**Feature Branch**: `008-github-rest-api`
**Created**: 2026-03-25
**Status**: Draft
**Input**: User description: "I want to begin adding integrations for the Github REST API. The first three pieces that we need to handle are the ability to authenticate and get tokens, the ability to get and store a list of my repositories and their details, and the ability to create a new issue for one of my repositories."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Authenticate with GitHub (Priority: P1)

As the API owner, I want the system to authenticate with GitHub using my credentials so that it can make authorized requests to the GitHub REST API on my behalf. This involves initiating an OAuth flow, receiving a callback with an authorization code, and exchanging it for an access token that is securely stored for subsequent API calls.

**Why this priority**: Authentication is the foundation for all GitHub API interactions. Without a valid access token, no other GitHub feature can function.

**Independent Test**: Can be fully tested by initiating the OAuth flow, completing the callback, and verifying the system stores a valid access token that can be used to call an authenticated GitHub endpoint.

**Acceptance Scenarios**:

1. **Given** the system is configured with valid GitHub OAuth credentials, **When** the user initiates the GitHub authentication flow, **Then** the system redirects to the GitHub authorization page.
2. **Given** the user has authorized the application on GitHub, **When** GitHub redirects back with an authorization code, **Then** the system exchanges the code for an access token and securely stores it.
3. **Given** authentication has completed successfully, **When** the user requests confirmation of their GitHub connection, **Then** the system returns the authenticated GitHub username and account status.
4. **Given** the GitHub OAuth callback receives an invalid or expired authorization code, **When** the system attempts to exchange it, **Then** the system returns a clear error indicating the authentication failed.
5. **Given** a stored access token has expired or been revoked, **When** the system attempts to use it, **Then** the system detects the failure and prompts reauthentication rather than returning a cryptic error.

---

### User Story 2 - Retrieve and Store Repository List (Priority: P2)

As the API owner, I want to retrieve a list of my GitHub repositories and their details so that I can see what repositories I have available and reference them when creating issues or performing other GitHub operations.

The system fetches my repositories from GitHub, stores the relevant details locally, and exposes an endpoint to view the stored repository list. The repository list can be refreshed on demand to pick up newly created or deleted repositories.

**Why this priority**: Knowing which repositories are available is a prerequisite for creating issues (P3) and any future repository-targeted operations. However, it depends on authentication (P1) being in place first.

**Independent Test**: Can be fully tested by triggering a repository sync after authentication and verifying the stored repository data matches the user's actual GitHub repositories.

**Acceptance Scenarios**:

1. **Given** the system has a valid GitHub access token, **When** the user triggers a repository sync, **Then** the system fetches and stores the list of repositories owned by the authenticated user.
2. **Given** repositories have been synced, **When** the user requests the repository list, **Then** the system returns all stored repositories with their key details (name, description, visibility, default branch, URL).
3. **Given** the user has created a new repository on GitHub since the last sync, **When** the user triggers a repository refresh, **Then** the newly created repository appears in the stored list.
4. **Given** repositories have been synced, **When** the user requests a specific repository by name, **Then** the system returns the details for that repository or an appropriate not-found response.
5. **Given** the system has no valid GitHub access token, **When** the user triggers a repository sync, **Then** the system returns an error indicating GitHub authentication is required.

---

### User Story 3 - Create a GitHub Issue (Priority: P3)

As the API owner, I want to create a new issue on one of my GitHub repositories so that I can programmatically track tasks, bugs, or ideas directly from this API without needing to visit the GitHub website.

The user specifies a repository (by name), an issue title, and optionally a body and labels. The system creates the issue via the GitHub API and returns the created issue details including the issue number and URL.

**Why this priority**: Issue creation is the first actionable write operation against GitHub and represents the primary use case beyond read-only access. It depends on both authentication (P1) and having a known repository list (P2).

**Independent Test**: Can be fully tested by creating an issue on a test repository and verifying the issue appears on GitHub with the correct title, body, and labels.

**Acceptance Scenarios**:

1. **Given** the system has a valid GitHub access token and a synced repository list, **When** the user creates an issue with a title and repository name, **Then** the system creates the issue on GitHub and returns the issue number and URL.
2. **Given** the system has a valid GitHub access token, **When** the user creates an issue with a title, body, and labels, **Then** the system creates the issue with all provided details.
3. **Given** the user specifies a repository name that does not exist in the synced list, **When** the user attempts to create an issue, **Then** the system returns a not-found error identifying the invalid repository.
4. **Given** the user does not provide an issue title, **When** the user attempts to create an issue, **Then** the system returns a validation error indicating the title is required.
5. **Given** the GitHub API returns an error (e.g., insufficient permissions on the repository), **When** the system attempts to create the issue, **Then** the system returns a meaningful error describing the problem.

---

### Edge Cases

- What happens when the GitHub API rate limit is exceeded? The system should return a clear error indicating the rate limit has been hit and include the reset time if available from the GitHub response headers.
- What happens when the stored access token is revoked by the user on GitHub? The system should detect the 401 response and indicate that reauthentication is required.
- What happens when the user has hundreds of repositories? The system should handle pagination from the GitHub API and store all repositories, not just the first page.
- What happens when the user attempts to create an issue on a repository they own but have restricted issue tracking on? The system should relay the GitHub API error clearly.
- What happens when the GitHub API is experiencing an outage? The system should return a clear error indicating the upstream service is temporarily unavailable.
- What happens when the OAuth state parameter in the callback does not match the expected value? The system should reject the callback and return an error to prevent CSRF attacks.

## Requirements *(mandatory)*

### Functional Requirements

#### Authentication

- **FR-001**: System MUST expose an endpoint to initiate the GitHub OAuth authorization flow, redirecting the user to GitHub's authorization page.
- **FR-002**: System MUST expose a callback endpoint to receive the authorization code from GitHub after user consent.
- **FR-003**: System MUST exchange the authorization code for an access token and securely store it.
- **FR-004**: System MUST validate the OAuth state parameter on callback to prevent CSRF attacks.
- **FR-005**: System MUST expose an endpoint to verify the current GitHub authentication status, returning the authenticated username.
- **FR-006**: System MUST detect expired or revoked tokens when they are used and return a clear error indicating reauthentication is needed.

#### Repository Management

- **FR-007**: System MUST expose an endpoint to trigger synchronization of the authenticated user's GitHub repositories.
- **FR-008**: System MUST fetch all repositories (handling pagination) owned by the authenticated user from the GitHub API during sync.
- **FR-009**: System MUST store the following details for each repository: name, full name, description, visibility (public/private), default branch, URL, and last updated timestamp.
- **FR-010**: System MUST expose an endpoint to list all stored repositories.
- **FR-011**: System MUST expose an endpoint to get details of a single stored repository by name.
- **FR-012**: System MUST replace the previously stored repository list on each sync to reflect the current state of the user's GitHub account.

#### Issue Creation

- **FR-013**: System MUST expose an endpoint to create a new issue on a specified repository.
- **FR-014**: System MUST require an issue title and repository name when creating an issue.
- **FR-015**: System MUST support an optional issue body and optional list of labels when creating an issue.
- **FR-016**: System MUST return the created issue's number, title, URL, and state after successful creation.
- **FR-017**: System MUST validate that the specified repository exists in the stored repository list before attempting to create the issue.

#### Error Handling

- **FR-018**: System MUST return a clear error when any GitHub operation is attempted without a valid access token.
- **FR-019**: System MUST return a clear error with rate limit reset information when the GitHub API rate limit is exceeded.
- **FR-020**: System MUST return a clear error when the GitHub API is unreachable or returns an unexpected error.

### Key Entities

- **GitHub Connection**: Represents the authenticated link between the system and a GitHub user account. Contains the access token, authenticated username, and connection status. Only one active connection is supported at a time.
- **GitHub Repository**: Represents a repository owned by the authenticated user. Contains the repository name, full name (owner/name), description, visibility (public or private), default branch name, URL, and the timestamp of the last update from GitHub.
- **GitHub Issue**: Represents an issue created on a repository. Contains the issue number, title, body, labels, state, and the URL for viewing the issue on GitHub. Associated with a specific GitHub Repository.

## Assumptions

- The system supports a single GitHub user connection at a time (the API owner's account). Multi-user GitHub authentication is out of scope.
- GitHub OAuth App (not GitHub App) flow is used for authentication, requiring a client ID and client secret configured in the Aspire AppHost.
- The OAuth flow requests the `repo` scope to enable reading private repositories and creating issues.
- Repository sync fetches only repositories owned by the authenticated user, not repositories they have been granted collaborator access to.
- The stored repository list is a snapshot refreshed on demand; it is not kept in real-time sync with GitHub.
- Labels specified when creating an issue are passed as-is to the GitHub API; the system does not validate whether labels exist on the target repository beforehand (GitHub will create them or ignore invalid ones depending on permissions).
- Access tokens are stored using the same persistence mechanism used by the existing application (e.g., Azure Table Storage via Aspire).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user can complete the full GitHub OAuth authentication flow and see their connected GitHub username within 30 seconds.
- **SC-002**: A repository sync retrieves and stores 100% of the authenticated user's owned repositories, including paginated results beyond the first page.
- **SC-003**: A user can view their stored repository list immediately after sync without needing to re-fetch from GitHub.
- **SC-004**: A user can create an issue on any of their synced repositories and receive the issue URL within 5 seconds.
- **SC-005**: All error responses from GitHub operations include a human-readable message that identifies the problem and suggests a resolution (e.g., "reauthenticate", "check repository name", "wait for rate limit reset").
- **SC-006**: The OAuth callback rejects 100% of requests with mismatched state parameters.

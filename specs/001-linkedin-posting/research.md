# Phase 0 Research: LinkedIn Posting Support

## Decision 1: Implement LinkedIn as an `ISocialPlatformClient` adapter

- Decision: Add a new infrastructure client that implements `ISocialPlatformClient` and exposes `PlatformName = "linkedin"`.
- Rationale: Existing post orchestration already depends on this interface and supports multi-platform fan-out without endpoint redesign.
- Alternatives considered:
  - Add LinkedIn logic directly inside `SocialPostService`: rejected because it breaks clean architecture and increases coupling.
  - Create a separate LinkedIn-only endpoint: rejected because requirements call for adding LinkedIn to existing post flow.

## Decision 2: Keep existing endpoint contract and status-code semantics

- Decision: Continue using `POST /api/social-posts` with the same request/response schema, extending accepted `platforms` value set to include `linkedin`.
- Rationale: Minimizes client impact and preserves current API behavior (200 all success, 207 partial success, 502 all failure).
- Alternatives considered:
  - Introduce API versioned endpoint for LinkedIn: rejected because no breaking contract changes are required.
  - Return LinkedIn-specific top-level response fields: rejected to maintain consistent per-platform result model.

## Decision 3: Use OAuth 2.0 access token configuration managed via AppHost

- Decision: Represent LinkedIn credentials/settings in strongly-typed options and source values from AppHost-managed environment/user secrets.
- Rationale: Aligns with constitution requirement for centralized configuration and avoids hardcoded secrets.
- Alternatives considered:
  - Store credentials in non-AppHost project `appsettings.json`: rejected by constitution.
  - Hardcode token values in code: rejected for security reasons.

## Decision 4: Normalize LinkedIn failure mapping to existing result error codes

- Decision: Map LinkedIn HTTP/API failures into existing error taxonomy (`AUTH_FAILED`, `RATE_LIMITED`, `VALIDATION_FAILED`, `PLATFORM_ERROR`, `UNKNOWN_ERROR`).
- Rationale: Preserves downstream handling and keeps endpoint response consistent across platforms.
- Alternatives considered:
  - Introduce LinkedIn-only error code set: rejected because it complicates multi-platform client handling.

## Decision 5: Preserve continue-on-error behavior across platforms

- Decision: LinkedIn failures must not block Bluesky/Mastodon posting attempts in the same request.
- Rationale: Existing service fan-out model is intentionally resilient and required by spec FR-004.
- Alternatives considered:
  - Fail-fast when LinkedIn fails: rejected because it reduces reliability and conflicts with existing behavior.

## Decision 6: Reuse existing image pipeline with LinkedIn-specific limits from `GetConfigurationAsync`

- Decision: Keep image download/upload orchestration in `SocialPostService`; LinkedIn client enforces/advertises platform constraints through `GetConfigurationAsync`.
- Rationale: Avoids duplicating media handling logic and keeps per-platform constraints encapsulated.
- Alternatives considered:
  - Add LinkedIn conditionals in endpoint validator for media limits: rejected because limits are platform-level runtime concerns.

## Decision 7: Add targeted tests at validator/service/endpoint integration boundaries

- Decision: Cover `linkedin` platform acceptance in validator tests, multi-platform partial success in service tests, and endpoint response semantics in API/integration tests.
- Rationale: Captures the most regression-prone seams with minimal test scope expansion.
- Alternatives considered:
  - Only add end-to-end tests: rejected because diagnosing platform-selection regressions would be slower.
  - No new tests: rejected by quality expectations and regression risk.

## Operational Notes

- LinkedIn API permissions/scopes and app registration are prerequisites external to this feature implementation.
- Production rollout requires setting LinkedIn configuration values in AppHost-managed deployment configuration.
- Logging must avoid token disclosure while still preserving platform-level diagnostics.

## Troubleshooting Notes

- If responses return `AUTH_FAILED`, verify `LinkedIn:AccessToken` validity, token expiration, and required LinkedIn app scopes.
- If startup validation fails, verify `LinkedIn:AuthorUrn` uses a supported format (`urn:li:person:*` or `urn:li:organization:*`).
- If requests return `VALIDATION_FAILED`, validate text/media constraints and LinkedIn API payload expectations.
- If requests return `RATE_LIMITED`, implement caller-side backoff and reduce burst concurrency.
- If requests return `PLATFORM_ERROR`, verify outbound connectivity and LinkedIn service availability before retrying.

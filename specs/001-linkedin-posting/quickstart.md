# Quickstart: LinkedIn Posting Support

## 1. Configure LinkedIn settings in AppHost-managed configuration

Set the following keys in AppHost user secrets/environment configuration:

- `LinkedIn:AccessToken`
- `LinkedIn:AuthorUrn`
- `LinkedIn:ApiBaseUrl` (optional override)

Keep existing keys for `Auth`, `Bluesky`, and `Mastodon` unchanged.

## 2. Start the API

Run the normal local workflow for this solution (AppHost or API project) after configuration is present.

## 3. Call the existing social post endpoint with LinkedIn target

- Endpoint: `POST /api/social-posts`
- Required header: `X-Api-Key: <api-key>`

Example request:

```bash
curl -X POST "https://<host>/api/social-posts" \
  -H "X-Api-Key: <api-key>" \
  -H "Content-Type: application/json" \
  -d '{
    "text": "Shipping a new BarretApi update today",
    "hashtags": ["dotnet", "api"],
    "platforms": ["linkedin", "bluesky"]
  }'
```

## 4. Verify response semantics

- `200` when all targeted platforms succeed.
- `207` when at least one platform succeeds and one fails.
- `502` when all targeted platforms fail.

Response body includes one `results[]` item per attempted platform, including LinkedIn entries.

## 5. Validate failure behavior safely

- Temporarily use an invalid LinkedIn token.
- Confirm LinkedIn result has `success=false` with an `errorCode`.
- Confirm other targeted platforms still execute and report independently.

## 6. Production rollout checklist

- Add LinkedIn config values to production AppHost-managed settings.
- Ensure secrets are not logged or exposed in response payloads.
- Run regression checks for Bluesky and Mastodon-only requests to confirm no behavior change.

## 7. Troubleshooting quick checks

- `AUTH_FAILED`: Validate `LinkedIn:AccessToken` and LinkedIn app permissions/scopes.
- `VALIDATION_FAILED`: Confirm payload size/content limits and author URN format.
- `RATE_LIMITED`: Retry with backoff and monitor request frequency.
- `PLATFORM_ERROR`: Validate outbound network connectivity to LinkedIn API endpoints.

## 8. Final verification command checklist

```bash
# Build
dotnet build BarretApi.slnx

# Run API
dotnet run --project src/BarretApi.Api/BarretApi.Api.csproj

# LinkedIn-only smoke request
curl -X POST "https://<host>/api/social-posts" \
  -H "X-Api-Key: <api-key>" \
  -H "Content-Type: application/json" \
  -d '{
    "text": "LinkedIn smoke test post",
    "platforms": ["linkedin"]
  }'

# Mixed-platform resilience check
curl -X POST "https://<host>/api/social-posts" \
  -H "X-Api-Key: <api-key>" \
  -H "Content-Type: application/json" \
  -d '{
    "text": "Mixed platform check",
    "platforms": ["linkedin", "bluesky"]
  }'
```

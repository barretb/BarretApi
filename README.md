# BarretApi

## API Endpoints

### Create social post

- Method: `POST`
- Path: `/api/social-posts`
- Auth header: `X-Api-Key: <value>`

Supported platforms in `platforms`:

- `bluesky`
- `mastodon`
- `linkedin`

### LinkedIn configuration

Configure these values in AppHost-managed configuration for environments that need LinkedIn posting:

- `LinkedIn:AccessToken`
- `LinkedIn:AuthorUrn`
- `LinkedIn:ApiBaseUrl` (optional, defaults to `https://api.linkedin.com`)

Use least-privilege LinkedIn app permissions, rotate access tokens regularly, and never log or return token values in API responses.

### Trigger RSS blog promotion

- Method: `POST`
- Path: `/api/social-posts/rss-promotion`
- Auth header: `X-Api-Key: <value>`

This endpoint checks the configured RSS feed, posts newly published entries first, then posts any eligible reminder entries.

### Production HTTPS requirement

Always call production endpoints using `https://`. Using `http://` may result in redirect behavior where clients retry with the wrong method and receive `405 Method Not Allowed`.

Example:

```bash
curl -X POST "https://<your-api-host>/api/social-posts/rss-promotion" \
  -H "X-Api-Key: <api-key>"
```

### LinkedIn rollout notes

- Add LinkedIn values to deployment settings before enabling LinkedIn in client requests.
- Validate with one `linkedin`-only request first, then test mixed-platform requests.
- Confirm mixed-platform failure handling returns `207` when LinkedIn fails and another platform succeeds.

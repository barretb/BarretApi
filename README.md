# BarretApi

## API Endpoints

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

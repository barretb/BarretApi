# Bluesky AT Protocol API — Research for .NET Implementation

**Researched**: 2026-02-28
**Sources**: Official Bluesky docs (docs.bsky.app), AT Protocol spec (atproto.com), GitHub lexicon schemas

---

## 1. Authentication

### Decision

Use the `com.atproto.server.createSession` endpoint with an **app password** (not the user's primary account password).

### Details

- **Endpoint**: `POST /xrpc/com.atproto.server.createSession`
- **Base URL**: `https://bsky.social` (the entryway/PDS host; should be configurable)
- **Content-Type**: `application/json`

**Request body**:

```json
{
  "identifier": "handle.example.com",
  "password": "xxxx-xxxx-xxxx-xxxx"
}
```

- `identifier` (string, required): The user's handle (e.g., `user.bsky.social`) or DID.
- `password` (string, required): The app password.

**Response body** (required fields):

```json
{
  "accessJwt": "eyJ...",
  "refreshJwt": "eyJ...",
  "handle": "user.bsky.social",
  "did": "did:plc:abc123..."
}
```

- `accessJwt`: Short-lived token (minutes). Used as `Authorization: Bearer <accessJwt>` header on all API calls.
- `refreshJwt`: Longer-lived token. Used only to refresh the session via `com.atproto.server.refreshSession`.
- `did`: The account's DID, required as the `repo` parameter when creating records.
- Additional optional response fields: `email`, `emailConfirmed`, `active`, `status`.

### Auth Flow

1. Call `createSession` with identifier + app password → receive `accessJwt` + `refreshJwt` + `did`.
2. Use `accessJwt` in the `Authorization: Bearer` header for all subsequent requests.
3. When the `accessJwt` expires, call `POST /xrpc/com.atproto.server.refreshSession` with the `refreshJwt` in the Authorization header to obtain a new `accessJwt` + `refreshJwt`.

### App Passwords

- App passwords have the format `xxxx-xxxx-xxxx-xxxx`.
- They grant slightly restricted permissions (cannot delete account or change auth settings).
- Clients don't need to do anything special — app passwords are used exactly like the primary password in `createSession`.
- Best practice: remind users to use app passwords instead of their primary password. Optionally detect the `xxxx-xxxx-xxxx-xxxx` format to warn if a non-app-password is being used.

### Rate Limits for Auth

- `createSession`: 30 per 5 minutes, 300 per day (per account).

### Rationale

The legacy session-based auth (createSession/refreshSession) is the simplest approach for server-to-server bot/API use. OAuth is the newer standard but adds complexity unnecessary for a single-user API utility. App passwords reduce security risk for third-party usage.

### Alternatives

- **OAuth (atproto OAuth)**: The newer recommended auth for multi-user apps/clients. Overkill for a personal utility API with a single configured account.

---

## 2. Creating a Post

### Decision

Use `com.atproto.repo.createRecord` with collection `app.bsky.feed.post`.

### Details

- **Endpoint**: `POST /xrpc/com.atproto.repo.createRecord`
- **Auth**: `Authorization: Bearer <accessJwt>`
- **Content-Type**: `application/json`

**Request body**:

```json
{
  "repo": "did:plc:abc123...",
  "collection": "app.bsky.feed.post",
  "record": {
    "$type": "app.bsky.feed.post",
    "text": "Hello World!",
    "createdAt": "2026-02-28T12:00:00.000Z",
    "langs": ["en"],
    "facets": [],
    "embed": null
  }
}
```

**Required fields in `record`**:

| Field | Type | Description |
|---|---|---|
| `$type` | string | Must be `"app.bsky.feed.post"` |
| `text` | string | Post content. Max 3,000 bytes / 300 graphemes. May be empty if embeds are present. |
| `createdAt` | string (datetime) | ISO 8601 timestamp. Use trailing `Z` format (preferred over `+00:00`). |

**Optional fields in `record`**:

| Field | Type | Description |
|---|---|---|
| `facets` | array of `app.bsky.richtext.facet` | Rich text annotations (links, mentions, hashtags) |
| `embed` | union | Embedded content: images, video, external link card, quote post, or record with media |
| `langs` | array of strings | Language codes (e.g., `["en"]`). Max 3 entries. |
| `labels` | union | Self-label values (content warnings) |
| `tags` | array of strings | Additional hashtags not in text. Max 8 tags, each max 64 graphemes. |
| `reply` | object | Reply reference with `root` and `parent` strong refs |

**Outer request fields**:

| Field | Type | Required | Description |
|---|---|---|---|
| `repo` | string (at-identifier) | Yes | The DID (or handle) of the account |
| `collection` | string (NSID) | Yes | `"app.bsky.feed.post"` |
| `record` | object | Yes | The post record (must contain `$type`) |
| `rkey` | string | No | Custom Record Key (max 512 chars). Auto-generated if omitted. |
| `validate` | boolean | No | Whether to validate against lexicon schema |
| `swapCommit` | string (CID) | No | Compare-and-swap with previous commit |

**Success response** (HTTP 200):

```json
{
  "uri": "at://did:plc:abc123.../app.bsky.feed.post/3k4duaz5vfs2b",
  "cid": "bafyreibjifzpqj6o6wcq3hejh7y4z4z2vmiklkvykc57tw3pcbx3kxifpm"
}
```

- `uri`: The AT URI of the created record.
- `cid`: Content hash (CID) of the record.

### Timestamp Format

Use ISO 8601 with UTC and trailing `Z`:

```csharp
DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
```

---

## 3. Rich Text Facets

### Decision

Use `app.bsky.richtext.facet` objects in the `facets` array. All byte offsets must be computed against the **UTF-8 encoded** byte representation of the text.

### Critical: UTF-8 Byte Offsets

> **WARNING**: Bluesky uses UTF-8 byte offsets, NOT character offsets and NOT UTF-16 code unit offsets. In .NET, `string.Length` gives UTF-16 code units, and `StringInfo` gives grapheme clusters — neither is correct for facet indexing. You MUST convert to UTF-8 bytes first.

```csharp
byte[] utf8Bytes = Encoding.UTF8.GetBytes(text);
// byteStart and byteEnd are indices into this byte array
```

- `byteStart`: Inclusive start index (zero-based) in the UTF-8 byte array.
- `byteEnd`: Exclusive end index in the UTF-8 byte array.
- `byteEnd - byteStart` = length of the annotated substring in UTF-8 bytes.

### Facet JSON Structure

```json
{
  "index": {
    "byteStart": 23,
    "byteEnd": 35
  },
  "features": [
    {
      "$type": "app.bsky.richtext.facet#link",
      "uri": "https://example.com"
    }
  ]
}
```

### Supported Feature Types

#### 3a. Links (`app.bsky.richtext.facet#link`)

```json
{
  "$type": "app.bsky.richtext.facet#link",
  "uri": "https://example.com"
}
```

- `uri` (string, required): The full URL. Note: the field is `uri` (not `url`).
- The `index` points to the substring in the post text that should be rendered as the link.

#### 3b. Mentions (`app.bsky.richtext.facet#mention`)

```json
{
  "$type": "app.bsky.richtext.facet#mention",
  "did": "did:plc:ewvi7nxzyoun6zhxrhs64oiz"
}
```

- `did` (string, required): The DID of the mentioned user (must be resolved from the handle via `com.atproto.identity.resolveHandle`).
- The `index` should span the `@handle` text in the post.

#### 3c. Hashtags (`app.bsky.richtext.facet#tag`)

```json
{
  "$type": "app.bsky.richtext.facet#tag",
  "tag": "csharp"
}
```

- `tag` (string, required): The hashtag text **without** the `#` prefix. Max 640 bytes / 64 graphemes.
- The `index` should span the `#hashtag` text in the post (including the `#` character).

### Complete Hashtag Example

For a post with text `"Hello world #dotnet"`:

```
Text:     H  e  l  l  o     w  o  r  l  d     #  d  o  t  n  e  t
UTF-8:    48 65 6C 6C 6F 20 77 6F 72 6C 64 20 23 64 6F 74 6E 65 74
Index:    0  1  2  3  4  5  6  7  8  9  10 11 12 13 14 15 16 17 18
```

```json
{
  "facets": [
    {
      "index": {
        "byteStart": 12,
        "byteEnd": 19
      },
      "features": [
        {
          "$type": "app.bsky.richtext.facet#tag",
          "tag": "dotnet"
        }
      ]
    }
  ]
}
```

### .NET Implementation Guidance for UTF-8 Byte Offset Calculation

```csharp
public static (int byteStart, int byteEnd) GetUtf8ByteOffsets(string text, int charStart, int charLength)
{
    byte[] utf8 = Encoding.UTF8.GetBytes(text);
    int byteStart = Encoding.UTF8.GetByteCount(text, 0, charStart);
    int byteEnd = byteStart + Encoding.UTF8.GetByteCount(text, charStart, charLength);
    return (byteStart, byteEnd);
}
```

### Rules

- Facets **cannot overlap**.
- Renderers should sort facets by `byteStart` and discard overlapping facets.
- The `features` array can contain multiple decorations on the same range (e.g., a link that is also a mention), but this is uncommon.

---

## 4. Image Uploads

### Decision

Upload blobs first via `com.atproto.repo.uploadBlob`, then reference the returned blob object in the post's `embed` field using `app.bsky.embed.images`.

### Step 1: Upload the Blob

- **Endpoint**: `POST /xrpc/com.atproto.repo.uploadBlob`
- **Auth**: `Authorization: Bearer <accessJwt>`
- **Content-Type**: The image MIME type (e.g., `image/png`, `image/jpeg`, `image/webp`)
- **Body**: Raw image bytes (not JSON, not multipart — raw binary in the request body)

**Response**:

```json
{
  "blob": {
    "$type": "blob",
    "ref": {
      "$link": "bafkreibabalobzn6cd366ukcsjycp4yymjymgfxcv6xczmlgpemzkz3cfa"
    },
    "mimeType": "image/png",
    "size": 760898
  }
}
```

### Step 2: Attach to Post

Include the blob in the `embed` field of the post record:

```json
{
  "$type": "app.bsky.embed.images",
  "images": [
    {
      "alt": "Description of the image for screen readers",
      "image": {
        "$type": "blob",
        "ref": {
          "$link": "bafkreibabalobzn6cd366ukcsjycp4yymjymgfxcv6xczmlgpemzkz3cfa"
        },
        "mimeType": "image/png",
        "size": 760898
      },
      "aspectRatio": {
        "width": 1280,
        "height": 720
      }
    }
  ]
}
```

### Image Constraints (from Lexicon Schema)

| Constraint | Value |
|---|---|
| **Max images per post** | **4** |
| **Max file size per image** | **1,000,000 bytes (1 MB)** (enforced by `app.bsky.embed.images` lexicon) |
| **Accepted MIME types** | `image/*` (any image type; typically `image/jpeg`, `image/png`, `image/webp`) |
| **PDS blob upload max** | 52,428,800 bytes (50 MB) — but the embed lexicon constrains images to 1 MB |
| **Alt text** | Required (`alt` field). Can be empty string but field must be present. |
| **Aspect ratio** | Optional but recommended. Only the ratio matters, not exact dimensions. Omit rather than guess. |

### Image Fields

| Field | Type | Required | Description |
|---|---|---|---|
| `image` | blob | Yes | The blob reference returned by `uploadBlob` |
| `alt` | string | Yes | Alt text for accessibility |
| `aspectRatio` | object | No | `{ "width": int, "height": int }` — the ratio between width and height |

### Important Notes

- Blobs are **deleted** if not referenced from a record within a time window (minutes).
- Upload each image first, collect the blob references, then create the post record referencing all blobs.
- **Strip EXIF metadata** from images before uploading (currently the client's responsibility).
- The server may sniff the content type and return a different MIME type than the `Content-Type` header provided.

---

## 5. Character Limit

### Decision

Bluesky's character limit is **300 grapheme clusters** (not characters, not bytes, not code points).

### Details

From the `app.bsky.feed.post` lexicon:

```json
"text": {
  "type": "string",
  "maxLength": 3000,
  "maxGraphemes": 300
}
```

| Constraint | Value | Meaning |
|---|---|---|
| `maxLength` | 3,000 | Maximum UTF-8 byte length of the text field |
| `maxGraphemes` | 300 | Maximum number of **grapheme clusters** |

### What is a Grapheme Cluster?

A grapheme cluster is what a user perceives as a single "character". Examples:

- `e` = 1 grapheme (1 byte UTF-8)
- `é` = 1 grapheme (2 bytes UTF-8, could be 1 or 2 code points)
- `👨‍❤️‍👨` = 1 grapheme (multiple code points joined by ZWJ, 17+ bytes UTF-8)
- `🇺🇸` = 1 grapheme (2 regional indicator code points, 8 bytes UTF-8)

### .NET Implementation

Use `System.Globalization.StringInfo` to count grapheme clusters:

```csharp
int graphemeCount = StringInfo.GetTextElementEnumerator(text)
    .AsEnumerable()
    .Count();
// Or more simply:
int graphemeCount = new StringInfo(text).LengthInTextElements;
```

### Rationale

The 300 grapheme cluster limit means that emoji-heavy posts and posts with combining characters are counted fairly (one visible character = one unit). The separate 3,000 byte limit prevents abuse with many multi-byte characters. Both limits must be satisfied.

---

## 6. Rate Limiting

### Decision

Implement retry logic with exponential backoff. Monitor `Retry-After` headers and HTTP 429 responses.

### Bluesky Rate Limits

#### Content Write Operations (per account)

- **5,000 points per hour**
- **35,000 points per day**

Point costs:

| Operation | Points |
|---|---|
| CREATE | 3 |
| UPDATE | 2 |
| DELETE | 1 |

This means a maximum of **1,666 creates per hour** or **11,666 creates per day**.

#### HTTP API Requests (PDS)

| Endpoint | Limit | Scope |
|---|---|---|
| All endpoints combined | 3,000 per 5 minutes | Per IP |
| `createSession` | 30 per 5 min / 300 per day | Per account |
| `createAccount` | 100 per 5 minutes | Per IP |

#### Rate Limit Response

- HTTP status: `429 Too Many Requests`
- May include a `Retry-After` header indicating how long to wait.
- Rate limit headers (per IETF draft `ratelimit-headers-02`) are returned on responses for client-side monitoring.

### Rationale

For a personal API utility making occasional posts, these limits are extremely generous and unlikely to be hit. Still, the implementation should handle 429 responses gracefully.

---

## 7. Error Handling

### Decision

All Bluesky API errors follow a standard JSON error response format. Parse the `error` and `message` fields.

### Error Response Format

**Content-Type**: `application/json`

```json
{
  "error": "InvalidRequest",
  "message": "Human-readable description of what went wrong"
}
```

| Field | Type | Required | Description |
|---|---|---|---|
| `error` | string | Yes | Machine-readable error type (ASCII, no whitespace). Maps to error names in the endpoint's lexicon. |
| `message` | string | No | Human-readable description, suitable for display or logging. |

### Common HTTP Status Codes

| Status | Meaning |
|---|---|
| 200 | Success |
| 400 | Bad Request (invalid input, schema validation failure) |
| 401 | Unauthorized (missing/expired auth token) |
| 403 | Forbidden (insufficient permissions) |
| 404 | Not Found |
| 413 | Payload Too Large |
| 429 | Too Many Requests (rate limited) |
| 500 | Internal Server Error |
| 502/503/504 | Temporary service issues |

### Known Error Types (from Lexicons)

- `createSession` errors: `AccountTakedown`, `AuthFactorTokenRequired`
- `refreshSession` errors: `InvalidToken`, `ExpiredToken`
- `createRecord` errors: `InvalidSwap`

### .NET Implementation Guidance

- Always check `response.IsSuccessStatusCode` before parsing the response body.
- On failure, deserialize the body into an error DTO: `{ Error: string, Message: string }`.
- Servers may return non-JSON error pages (e.g., from load balancers). Be robust to HTML error responses.
- Implement retries with exponential backoff for 429, 500, 502, 503, 504.

---

## Summary: Key URLs for .NET Implementation

| Purpose | URL |
|---|---|
| Base PDS host (configurable) | `https://bsky.social` |
| Create session | `POST /xrpc/com.atproto.server.createSession` |
| Refresh session | `POST /xrpc/com.atproto.server.refreshSession` |
| Create record (post) | `POST /xrpc/com.atproto.repo.createRecord` |
| Upload blob (image) | `POST /xrpc/com.atproto.repo.uploadBlob` |
| Resolve handle to DID | `GET /xrpc/com.atproto.identity.resolveHandle?handle=...` |

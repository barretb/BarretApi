# Research: RSS Random Post

**Feature**: 002-rss-random-post
**Date**: 2026-03-04

## Research Questions & Decisions

### 1. Feed Reader: New Interface or Extend Existing?

**Decision**: Add a new overload to `IBlogFeedReader` that accepts a URL parameter.

**Rationale**: The existing `IBlogFeedReader.ReadEntriesAsync()` reads from the configured `BlogPromotionOptions.FeedUrl`. The new feature needs to accept a URL at runtime from the request body. Rather than creating an entirely new interface, we add an overload:

```csharp
Task<IReadOnlyList<BlogFeedEntry>> ReadEntriesAsync(
    string feedUrl,
    CancellationToken cancellationToken = default);
```

The existing parameterless overload continues to delegate to the configured URL. The new overload accepts any URL. Both share the same parsing logic inside `RssBlogFeedReader`.

**Alternatives considered**:

- **New interface `IUrlFeedReader`**: Rejected — would duplicate the feed-reading contract and introduce unnecessary abstraction. The responsibility is the same (read RSS entries); only the URL source differs.
- **Inject URL via a scoped options override**: Rejected — over-engineered for a single parameter; scoped options patterns add DI complexity for no benefit.

### 2. Random Selection Strategy

**Decision**: Use `Random.Shared.Next(eligibleEntries.Count)` to pick an index from the eligible list.

**Rationale**: `Random.Shared` is thread-safe in .NET 6+ and provides adequate randomness for non-cryptographic selection. No need for `RandomNumberGenerator` since the selection has no security implications.

**Alternatives considered**:

- **`RandomNumberGenerator.GetInt32()`**: Rejected — cryptographic randomness is unnecessary overhead for content selection.
- **Shuffle then take first**: Rejected — unnecessary O(n) shuffle when only one element is needed.

### 3. SSRF Prevention for User-Supplied URLs

**Decision**: Validate that the URL uses `http` or `https` scheme, and rely on the existing `HttpClient` configuration (no internal network access from the hosting environment). Add URL validation in the FluentValidation validator.

**Rationale**: The feed URL comes from an authenticated API request (API key required). The risk surface is limited to authenticated callers. Scheme validation (`http`/`https` only) prevents `file://`, `ftp://`, and other protocol abuse. The `HttpClient` used by `RssBlogFeedReader` is configured with standard resilience policies via Aspire.

**Alternatives considered**:

- **DNS resolution check / IP blocklist**: Rejected — adds complexity; the API is already behind authentication and the hosting environment prevents internal network egress.
- **URL allowlist**: Rejected — contradicts the feature purpose of accepting arbitrary feed URLs.

### 4. Post Text Construction Pattern

**Decision**: Follow the existing `BlogPromotionOrchestrator.BuildInitialPostText()` pattern: `"{Title}\n{CanonicalUrl}"`.

**Rationale**: Consistency with the existing promotion endpoint. The title + URL pattern provides readers with enough context and the URL supports link previews on all three platforms. Entry tags (minus excluded tags) are passed as hashtags via the `SocialPost.Hashtags` property, consistent with existing behavior.

**Alternatives considered**:

- **Include summary text**: Rejected — summaries can be long and inconsistent across feeds; title + URL is more reliable and fits all platform character limits.
- **Custom template parameter**: Rejected — out of scope per spec assumptions ("no custom message template is required").

### 5. Hero Image Handling

**Decision**: If the feed entry has a `HeroImageUrl`, pass it as `ImageUrls` on the `SocialPost`, reusing the existing `BuildImageUrls()` pattern from `BlogPromotionOrchestrator`.

**Rationale**: The spec assumes hero image support consistent with existing RSS promotion behavior. The `SocialPostService` already handles image URL downloads and platform-specific uploads.

**Alternatives considered**:

- **Skip images entirely**: Rejected — spec assumption explicitly states hero image support.
- **Download image in the new service**: Rejected — `SocialPostService` already handles `ImageUrls` → download → upload; duplicating that logic would violate the constitution (no duplication of platform-specific publishing logic, SC-006).

### 6. Error Response Shape

**Decision**: Return the same `CreateSocialPostResponse` shape for success/partial-success/failure (200/207/502), plus custom error responses for feed-level failures (422 for no eligible entries, 502 for feed read failure).

**Rationale**: Reusing the existing response shape for platform results maintains API consistency. Feed-level errors (empty feed, all entries filtered out) are distinct from platform posting failures and benefit from a dedicated response with a descriptive message.

After further consideration: use a new `RssRandomPostResponse` that wraps the per-platform results AND includes the selected entry details (`selectedTitle`, `selectedUrl`), satisfying FR-018. Feed-level errors use FastEndpoints' built-in error response mechanism.

**Alternatives considered**:

- **Reuse `CreateSocialPostResponse` directly**: Rejected — it lacks the `selectedTitle`/`selectedUrl` fields required by FR-018.
- **Separate error endpoint**: Rejected — unnecessary; FastEndpoints' `AddError`/`Send.ErrorsAsync` handles validation and domain errors cleanly.

### 7. Service Layer Design

**Decision**: Create `RssRandomPostService` in `BarretApi.Core.Services` with a single `SelectAndPostAsync` method that:

1. Fetches the feed via `IBlogFeedReader.ReadEntriesAsync(url)`
2. Applies tag exclusion filter
3. Applies recency filter
4. Validates at least one entry remains
5. Randomly selects one entry
6. Constructs a `SocialPost` from the entry
7. Delegates to `SocialPostService.PostAsync()`
8. Returns a result containing the selected entry details and per-platform results

**Rationale**: Keeps the endpoint thin (REPR pattern) and the orchestration logic testable in isolation. The service depends on `IBlogFeedReader` and `SocialPostService` — both injectable and mockable.

**Alternatives considered**:

- **Put all logic in the endpoint**: Rejected — violates constitution Principle II (single responsibility) and makes unit testing harder.
- **Extend `BlogPromotionOrchestrator`**: Rejected — that service is tightly coupled to the promotion/tracking workflow. The random-post feature is stateless with fundamentally different behavior.

# Research: RSS Reminder Post Header Update

**Feature**: `005-rss-reminder-header`
**Date**: 2026-03-11

## 1. Current Implementation

### Decision: Location of the change

- **Decision**: Modify the static method `BuildReminderPostText` in `BlogPromotionOrchestrator.cs`
- **Rationale**: This is the sole location where reminder post text is constructed. It is a `private static` method that returns a formatted string. The change is isolated and has no side effects.
- **Alternatives considered**:
  - Making the header configurable via `BlogPromotionOptions`: Rejected per spec — "No new configuration parameters are introduced; the header text is a fixed string."
  - Extracting a shared post-text builder service: Rejected per YAGNI (Constitution Principle VII) — the method is 1 line and used in exactly one place.

### Current format

```csharp
private static string BuildReminderPostText(BlogPostPromotionRecord record)
    => $"Did you miss it earlier? {record.Title}\n{record.CanonicalUrl}";
```

**Output**: `Did you miss it earlier? My Blog Post Title\nhttps://example.com/post`

### Target format

```csharp
private static string BuildReminderPostText(BlogPostPromotionRecord record)
    => $"In case you missed it earlier...\n\n{record.Title}\n{record.CanonicalUrl}";
```

**Output**: `In case you missed it earlier...\n\nMy Blog Post Title\nhttps://example.com/post`

## 2. Test Strategy

### Decision: Add unit tests for `BuildReminderPostText`

- **Decision**: Create a new test class `BlogPromotionOrchestrator_BuildReminderPostText_Tests` in `BarretApi.Core.UnitTests/Services/`. Since `BuildReminderPostText` is `private static`, tests will exercise it through the public `RunAsync` method by setting up a scenario where a reminder post is eligible, then asserting the text passed to the social post service.
- **Rationale**: No existing tests cover `BlogPromotionOrchestrator`. Testing through the public API is preferred over reflection-based access to private methods (Constitution Principle III).
- **Alternatives considered**:
  - Making `BuildReminderPostText` internal and using `[InternalsVisibleTo]`: Rejected — changes access modifier solely for testing, violating encapsulation.
  - Testing via reflection: Rejected — fragile and violates testing best practices.

## 3. Impact on Existing Spec (001-rss-blog-posting)

### Decision: Update the original spec's FR-009

- **Decision**: The original spec `001-rss-blog-posting/spec.md` references FR-009: `System MUST prefix reminder social post text with "Did you miss it earlier?"`. This acceptance criterion will become outdated. The README documentation for the RSS promotion endpoint should also be updated.
- **Rationale**: Keeping specs and docs in sync prevents confusion for future contributors.

## 4. Platform Character Limits

- **Decision**: No special handling needed.
- **Rationale**: The new header ("In case you missed it earlier...\n\n") is 35 characters including newlines, compared to the old prefix ("Did you miss it earlier? ") at 25 characters. The 10-character increase is negligible relative to platform limits (Bluesky 300, Mastodon 500, LinkedIn 3000). The existing `SocialPostService.PostAsync` pipeline already handles text shortening if needed.

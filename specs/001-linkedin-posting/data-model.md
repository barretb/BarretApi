# Data Model: LinkedIn Posting Support

## 1) SocialPostRequest (API input)

Represents one request to publish content to one or more platforms, now including LinkedIn.

### SocialPostRequest Fields

- `Text` (string, optional when images exist): Post body text.
- `Hashtags` (array of string, optional): Additional hashtags to append/process.
- `Platforms` (array of string, optional): Target platform identifiers (`bluesky`, `mastodon`, `linkedin`).
- `Images` (array of `ImageAttachmentRequest`, optional): URL-based image references with alt text.

### SocialPostRequest Validation Rules

- `Text` is required when `Images` is null or empty.
- `Text` length must not exceed 10,000 characters.
- `Platforms` values must be in allowed set: `bluesky | mastodon | linkedin`.
- Maximum of 4 `Images`.
- Each image requires non-empty `Url` and non-empty non-whitespace `AltText`.
- `AltText` length must not exceed 1,500 characters.
- Each hashtag length must not exceed 100 characters and must not contain spaces.

## 2) ImageAttachmentRequest (API input child)

Represents one image URL to download and upload per platform.

### ImageAttachmentRequest Fields

- `Url` (string, required): Source URL for image download.
- `AltText` (string, required): Accessibility text provided by caller.

## 3) LinkedInOptions (configuration entity)

Represents environment-provided LinkedIn authentication and endpoint settings.

### LinkedInOptions Fields

- `SectionName` (const string): Configuration section key (planned: `LinkedIn`).
- `AccessToken` (string, required): OAuth bearer token used for LinkedIn API calls.
- `AuthorUrn` (string, required): LinkedIn author/member URN used in publish payload.
- `ApiBaseUrl` (string, optional): LinkedIn API host/base URL (default official API base).

### LinkedInOptions Validation Rules

- `AccessToken` must be non-empty.
- `AuthorUrn` must be non-empty and in valid LinkedIn URN format.
- `ApiBaseUrl` must be absolute HTTPS URL when supplied.

## 4) PlatformPostResult (core output per platform)

Represents posting outcome for each targeted platform including LinkedIn.

### PlatformPostResult Fields

- `Platform` (string, required): Platform identifier.
- `Success` (bool, required): Indicates success/failure.
- `PostId` (string, optional): Platform-native post identifier.
- `PostUrl` (string, optional): Public post URL when available.
- `PublishedText` (string, optional): Final text used for publish after shortening/hashtag processing.
- `ErrorMessage` (string, optional): Failure detail.
- `ErrorCode` (string, optional): Normalized error classification.

## 5) CreateSocialPostResponse (API output)

Represents endpoint-level aggregate response using existing contract.

### CreateSocialPostResponse Fields

- `Results` (array of `PlatformResult`, required): One result per targeted/resolved platform.
- `PostedAt` (datetime, required): UTC timestamp of response creation.

## Relationships

- One `SocialPostRequest` can target many platforms through `Platforms`.
- One platform target produces at most one `PlatformPostResult` entry in response.
- `CreateSocialPostResponse.Results` contains platform results for all attempted targets.
- `LinkedInOptions` is consumed by LinkedIn platform client only, but result shape is shared with all platforms.

## State Transitions

### Platform execution state per request

- `NotRequested -> Requested` when caller includes platform in `Platforms`.
- `Requested -> Attempted` when platform client is resolved and invoked.
- `Attempted -> Succeeded` when API publish call returns success.
- `Attempted -> Failed` when auth/validation/rate-limit/platform error occurs.

### Endpoint aggregate state

- `AllSucceeded` => HTTP 200 when all `Results.Success == true`.
- `PartialSucceeded` => HTTP 207 when at least one success and at least one failure.
- `AllFailed` => HTTP 502 when all attempted platform results fail.

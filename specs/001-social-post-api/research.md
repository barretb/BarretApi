# Research: Social Media Post API — Technology Decisions

**Branch**: `001-social-post-api` | **Date**: 2026-02-28 | **Plan**: [plan.md](plan.md)

---

## 1. FastEndpoints 7.x REPR Pattern

### 1.1 Structuring Request/Endpoint/Response for the POST Endpoint

**Decision**: Use a single endpoint class `CreateSocialPostEndpoint` following the REPR pattern with `Endpoint<CreateSocialPostRequest, CreateSocialPostResponse>`.

**Rationale (from FastEndpoints docs)**:

- FastEndpoints uses `Endpoint<TRequest, TResponse>` as the base class. The `Configure()` method sets the route and verb; `HandleAsync()` contains the logic.
- The request DTO properties are automatically bound from JSON body, form fields, route params, query params, headers, and claims — in that priority order.
- FluentValidation is handled via a `Validator<TRequest>` class that is auto-discovered and auto-registered (no manual DI registration needed).

```csharp
public sealed class CreateSocialPostEndpoint : Endpoint<CreateSocialPostRequest, CreateSocialPostResponse>
{
    public override void Configure()
    {
        Post("/api/social-posts");
        AllowFileUploads(); // enables multipart/form-data
    }

    public override async Task HandleAsync(CreateSocialPostRequest req, CancellationToken ct)
    {
        // orchestrate posting to platforms
        // return per-platform results
    }
}
```

### 1.2 Handling Both JSON and Multipart/Form-Data in a Single Endpoint

**Decision**: Use **two separate endpoints** — one for JSON (`POST /api/social-posts` with JSON body containing image URLs) and one for multipart/form-data (`POST /api/social-posts/upload` with binary file uploads). Both share the same handler logic via a shared service.

**Rationale**:

- FastEndpoints **can** technically handle both on a single endpoint. Calling `AllowFileUploads()` enables `multipart/form-data`. Without it, JSON is the default. However, the binding behaviors differ:
  - JSON body: properties are deserialized via `System.Text.Json`
  - Form data: properties are bound from form fields; files bind to `IFormFile` properties
- A single endpoint with `AllowFormData()` or `AllowFileUploads()` will accept multipart/form-data, but **JSON requests will be rejected** because the endpoint's accepted content type is changed.
- To accept **both** content types on the same route, you'd need a custom `IRequestBinder<TRequest>` that inspects `Content-Type` and delegates accordingly. This is supported but adds complexity.
- **Practical recommendation**: Use two endpoints with a shared orchestration service. This keeps each endpoint clean, makes Swagger documentation accurate, and avoids custom binder complexity.

**Alternative considered**: A single endpoint with a custom request binder that checks `HasJsonContentType()` and branches. FastEndpoints documents this pattern in the "DIY Request Binding" section. However, this adds maintenance burden and makes OpenAPI documentation harder.

**Final decision for this project**: Since the spec supports both image URLs (JSON) and binary uploads (multipart/form-data) per FR-022 and FR-023, use **two endpoints sharing a common service**:

| Endpoint | Content-Type | Image Source |
|---|---|---|
| `POST /api/social-posts` | `application/json` | Images as URLs in JSON body |
| `POST /api/social-posts/upload` | `multipart/form-data` | Images as binary file uploads |

Both endpoints call the same `ISocialPostOrchestrator` service, which normalizes images to streams before passing to platform clients.

### 1.3 FluentValidation Integration

**Decision**: Use `Validator<TRequest>` base class from FastEndpoints.

**Key findings**:

- FastEndpoints bundles FluentValidation — no separate package install needed.
- Validators inherit `Validator<TRequest>` (not `AbstractValidator<TRequest>`).
- Validators are auto-discovered and registered as **singletons** for performance.
- Invalid requests automatically return `400 Bad Request` with a structured error JSON body.
- Validators should **not** maintain state. Scoped dependencies can be resolved via `Resolve<T>()`.
- For business logic validation inside the handler, use `AddError()` / `ThrowIfAnyErrors()` / `ThrowError()`.

```csharp
public sealed class CreateSocialPostValidator : Validator<CreateSocialPostRequest>
{
    public CreateSocialPostValidator()
    {
        RuleFor(x => x.Text)
            .NotEmpty()
            .When(x => x.Images is null || x.Images.Count == 0)
            .WithMessage("Post text is required when no images are attached.");

        RuleForEach(x => x.Images).ChildRules(image =>
        {
            image.RuleFor(i => i.AltText)
                .NotEmpty()
                .WithMessage("Alt text is required for every image.");
        });
    }
}
```

### 1.4 Partial Success HTTP Status Code

**Decision**: Return **HTTP 200 OK** (or 207) with per-platform status in the response body. Recommend **HTTP 207 Multi-Status** for clarity.

**Analysis**:

| Option | Pros | Cons |
|---|---|---|
| **200 OK** with per-platform results | Simple; well-understood; clients parse body | Misleading — implies full success when some platforms failed |
| **207 Multi-Status** (WebDAV, RFC 4918) | Semantically correct for mixed outcomes; widely recognized pattern | Not a standard REST status code; some HTTP clients may not expect it |
| **202 Accepted** | Good for async workflows | Not applicable — our API is synchronous |
| **200 OK** with `X-Partial-Success` header | Keeps 200 while hinting at partial failure | Non-standard header; easy to miss |

**Recommendation**: Use **HTTP 207 Multi-Status** when at least one platform fails and at least one succeeds. Use **HTTP 200 OK** when all targeted platforms succeed. Use **HTTP 502 Bad Gateway** (or 500) when all platforms fail.

**Rationale**: 207 is the most semantically correct choice for partial success. It's used by Microsoft Graph API, CalDAV/CardDAV, and other batch-operation APIs. Clients that don't understand 207 will treat it as a 2xx success, which is acceptable behavior.

```csharp
// In the endpoint handler:
if (results.All(r => r.IsSuccess))
    await Send.OkAsync(response, ct);
else if (results.Any(r => r.IsSuccess))
    await Send.CustomAsync(response, statusCode: 207, ct);
else
    await Send.CustomAsync(response, statusCode: 502, ct);
```

Response body structure (same for 200 and 207):

```json
{
    "text": "Hello world!",
    "results": [
        {
            "platform": "bluesky",
            "success": true,
            "postId": "at://did:plc:abc123/app.bsky.feed.post/xyz",
            "postUri": "https://bsky.app/profile/...",
            "error": null
        },
        {
            "platform": "mastodon",
            "success": false,
            "postId": null,
            "postUri": null,
            "error": "Authentication failed: Invalid access token"
        }
    ]
}
```

---

## 2. HttpClientFactory with Polly Retry

### 2.1 Microsoft.Extensions.Http.Resilience (the Modern Approach)

**Decision**: Use `Microsoft.Extensions.Http.Resilience` with `AddStandardResilienceHandler()` from the Aspire ServiceDefaults project. Customize retry settings per platform client.

**Key findings from Microsoft docs**:

- `Microsoft.Extensions.Http.Resilience` is the official .NET library for HTTP resilience, built on top of Polly v8+.
- It replaces the older `Microsoft.Extensions.Http.Polly` package (which used Polly v7).
- **Aspire ServiceDefaults already includes it** — the `AddServiceDefaults()` method calls `ConfigureHttpClientDefaults` which adds `AddStandardResilienceHandler()` to ALL `HttpClient` instances.

### 2.2 Standard Resilience Handler Defaults (Already in Aspire)

The standard resilience handler chains **five strategies** in order:

| # | Strategy | Default |
|---|---|---|
| 1 | Rate limiter | 1,000 concurrent permits |
| 2 | Total timeout | 30 seconds |
| 3 | Retry | 3 retries, exponential backoff, 2s base delay, jitter enabled |
| 4 | Circuit breaker | 10% failure ratio, 100 min throughput, 30s sampling, 5s break |
| 5 | Attempt timeout | 10 seconds per attempt |

**Handled status codes**: HTTP 500+, 408 (Request Timeout), 429 (Too Many Requests).
**Handled exceptions**: `HttpRequestException`, `TimeoutRejectedException`.

**This already satisfies FR-025** (retry transient errors with exponential backoff) with no additional code needed beyond what Aspire ServiceDefaults provides.

### 2.3 Customizing Retry for Platform Clients

**Decision**: Use named `HttpClient` instances registered via `AddHttpClient<T>()`. Customize retry settings if the defaults don't match requirements.

```csharp
// In the API project's DI registration:
builder.Services.AddHttpClient<BlueskyClient>(client =>
{
    // BaseAddress will be set via Aspire service discovery or configuration
})
.AddStandardResilienceHandler(options =>
{
    // Customize retry: FR-025 says configurable count + initial delay
    options.Retry.MaxRetryAttempts = 3;          // default is already 3
    options.Retry.Delay = TimeSpan.FromSeconds(1); // spec says ~1 second initial
    options.Retry.BackoffType = DelayBackoffType.Exponential;
    options.Retry.UseJitter = true;
});
```

**Important**: If using Aspire ServiceDefaults (which applies `AddStandardResilienceHandler()` to ALL HttpClients via `ConfigureHttpClientDefaults`), adding a second resilience handler per-client would **stack** handlers. To avoid this, either:

1. **Option A**: Rely on the global default from ServiceDefaults (simplest — defaults already match A-009).
2. **Option B**: Use `RemoveAllResilienceHandlers()` on the specific client builder, then add a custom handler.

**Recommendation**: Option A — rely on Aspire's built-in standard resilience handler. The defaults (3 retries, exponential backoff with ~2s base + jitter, 30s total timeout) align closely with A-009 (3 retries, ~1s initial delay). If the 2s vs 1s base delay matters, customize via:

```csharp
builder.Services.Configure<HttpStandardResilienceOptions>(
    "BlueskyClient",  // named options matching the HttpClient name
    options =>
    {
        options.Retry.Delay = TimeSpan.FromSeconds(1);
    });
```

### 2.4 Aspire Built-In Resilience

**Yes, Aspire provides built-in resilience** via the ServiceDefaults project:

```csharp
// From Aspire ServiceDefaults Extensions.cs:
builder.Services.ConfigureHttpClientDefaults(http =>
{
    http.AddStandardResilienceHandler();  // <-- Polly-based resilience for ALL HttpClients
    http.AddServiceDiscovery();
});
```

This means:

- Every `HttpClient` created via `IHttpClientFactory` automatically gets retry, circuit breaker, rate limiting, and timeout.
- No additional NuGet packages or configuration needed in the API project.
- The `Microsoft.Extensions.Http.Resilience` package is already referenced by the ServiceDefaults project.

### 2.5 Making Retry Settings Configurable via Aspire AppHost

Per FR-025, retry settings should be configurable. Use Aspire parameters passed as environment variables:

```csharp
// AppHost Program.cs:
var retryCount = builder.AddParameter("retry-max-attempts");
var retryDelay = builder.AddParameter("retry-initial-delay-ms");

builder.AddProject<Projects.BarretApi_Api>("api")
    .WithEnvironment("Resilience__Retry__MaxRetryAttempts", retryCount)
    .WithEnvironment("Resilience__Retry__InitialDelayMs", retryDelay);
```

```csharp
// API project — bind to options:
builder.Services.Configure<HttpStandardResilienceOptions>(options =>
{
    var config = builder.Configuration.GetSection("Resilience:Retry");
    var maxRetries = config.GetValue<int?>("MaxRetryAttempts");
    var delayMs = config.GetValue<int?>("InitialDelayMs");

    if (maxRetries.HasValue)
        options.Retry.MaxRetryAttempts = maxRetries.Value;
    if (delayMs.HasValue)
        options.Retry.Delay = TimeSpan.FromMilliseconds(delayMs.Value);
});
```

---

## 3. API Key Authentication in ASP.NET Core

### 3.1 Approach: Custom AuthenticationHandler

**Decision**: Implement API key authentication using a custom `AuthenticationHandler<AuthenticationSchemeOptions>`. This is the recommended FastEndpoints approach per the official example gist.

**Rationale**:

- FastEndpoints' security model builds on ASP.NET Core's standard authentication middleware.
- The official FastEndpoints API key auth example uses `AuthenticationHandler<AuthenticationSchemeOptions>`.
- This integrates naturally with FastEndpoints' endpoint-level auth configuration — endpoints are **secure by default** and require explicit `AllowAnonymous()` to opt out.
- Middleware-based approaches (checking the key in raw middleware) bypass ASP.NET Core's auth pipeline, losing features like `[Authorize]` attribute support, claims-based identity, and proper 401 challenge responses.

### 3.2 Implementation Pattern

Based on the official FastEndpoints gist (updated for .NET 8+/10):

```csharp
public sealed class ApiKeyAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IConfiguration config)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    internal const string SchemeName = "ApiKey";
    internal const string HeaderName = "X-Api-Key";

    private readonly string _apiKey = config["Auth:ApiKey"]
        ?? throw new InvalidOperationException("API key not configured");

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var extractedApiKey))
            return Task.FromResult(AuthenticateResult.NoResult());

        if (!extractedApiKey.Equals(_apiKey))
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key"));

        var identity = new ClaimsIdentity(
            claims: [new Claim("ClientID", "Owner")],
            authenticationType: Scheme.Name);
        var principal = new GenericPrincipal(identity, roles: null);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
```

### 3.3 DI Registration

```csharp
// Program.cs
bld.Services
    .AddFastEndpoints()
    .AddAuthorization()
    .AddAuthentication(ApiKeyAuthHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthHandler>(
        ApiKeyAuthHandler.SchemeName, null);

var app = bld.Build();
app.UseAuthentication()    // must come before UseAuthorization
    .UseAuthorization()     // must come before UseFastEndpoints
    .UseFastEndpoints();
```

### 3.4 API Key Storage

Per the project constitution (A-008): The API key is stored in **Aspire AppHost User Secrets** and passed to the API project via configuration/environment variable:

```csharp
// AppHost:
var apiKey = builder.AddParameter("api-key", secret: true);
builder.AddProject<Projects.BarretApi_Api>("api")
    .WithEnvironment("Auth__ApiKey", apiKey);
```

```json
// AppHost appsettings.json or User Secrets:
{
    "Parameters": {
        "api-key": "your-secret-key-here"
    }
}
```

The API project reads `Auth:ApiKey` from its configuration (populated by the environment variable `Auth__ApiKey` that Aspire injects).

---

## 4. Multipart Form-Data / File Uploads in FastEndpoints

### 4.1 Enabling File Uploads

**Decision**: Use `AllowFileUploads()` in the endpoint's `Configure()` method.

**Key findings**:

- By default, FastEndpoints endpoints **do not accept** `multipart/form-data`. You must explicitly enable it.
- `AllowFileUploads()` enables multipart/form-data and exposes files via `Files` property (`IFormFileCollection`).
- Files can also be bound directly to DTO properties of type `IFormFile`, `IEnumerable<IFormFile>`, `List<IFormFile>`, or `IFormFileCollection`.
- Form fields (non-file data) are automatically bound to the DTO alongside files.
- There's also an `[AllowFileUploads]` attribute equivalent.

### 4.2 Request DTO with File Binding

```csharp
public sealed class CreateSocialPostUploadRequest
{
    public string? Text { get; set; }
    public List<string>? Hashtags { get; set; }
    public List<string>? Platforms { get; set; }

    // Files bound from multipart form fields
    public IFormFileCollection? Images { get; set; }

    // Alt text provided as parallel form fields: AltTexts[0], AltTexts[1], etc.
    public List<string>? AltTexts { get; set; }
}
```

### 4.3 Nested Complex Form Data

FastEndpoints supports deeply nested complex form data binding with the `[FromForm]` attribute:

```csharp
public sealed class CreateSocialPostUploadRequest
{
    public string? Text { get; set; }
    public List<string>? Hashtags { get; set; }
    public List<string>? Platforms { get; set; }

    [FromForm]
    public List<ImageUpload> Images { get; set; } = [];
}

public sealed class ImageUpload
{
    public IFormFile File { get; set; } = null!;
    public string AltText { get; set; } = string.Empty;
}
```

With curl:

```bash
curl -X POST http://localhost:5000/api/social-posts/upload \
  --form 'Text="Hello world"' \
  --form 'Hashtags="dotnet"' \
  --form 'Hashtags="webapi"' \
  --form 'Images[0].File=@"/photo1.jpg"' \
  --form 'Images[0].AltText="A sunset photo"' \
  --form 'Images[1].File=@"/photo2.jpg"' \
  --form 'Images[1].AltText="A mountain landscape"'
```

### 4.4 Large File Considerations

- ASP.NET Core buffers uploaded files fully into memory or disk by default when using `IFormFile`.
- For the social media use case, images are limited to 8 MB max (Mastodon limit), so full buffering is acceptable.
- For larger files, `AllowFileUploads(dontAutoBindFormData: true)` + streaming via `FormFileSectionsAsync()` is available but overkill here.
- Kestrel's max request body size may need increasing: `MaxRequestBodySize(50 * 1024 * 1024)` in endpoint config if needed.

### 4.5 IFormFile Best Practices

- **Never persist `IFormFile` data** — read the stream promptly and pass to platform clients. This aligns with FR-021 (fire-and-forget, no local persistence).
- **Validate file size early** via FluentValidation before processing.
- **Validate content type** (MIME type) and optionally file magic bytes for security (FR-015: JPEG, PNG, GIF, WebP only).
- **Use `file.OpenReadStream()`** to get a `Stream` for uploading to platform APIs.
- Do not trust `file.FileName` from the client; use it only for metadata if needed.

---

## 5. Aspire 13 Service Configuration

### 5.1 Configuring External HTTP Services

**Decision**: Use Aspire's `AddParameter()` for configuration values and `WithEnvironment()` to pass them to the API project. No Aspire resource type exists for arbitrary external HTTP APIs (Bluesky/Mastodon are not Aspire-managed resources).

**Pattern for external service URLs**:

```csharp
// AppHost Program.cs:
var blueskyUrl = builder.AddParameter("bluesky-url");
var mastodonUrl = builder.AddParameter("mastodon-url");
var mastodonCharLimit = builder.AddParameter("mastodon-char-limit");

var apiKey = builder.AddParameter("api-key", secret: true);
var blueskyHandle = builder.AddParameter("bluesky-handle");
var blueskyAppPassword = builder.AddParameter("bluesky-app-password", secret: true);
var mastodonAccessToken = builder.AddParameter("mastodon-access-token", secret: true);

builder.AddProject<Projects.BarretApi_Api>("api")
    .WithEnvironment("Auth__ApiKey", apiKey)
    .WithEnvironment("Bluesky__BaseUrl", blueskyUrl)
    .WithEnvironment("Bluesky__Handle", blueskyHandle)
    .WithEnvironment("Bluesky__AppPassword", blueskyAppPassword)
    .WithEnvironment("Mastodon__BaseUrl", mastodonUrl)
    .WithEnvironment("Mastodon__AccessToken", mastodonAccessToken)
    .WithEnvironment("Mastodon__CharacterLimit", mastodonCharLimit);
```

```json
// AppHost appsettings.json (non-secret defaults):
{
    "Parameters": {
        "bluesky-url": "https://bsky.social",
        "mastodon-url": "https://mastodon.social",
        "mastodon-char-limit": "500"
    }
}
```

Secret values go in AppHost User Secrets:

```json
{
    "Parameters": {
        "api-key": "my-dev-api-key",
        "bluesky-handle": "user.bsky.social",
        "bluesky-app-password": "xxxx-xxxx-xxxx-xxxx",
        "mastodon-access-token": "your-mastodon-token"
    }
}
```

### 5.2 Consuming Configuration in the API Project

Use strongly-typed options classes with `IOptions<T>` (per the constitution):

```csharp
// In BarretApi.Core:
public sealed class BlueskyOptions
{
    public string BaseUrl { get; set; } = "https://bsky.social";
    public string Handle { get; set; } = string.Empty;
    public string AppPassword { get; set; } = string.Empty;
}

public sealed class MastodonOptions
{
    public string BaseUrl { get; set; } = "https://mastodon.social";
    public string AccessToken { get; set; } = string.Empty;
    public int CharacterLimit { get; set; } = 500;
}
```

```csharp
// In API project Program.cs:
builder.Services.Configure<BlueskyOptions>(builder.Configuration.GetSection("Bluesky"));
builder.Services.Configure<MastodonOptions>(builder.Configuration.GetSection("Mastodon"));
```

The environment variables injected by Aspire (e.g., `Bluesky__BaseUrl`) automatically bind to the configuration hierarchy via the standard .NET configuration system (`__` maps to `:` in config keys).

### 5.3 Aspire Parameter Resolution Order

1. **Environment variables** (`Parameters__*` syntax)
2. **Configuration files** (appsettings.json, User Secrets)
3. **User prompts** (Aspire dashboard prompts for missing values)

### 5.4 Service Defaults Integration

The API project references the `BarretApi.ServiceDefaults` project and calls `builder.AddServiceDefaults()`, which provides:

- OpenTelemetry metrics and tracing
- Health check endpoints (`/health`, `/alive`)
- Service discovery
- **Standard resilience handler on ALL HttpClients** (retry, circuit breaker, rate limiter, timeouts)

No additional resilience configuration is needed unless overriding defaults for specific clients.

---

## Summary of Decisions

| # | Topic | Decision | Key Rationale |
|---|---|---|---|
| 1a | Endpoint structure | REPR pattern with `Endpoint<TRequest, TResponse>` | FastEndpoints standard; auto-binding; auto-validation |
| 1b | JSON + multipart | Two endpoints, shared service | Avoids custom binder complexity; clean Swagger docs |
| 1c | Validation | `Validator<TRequest>` (auto-discovered) | Built into FastEndpoints; singleton performance |
| 1d | Partial success | 207 Multi-Status / 200 OK / 502 | Semantically correct; consistent with batch APIs |
| 2a | HTTP resilience | `Microsoft.Extensions.Http.Resilience` via Aspire ServiceDefaults | Already included; standard handler covers retry + circuit breaker |
| 2b | Retry config | Aspire defaults (3 retries, exponential, jitter) | Matches A-009; configurable via AppHost parameters |
| 3a | API key auth | Custom `AuthenticationHandler` | Official FastEndpoints pattern; integrates with ASP.NET auth pipeline |
| 3b | Key storage | Aspire AppHost User Secrets → environment variable | Per constitution; no secrets in source |
| 4a | File uploads | `AllowFileUploads()` + `IFormFile`/`IFormFileCollection` binding | Built-in FastEndpoints support; auto-binding to DTO |
| 4b | Form data | Nested complex binding with `[FromForm]` | Pairs files with alt text naturally |
| 5a | External services | `AddParameter()` + `WithEnvironment()` in AppHost | Standard Aspire pattern; no Aspire resource for external APIs |
| 5b | Config consumption | `IOptions<T>` with strongly-typed classes | Per constitution; config only in AppHost |
| 5c | Resilience defaults | Aspire ServiceDefaults provides it globally | Zero additional config for standard retry behavior |

---

## Sources

- [FastEndpoints: Get Started](https://fast-endpoints.com/docs/get-started)
- [FastEndpoints: Model Binding](https://fast-endpoints.com/docs/model-binding) — JSON, form fields, `[FromForm]`, nested complex data
- [FastEndpoints: Validation](https://fast-endpoints.com/docs/validation) — `Validator<T>`, FluentValidation integration
- [FastEndpoints: File Handling](https://fast-endpoints.com/docs/file-handling) — `AllowFileUploads()`, `IFormFile` binding, large files
- [FastEndpoints: Security](https://fast-endpoints.com/docs/security) — Auth schemes, custom handlers
- [FastEndpoints: API Key Auth Gist](https://gist.github.com/dj-nitehawk/4efe5ef70f813aec2c55fff3bbb833c0) — Complete ApiKey AuthenticationHandler example
- [Microsoft: Build resilient HTTP apps](https://learn.microsoft.com/en-us/dotnet/core/resilience/http-resilience) — `Microsoft.Extensions.Http.Resilience`, standard handler, custom pipelines
- [Aspire: Service Defaults](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/service-defaults) — `AddServiceDefaults()`, built-in resilience
- [Aspire: External Parameters](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/external-parameters) — `AddParameter()`, secrets, environment variables
- [RFC 4918 §13](https://www.rfc-editor.org/rfc/rfc4918#section-13) — HTTP 207 Multi-Status

---

## 6. Bluesky AT Protocol API

### 6.1 Authentication

**Decision**: Use app password authentication via `POST /xrpc/com.atproto.server.createSession`.

**Rationale**: Bluesky uses the AT Protocol; app passwords are the recommended approach for API integrations (avoids exposing the main account password).

**Alternatives considered**: OAuth2 DPoP (AT Protocol OAuth is emerging but not yet stable for server-to-server use).

**Flow**:

1. Call `POST /xrpc/com.atproto.server.createSession` with `{ "identifier": "<handle-or-did>", "password": "<app-password>" }`
2. Response returns `accessJwt` (short-lived) and `refreshJwt` (long-lived)
3. All subsequent calls use `Authorization: Bearer <accessJwt>`
4. When `accessJwt` expires, call `POST /xrpc/com.atproto.server.refreshSession` with `refreshJwt` to obtain a new pair

**Configuration**: Store handle and app password in Aspire AppHost User Secrets. The service should authenticate on first use and cache/refresh tokens in memory.

### 6.2 Creating Posts

**Decision**: Use `POST /xrpc/com.atproto.repo.createRecord` with collection `app.bsky.feed.post`.

**Request body**:

```json
{
  "repo": "<user-did>",
  "collection": "app.bsky.feed.post",
  "record": {
    "$type": "app.bsky.feed.post",
    "text": "Hello world",
    "createdAt": "2026-02-28T12:00:00.000Z",
    "facets": [],
    "embed": {}
  }
}
```

**Response**: Returns `{ "uri": "at://...", "cid": "bafyrei..." }` — the `uri` is the post identifier.

### 6.3 Character Limits

**Decision**: Enforce **300 grapheme clusters** AND **3,000 UTF-8 bytes** (both must be satisfied).

**Rationale**: Bluesky uses grapheme cluster counting, not char/codepoint counting.

**Alternatives considered**: Simple `string.Length` — incorrect; would miscount emoji and combining characters.

**.NET implementation**: Use `System.Globalization.StringInfo.LengthInTextElements` for grapheme cluster counting and `Encoding.UTF8.GetByteCount()` for byte length validation.

### 6.4 Rich Text Facets

**Decision**: Build facets with **UTF-8 byte offsets** for hashtags, links, and mentions.

**Rationale**: Bluesky requires explicit facet annotations for any rich text; plain `#tag` text is NOT automatically linked.

**Critical**: Facet byte indices are UTF-8 byte offsets, NOT character indices. Use `Encoding.UTF8.GetByteCount(text[..charIndex])` to convert character positions to byte offsets.

**Facet types**:

| Feature | Type URI | Index fields |
|---------|----------|-------------|
| Link | `app.bsky.richtext.facet#link` | `uri` |
| Mention | `app.bsky.richtext.facet#mention` | `did` |
| Hashtag | `app.bsky.richtext.facet#tag` | `tag` (without `#` prefix) |

**Facet structure**:

```json
{
  "index": { "byteStart": 6, "byteEnd": 13 },
  "features": [{ "$type": "app.bsky.richtext.facet#tag", "tag": "dotnet" }]
}
```

### 6.5 Image Uploads

**Decision**: Upload via `POST /xrpc/com.atproto.repo.uploadBlob`, then embed in the post record.

**Upload**: `POST /xrpc/com.atproto.repo.uploadBlob` with `Content-Type` set to the image MIME type and raw binary body. Returns `{ "blob": { "$type": "blob", "ref": { "$link": "..." }, "mimeType": "...", "size": ... } }`.

**Embed in post**:

```json
{
  "$type": "app.bsky.embed.images",
  "images": [
    {
      "alt": "Description of image",
      "image": { "$type": "blob", "ref": { "$link": "..." }, "mimeType": "image/jpeg", "size": 12345 }
    }
  ]
}
```

**Limits**: Max **4 images** per post. Max **1 MB** per image file.

### 6.6 Rate Limits

| Metric | Limit |
|--------|-------|
| Points per hour | 5,000 |
| Points per day | 35,000 |
| CREATE action cost | 3 points |
| Requests per 5 min per IP | 3,000 |

Rate limit exceeded returns HTTP 429 with `RateLimit-Limit`, `RateLimit-Remaining`, `RateLimit-Reset` headers.

### 6.7 Error Handling

Errors return JSON: `{ "error": "ErrorCode", "message": "Human-readable description" }`. Common codes: `InvalidToken`, `ExpiredToken`, `InvalidRequest`, `RateLimitExceeded`.

---

## 7. Mastodon API

### 7.1 Authentication

**Decision**: Use a pre-generated user access token (Bearer token).

**Rationale**: For single-user personal API, generating a token via the web UI (Preferences > Development > New Application) is sufficient; no need for full OAuth2 authorization code flow.

**Alternatives considered**: Full OAuth2 flow (`POST /api/v1/apps` → authorize → exchange code for token) — unnecessary for single user.

**Required scopes**: `write:statuses`, `write:media`.

**Token passing**: `Authorization: Bearer <user_token>` on every request.

**Token lifetime**: Tokens do not expire automatically; valid until deleted by user or revoked by app.

### 7.2 Creating Posts

**Decision**: Use `POST {instanceUrl}/api/v1/statuses`.

**Request** (form data):

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `status` | String | Yes (unless `media_ids` provided) | Text content |
| `media_ids[]` | Array\<String\> | No | Attachment IDs from prior upload |
| `visibility` | String | No | `public` / `unlisted` / `private` / `direct` |
| `language` | String | No | ISO 639-1 code |

**Default visibility**: `public`, configurable per request.

**Idempotency**: Send `Idempotency-Key` header (arbitrary string) to prevent duplicate posts; stored by Mastodon for 1 hour.

**Response**: Returns a `Status` entity (JSON) with `id`, `url`, `content`, `created_at`, etc.

### 7.3 Character Limits

**Decision**: Query `GET {instanceUrl}/api/v2/instance` at startup to get actual limits; do NOT hardcode 500.

**Rationale**: Limits vary by instance; some forks allow 5,000+ characters.

**URL counting**: URLs count as **23 characters** regardless of actual length (`characters_reserved_per_url: 23`).

**Relevant instance config fields**:

```json
{
  "configuration": {
    "statuses": {
      "max_characters": 500,
      "max_media_attachments": 4,
      "characters_reserved_per_url": 23
    },
    "media_attachments": {
      "description_limit": 1500,
      "image_size_limit": 16777216,
      "supported_mime_types": ["image/jpeg", "image/png", "..."]
    }
  }
}
```

### 7.4 Hashtags

**Decision**: Include hashtags as plain text in the status body — Mastodon auto-detects `#tag` and makes them clickable.

**Rationale**: No facet/annotation mechanism exists in the Mastodon posting API; fundamentally different from Bluesky.

### 7.5 Image Uploads

**Decision**: Upload via `POST {instanceUrl}/api/v2/media` (v1 endpoint is deprecated), then attach IDs to the status.

**Upload request** (`multipart/form-data`):

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `file` | File | Yes | Image data with MIME type |
| `description` | String | No | Alt text (max 1,500 chars) |

**Response**: Returns `MediaAttachment` entity with `id`, `url`, `type`, etc.

- HTTP 200: Processed synchronously (typical for images)
- HTTP 202: Async processing (video/audio); poll `GET /api/v1/media/:id`

**Limits**:

| Metric | Default (mastodon.social) |
|--------|--------------------------|
| Image file size | **16 MB** |
| Max images per status | **4** |
| Alt text length | **1,500 characters** |
| Supported image formats | JPEG, PNG, GIF, WebP, HEIC, HEIF, AVIF |

**Spec update note**: A-002 states Mastodon image limit is ~8 MB. The actual mastodon.social default is **16 MB** as of Mastodon 4.x. Consider querying dynamically via `GET /api/v2/instance`.

### 7.6 Rate Limits

| Scope | Limit | Window |
|-------|-------|--------|
| Per account (general) | 300 requests | 5 minutes |
| Per IP (general) | 300 requests | 5 minutes |
| Media upload | 30 requests | 30 minutes |

**Headers**: `X-RateLimit-Limit`, `X-RateLimit-Remaining`, `X-RateLimit-Reset` (ISO 8601).

When exceeded: HTTP 429 Too Many Requests.

### 7.7 Error Handling

Errors return JSON: `{ "error": "message", "error_description": "optional details" }`.

| Code | Meaning |
|------|---------|
| 400 | Bad request / missing parameter |
| 401 | Invalid access token |
| 403 | Action not allowed |
| 422 | Validation failed (text blank, invalid file type) |
| 429 | Rate limit exceeded |
| 5xx | Server error (transient, eligible for retry) |

### 7.8 Instance URL

**Decision**: Base URL is configurable per deployment (e.g., `https://mastodon.social`, `https://fosstodon.org`).

**Configuration**: Stored in Aspire AppHost as a named parameter.

---

## 8. Cross-Cutting Decisions

### 8.1 Platform Abstraction

**Decision**: Define `ISocialPlatformClient` interface in Core with methods `PostAsync()` and `UploadImageAsync()`; implement per-platform in Infrastructure.

**Rationale**: Constitution Principle II — interfaces in Core, implementations in Infrastructure; allows testing with NSubstitute mocks.

### 8.2 Text Shortening Strategy

**Decision**: Shorten per-platform using each platform's actual character limit; truncate at word boundary; append "…" (single Unicode ellipsis character U+2026); preserve trailing hashtags by removing them last.

**Rationale**: Spec FR-005 through FR-007; using Unicode ellipsis rather than "..." saves 2 characters.

**Bluesky-specific**: Use `StringInfo.LengthInTextElements` for grapheme cluster counting. Also validate `Encoding.UTF8.GetByteCount()` does not exceed 3,000 bytes.

**Mastodon-specific**: URLs count as 23 characters regardless of length; query instance config for actual `max_characters`.

### 8.3 Hashtag Processing

**Decision**: Parse inline hashtags from text (regex `#\w+`); merge with separate list; de-duplicate (case-insensitive); auto-prefix with `#`; for Bluesky, generate facets for each hashtag.

**Rationale**: Spec FR-011 through FR-014, FR-026; Mastodon auto-detects but Bluesky requires facets.

### 8.4 Image Download from URL

**Decision**: For URL-referenced images, download via `HttpClient` with a configurable timeout and max file size; validate `Content-Type` header before downloading full body.

**Rationale**: Spec FR-022, FR-024; prevents downloading excessively large files or non-image content.

### 8.5 Bluesky Token Lifecycle

**Decision**: Authenticate on first request; cache `accessJwt` and `refreshJwt` in memory; refresh automatically when expired; re-authenticate from credentials if refresh fails.

**Rationale**: Avoids authentication on every request while handling token expiry gracefully. No persistence needed since this is a stateless API.

### 8.6 Mastodon Instance Configuration Caching

**Decision**: Fetch `GET /api/v2/instance` on first request and cache the configuration (character limits, media limits) in memory for the application lifetime.

**Rationale**: Instance configuration rarely changes; caching avoids an extra HTTP call on every post request.

---

## Extended Summary of Decisions

| # | Topic | Decision | Key Rationale |
|---|---|---|---|
| 6a | Bluesky auth | App password → `createSession` → JWT tokens | Recommended for integrations; avoids main password |
| 6b | Bluesky posts | `createRecord` with `app.bsky.feed.post` | Only supported method on AT Protocol |
| 6c | Bluesky char limit | 300 grapheme clusters + 3,000 UTF-8 bytes | Must use `StringInfo.LengthInTextElements` |
| 6d | Bluesky facets | UTF-8 byte offsets for hashtags/links/mentions | Required for rich text; plain text NOT auto-linked |
| 6e | Bluesky images | `uploadBlob` → embed in record | Two-step; max 4 images, max 1 MB each |
| 7a | Mastodon auth | Pre-generated Bearer token | Simplest for single-user; no OAuth flow needed |
| 7b | Mastodon posts | `POST /api/v1/statuses` with form data | Standard endpoint; supports idempotency key |
| 7c | Mastodon char limit | Query `GET /api/v2/instance` dynamically | Varies by instance; 500 is just the default |
| 7d | Mastodon hashtags | Auto-detected from plain text | No annotation API exists |
| 7e | Mastodon images | `POST /api/v2/media` (v1 deprecated) | Two-step; max 4 images, max 16 MB each |
| 8a | Platform abstraction | `ISocialPlatformClient` in Core | Constitution Principle II; testable with mocks |
| 8b | Text shortening | Per-platform; word boundary; "…" ellipsis | FR-005–FR-007; Bluesky uses grapheme clusters |
| 8c | Hashtag processing | Parse, merge, de-dup, facet-build | FR-011–FR-014, FR-026 |
| 8d | URL images | Download with timeout + size guard | FR-022, FR-024 |
| 8e | Bluesky tokens | Cache in memory; auto-refresh | Stateless API; no persistence needed |
| 8f | Mastodon config | Cache instance config in memory | Avoids redundant HTTP calls |

---

## Extended Sources

- [Bluesky: AT Protocol — Create a Post](https://docs.bsky.app/docs/tutorials/creating-a-post) — Record structure, facets, embeds
- [Bluesky: AT Protocol — Upload Images](https://docs.bsky.app/docs/tutorials/creating-a-post#images-embeds) — `uploadBlob`, image embed format
- [Bluesky: AT Protocol — Rich Text Facets](https://docs.bsky.app/docs/advanced-guides/post-richtext) — Facet types, byte offset calculation
- [Bluesky: AT Protocol — Rate Limits](https://docs.bsky.app/docs/advanced-guides/rate-limits) — Points system, daily/hourly limits
- [Mastodon API: Posting a new status](https://docs.joinmastodon.org/methods/statuses/#create) — Parameters, visibility, idempotency
- [Mastodon API: Uploading media](https://docs.joinmastodon.org/methods/media/#v2) — v2 endpoint, description (alt text), async processing
- [Mastodon API: Instance configuration](https://docs.joinmastodon.org/methods/instance/#v2) — Character limits, media limits
- [Mastodon API: Rate Limits](https://docs.joinmastodon.org/api/rate-limits/) — Per-account, per-IP, media-specific

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Infrastructure.Bluesky.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BarretApi.Infrastructure.Bluesky;

public sealed class BlueskyClient(
    HttpClient httpClient,
    IOptions<BlueskyOptions> options,
    ILogger<BlueskyClient> logger)
    : ISocialPlatformClient
{
    private readonly BlueskyOptions _options = options.Value;
    private BlueskySession? _session;
    private DateTimeOffset _sessionExpiresAtUtc = DateTimeOffset.MinValue;
    private readonly SemaphoreSlim _sessionLock = new(1, 1);

    public string PlatformName => "bluesky";

    public async Task<PlatformPostResult> PostAsync(
        string text,
        IReadOnlyList<UploadedImage> images,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var session = await EnsureAuthenticatedAsync(cancellationToken);
            var (result, _) = await CreatePostRecordAsync(session, text, images, reply: null, cancellationToken);
            return result;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Bluesky API request failed");
            return CreateFailureResult(ex.Message, "PLATFORM_ERROR", ex);
        }
        catch (TaskCanceledException ex)
        {
            logger.LogError(ex, "Bluesky API request timed out");
            return CreateFailureResult("Request timed out", "PLATFORM_ERROR", ex);
        }
    }

    public async Task<IReadOnlyList<PlatformPostResult>> PostThreadAsync(
        IReadOnlyList<ThreadSegmentPost> segments,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var session = await EnsureAuthenticatedAsync(cancellationToken);
            var results = new List<PlatformPostResult>();
            BlueskyPostRef? rootRef = null;
            BlueskyPostRef? parentRef = null;

            foreach (var segment in segments)
            {
                BlueskyReplyRef? replyRef = rootRef is not null && parentRef is not null
                    ? new BlueskyReplyRef { Root = rootRef, Parent = parentRef }
                    : null;

                var (postResult, raw) = await CreatePostRecordAsync(
                    session, segment.Text, segment.Images, replyRef, cancellationToken);

                results.Add(postResult);

                if (!postResult.Success || raw is null)
                {
                    break;
                }

                var postRef = new BlueskyPostRef { Uri = raw.Uri, Cid = raw.Cid };
                rootRef ??= postRef;
                parentRef = postRef;
            }

            while (results.Count < segments.Count)
            {
                results.Add(new PlatformPostResult
                {
                    Platform = PlatformName,
                    Success = false,
                    ErrorMessage = "Previous segment failed",
                    ErrorCode = "THREAD_BROKEN"
                });
            }

            return results;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Bluesky API request failed during thread posting");
            return segments.Select(_ => CreateFailureResult(ex.Message, "PLATFORM_ERROR", ex)).ToList();
        }
        catch (TaskCanceledException ex)
        {
            logger.LogError(ex, "Bluesky API request timed out during thread posting");
            return segments.Select(_ => CreateFailureResult("Request timed out", "PLATFORM_ERROR", ex)).ToList();
        }
    }

    private async Task<(PlatformPostResult result, BlueskyCreateRecordResponse? raw)> CreatePostRecordAsync(
        BlueskySession session,
        string text,
        IReadOnlyList<UploadedImage> images,
        BlueskyReplyRef? reply,
        CancellationToken cancellationToken)
    {
        var embed = BuildImageEmbed(images);
        var facets = BlueskyFacetBuilder.BuildFacets(text);

        var request = new BlueskyCreateRecordRequest
        {
            Repo = session.Did,
            Collection = "app.bsky.feed.post",
            Record = new BlueskyPostRecord
            {
                Text = text,
                CreatedAt = DateTimeOffset.UtcNow.ToString("o"),
                Facets = facets,
                Embed = embed,
                Reply = reply
            }
        };

        SetAuthHeader(session.AccessJwt);
        var response = await httpClient.PostAsJsonAsync(
            "/xrpc/com.atproto.repo.createRecord",
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await ReadErrorAsync(response, cancellationToken);
            return (CreateFailureResult(error.errorMessage, error.errorCode), null);
        }

        var raw = await response.Content.ReadFromJsonAsync<BlueskyCreateRecordResponse>(cancellationToken);
        var postUrl = BuildPostUrl(session.Handle, raw!.Uri);

        var postResult = new PlatformPostResult
        {
            Platform = PlatformName,
            Success = true,
            PostId = raw.Uri,
            PostUrl = postUrl,
            PublishedText = text
        };

        return (postResult, raw);
    }

    public async Task<UploadedImage> UploadImageAsync(
        ImageData image,
        CancellationToken cancellationToken = default)
    {
        var session = await EnsureAuthenticatedAsync(cancellationToken);

        using var content = new ByteArrayContent(image.Content);
        content.Headers.ContentType = new MediaTypeHeaderValue(image.ContentType);

        SetAuthHeader(session.AccessJwt);
        var response = await httpClient.PostAsync(
            "/xrpc/com.atproto.repo.uploadBlob",
            content,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await ReadErrorAsync(response, cancellationToken);
            throw new HttpRequestException($"Bluesky blob upload failed: {error.errorMessage}");
        }

        var result = await response.Content.ReadFromJsonAsync<BlueskyUploadBlobResponse>(cancellationToken);

        return new UploadedImage
        {
            PlatformImageId = result!.Blob.Ref.Link,
            AltText = image.AltText,
            PlatformData = result.Blob
        };
    }

    public Task<PlatformConfiguration> GetConfigurationAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PlatformConfiguration
        {
            Name = PlatformName,
            MaxCharacters = 300,
            MaxImages = 4,
            MaxImageSizeBytes = 1_048_576,
            MaxAltTextLength = 1_000
        });
    }

    private async Task<BlueskySession> EnsureAuthenticatedAsync(CancellationToken cancellationToken)
    {
        if (_session is not null && _sessionExpiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(2))
        {
            return _session;
        }

        await _sessionLock.WaitAsync(cancellationToken);
        try
        {
            if (_session is not null && _sessionExpiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(2))
            {
                return _session;
            }

            if (_session is not null)
            {
                try
                {
                    _session = await RefreshSessionAsync(_session.RefreshJwt, cancellationToken);
                    _sessionExpiresAtUtc = GetTokenExpiryUtc(_session.AccessJwt);
                    return _session;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Bluesky session refresh failed, creating new session");
                    _session = null;
                }
            }

            _session = await CreateSessionAsync(cancellationToken);
            _sessionExpiresAtUtc = GetTokenExpiryUtc(_session.AccessJwt);
            return _session;
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    private async Task<BlueskySession> RefreshSessionAsync(
        string refreshJwt,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Refreshing Bluesky session");

        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", refreshJwt);

        var response = await httpClient.PostAsync(
            "/xrpc/com.atproto.server.refreshSession",
            null,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await ReadErrorAsync(response, cancellationToken);
            throw new InvalidOperationException($"Bluesky session refresh failed: {error.errorMessage}");
        }

        var session = await response.Content.ReadFromJsonAsync<BlueskySession>(cancellationToken)
            ?? throw new InvalidOperationException("Failed to deserialize Bluesky refresh session response");

        logger.LogInformation("Bluesky session refreshed for {Did}", session.Did);
        return session;
    }

    private async Task<BlueskySession> CreateSessionAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Authenticating with Bluesky as {Handle}", _options.Handle);

        var payload = new { identifier = _options.Handle, password = _options.AppPassword };
        var response = await httpClient.PostAsJsonAsync(
            "/xrpc/com.atproto.server.createSession",
            payload,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await ReadErrorAsync(response, cancellationToken);
            throw new InvalidOperationException($"Bluesky authentication failed: {error.errorMessage}");
        }

        var session = await response.Content.ReadFromJsonAsync<BlueskySession>(cancellationToken)
            ?? throw new InvalidOperationException("Failed to deserialize Bluesky session response");

        logger.LogInformation("Authenticated with Bluesky as {Did}", session.Did);
        return session;
    }

    private void SetAuthHeader(string accessJwt)
    {
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessJwt);
    }

    private static object? BuildImageEmbed(IReadOnlyList<UploadedImage> images)
    {
        if (images.Count == 0)
        {
            return null;
        }

        return new BlueskyImageEmbed
        {
            Images = images.Select(img => new BlueskyEmbedImage
            {
                Alt = img.AltText,
                Image = (BlueskyBlob)img.PlatformData!
            }).ToList()
        };
    }

    private static string BuildPostUrl(string handle, string atUri)
    {
        var rkey = atUri.Split('/').Last();
        return $"https://bsky.app/profile/{handle}/post/{rkey}";
    }

    private static async Task<(string errorMessage, string errorCode)> ReadErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var statusCode = (int)response.StatusCode;

        try
        {
            var errorBody = await response.Content.ReadFromJsonAsync<BlueskyErrorResponse>(cancellationToken);
            var message = errorBody?.Message ?? errorBody?.Error ?? $"HTTP {statusCode}";
            var code = MapErrorCode(statusCode, errorBody?.Error);
            return (message, code);
        }
        catch (JsonException)
        {
            return ($"HTTP {statusCode}", MapErrorCode(statusCode, null));
        }
    }

    private static string MapErrorCode(int statusCode, string? errorType)
    {
        return errorType switch
        {
            "InvalidToken" or "ExpiredToken" => "AUTH_FAILED",
            "RateLimitExceeded" => "RATE_LIMITED",
            _ => statusCode switch
            {
                401 => "AUTH_FAILED",
                429 => "RATE_LIMITED",
                >= 400 and < 500 => "VALIDATION_FAILED",
                _ => "PLATFORM_ERROR"
            }
        };
    }

    private static DateTimeOffset GetTokenExpiryUtc(string jwt)
    {
        var parts = jwt.Split('.');
        if (parts.Length != 3)
        {
            return DateTimeOffset.MinValue;
        }

        var payload = parts[1]
            .Replace('-', '+')
            .Replace('_', '/');

        switch (payload.Length % 4)
        {
            case 2:
                payload += "==";
                break;
            case 3:
                payload += "=";
                break;
        }

        try
        {
            var json = Convert.FromBase64String(payload);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("exp", out var exp))
            {
                return DateTimeOffset.FromUnixTimeSeconds(exp.GetInt64());
            }
        }
        catch (Exception)
        {
            // If JWT parsing fails, return MinValue to force re-authentication
        }

        return DateTimeOffset.MinValue;
    }

    private PlatformPostResult CreateFailureResult(
        string message,
        string errorCode,
        Exception? exception = null)
    {
        return new PlatformPostResult
        {
            Platform = PlatformName,
            Success = false,
            ErrorMessage = message,
            ErrorCode = errorCode,
            Error = exception ?? new InvalidOperationException(message)
        };
    }
}

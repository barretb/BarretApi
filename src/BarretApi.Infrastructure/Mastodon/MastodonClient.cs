using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Infrastructure.Mastodon.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BarretApi.Infrastructure.Mastodon;

public sealed class MastodonClient(
    HttpClient httpClient,
    IOptions<MastodonOptions> options,
    ILogger<MastodonClient> logger)
    : ISocialPlatformClient
{
    private readonly MastodonOptions _options = options.Value;
    private MastodonInstanceConfig? _instanceConfig;
    private readonly SemaphoreSlim _configLock = new(1, 1);

    public string PlatformName => "mastodon";

    public async Task<PlatformPostResult> PostAsync(
        string text,
        IReadOnlyList<UploadedImage> images,
        CancellationToken cancellationToken = default)
    {
        try
        {
            SetAuthHeader();

            var formData = new List<KeyValuePair<string, string>>
            {
                new("status", text)
            };

            foreach (var image in images)
            {
                formData.Add(new KeyValuePair<string, string>("media_ids[]", image.PlatformImageId));
            }

            using var content = new FormUrlEncodedContent(formData);
            var response = await httpClient.PostAsync("/api/v1/statuses", content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await ReadErrorAsync(response, cancellationToken);
                return CreateFailureResult(error.errorMessage, error.errorCode);
            }

            var status = await response.Content.ReadFromJsonAsync<MastodonStatus>(cancellationToken);

            return new PlatformPostResult
            {
                Platform = PlatformName,
                Success = true,
                PostId = status!.Id,
                PostUrl = status.Url,
                PublishedText = text
            };
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Mastodon API request failed");
            return CreateFailureResult(ex.Message, "PLATFORM_ERROR", ex);
        }
        catch (TaskCanceledException ex)
        {
            logger.LogError(ex, "Mastodon API request timed out");
            return CreateFailureResult("Request timed out", "PLATFORM_ERROR", ex);
        }
    }

    public async Task<UploadedImage> UploadImageAsync(
        ImageData image,
        CancellationToken cancellationToken = default)
    {
        SetAuthHeader();

        using var formContent = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(image.Content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(image.ContentType);
        formContent.Add(fileContent, "file", image.FileName ?? "image");

        if (!string.IsNullOrWhiteSpace(image.AltText))
        {
            formContent.Add(new StringContent(image.AltText), "description");
        }

        var response = await httpClient.PostAsync("/api/v2/media", formContent, cancellationToken);
        response.EnsureSuccessStatusCode();

        var attachment = await response.Content.ReadFromJsonAsync<MastodonMediaAttachment>(cancellationToken);

        return new UploadedImage
        {
            PlatformImageId = attachment!.Id,
            AltText = image.AltText
        };
    }

    public async Task<PlatformConfiguration> GetConfigurationAsync(
        CancellationToken cancellationToken = default)
    {
        var config = await EnsureInstanceConfigAsync(cancellationToken);
        var statuses = config.Configuration?.Statuses;
        var media = config.Configuration?.MediaAttachments;

        return new PlatformConfiguration
        {
            Name = PlatformName,
            MaxCharacters = statuses?.MaxCharacters ?? 500,
            MaxImages = statuses?.MaxMediaAttachments ?? 4,
            MaxImageSizeBytes = media?.ImageSizeLimit ?? 16_777_216,
            MaxAltTextLength = media?.DescriptionLimit ?? 1_500
        };
    }

    private async Task<MastodonInstanceConfig> EnsureInstanceConfigAsync(CancellationToken cancellationToken)
    {
        if (_instanceConfig is not null)
        {
            return _instanceConfig;
        }

        await _configLock.WaitAsync(cancellationToken);
        try
        {
            if (_instanceConfig is not null)
            {
                return _instanceConfig;
            }

            _instanceConfig = await FetchInstanceConfigAsync(cancellationToken);
            return _instanceConfig;
        }
        finally
        {
            _configLock.Release();
        }
    }

    private async Task<MastodonInstanceConfig> FetchInstanceConfigAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Fetching Mastodon instance configuration from {InstanceUrl}", _options.InstanceUrl);

        var response = await httpClient.GetAsync("/api/v2/instance", cancellationToken);
        response.EnsureSuccessStatusCode();

        var config = await response.Content.ReadFromJsonAsync<MastodonInstanceConfig>(cancellationToken)
            ?? new MastodonInstanceConfig();

        logger.LogInformation(
            "Mastodon instance config: MaxCharacters={MaxCharacters}, MaxMedia={MaxMedia}",
            config.Configuration?.Statuses?.MaxCharacters ?? 500,
            config.Configuration?.Statuses?.MaxMediaAttachments ?? 4);

        return config;
    }

    private void SetAuthHeader()
    {
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _options.AccessToken);
    }

    private static async Task<(string errorMessage, string errorCode)> ReadErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var statusCode = (int)response.StatusCode;

        try
        {
            var errorBody = await response.Content.ReadFromJsonAsync<MastodonError>(cancellationToken);
            var message = errorBody?.Error ?? $"HTTP {statusCode}";
            var code = MapErrorCode(statusCode);
            return (message, code);
        }
        catch (JsonException)
        {
            return ($"HTTP {statusCode}", MapErrorCode(statusCode));
        }
    }

    private static string MapErrorCode(int statusCode)
    {
        return statusCode switch
        {
            401 => "AUTH_FAILED",
            403 => "AUTH_FAILED",
            429 => "RATE_LIMITED",
            422 => "VALIDATION_FAILED",
            >= 400 and < 500 => "VALIDATION_FAILED",
            _ => "PLATFORM_ERROR"
        };
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

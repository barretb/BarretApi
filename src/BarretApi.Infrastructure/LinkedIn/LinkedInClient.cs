using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BarretApi.Core.Configuration;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Infrastructure.LinkedIn.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BarretApi.Infrastructure.LinkedIn;

public sealed class LinkedInClient(
    HttpClient httpClient,
    IOptions<LinkedInOptions> options,
    LinkedInTokenProvider tokenProvider,
    ILogger<LinkedInClient> logger)
    : ISocialPlatformClient
{
    private static readonly HttpClient UploadHttpClient = new();
    private readonly LinkedInOptions _options = options.Value;

    public string PlatformName => "linkedin";

    public async Task<PlatformPostResult> PostAsync(
        string text,
        IReadOnlyList<UploadedImage> images,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = BuildPostRequest(text, images);
            var jsonContent = JsonContent.Create(payload);
            await jsonContent.LoadIntoBufferAsync();

            using var request = new HttpRequestMessage(HttpMethod.Post, "/v2/ugcPosts")
            {
                Content = jsonContent
            };
            await SetAuthHeadersAsync(request, cancellationToken);

            var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var error = await ReadErrorAsync(response, cancellationToken);
                return CreateFailureResult(error.errorMessage, error.errorCode);
            }

            var postId = ReadPostId(response);
            var postUrl = BuildPostUrl(postId);

            return new PlatformPostResult
            {
                Platform = PlatformName,
                Success = true,
                PostId = postId,
                PostUrl = postUrl,
                PublishedText = text
            };
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "LinkedIn API request failed");
            return CreateFailureResult(ex.Message, "PLATFORM_ERROR", ex);
        }
        catch (TaskCanceledException ex)
        {
            logger.LogError(ex, "LinkedIn API request timed out");
            return CreateFailureResult("Request timed out", "PLATFORM_ERROR", ex);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected LinkedIn posting error");
            return CreateFailureResult(ex.Message, "UNKNOWN_ERROR", ex);
        }
    }

    public async Task<UploadedImage> UploadImageAsync(
        ImageData image,
        CancellationToken cancellationToken = default)
    {
        var uploadRegistration = await RegisterUploadAsync(cancellationToken);
        await UploadBinaryAsync(uploadRegistration.uploadUrl, image, cancellationToken);

        return new UploadedImage
        {
            PlatformImageId = uploadRegistration.asset,
            AltText = image.AltText
        };
    }

    public Task<PlatformConfiguration> GetConfigurationAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PlatformConfiguration
        {
            Name = PlatformName,
            MaxCharacters = 3_000,
            MaxImages = 9,
            MaxImageSizeBytes = 20 * 1024 * 1024,
            MaxAltTextLength = 4_086
        });
    }

    private async Task<(string asset, string uploadUrl)> RegisterUploadAsync(CancellationToken cancellationToken)
    {
        var payload = new LinkedInRegisterUploadRequest
        {
            RegisterUploadRequest = new LinkedInUploadRegistration
            {
                Owner = _options.AuthorUrn,
                Recipes = ["urn:li:digitalmediaRecipe:feedshare-image"],
                ServiceRelationships =
                [
                    new LinkedInServiceRelationship
                    {
                        RelationshipType = "OWNER",
                        Identifier = "urn:li:userGeneratedContent"
                    }
                ]
            }
        };

        var jsonContent = JsonContent.Create(payload);
        await jsonContent.LoadIntoBufferAsync();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/v2/assets?action=registerUpload")
        {
            Content = jsonContent
        };
        await SetAuthHeadersAsync(request, cancellationToken);

        var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await ReadErrorAsync(response, cancellationToken);
            throw new InvalidOperationException($"LinkedIn upload registration failed: {error.errorMessage}");
        }

        var registration = await response.Content.ReadFromJsonAsync<LinkedInRegisterUploadResponse>(cancellationToken);
        var asset = registration?.Value?.Asset;
        var uploadUrl = registration?.Value?.UploadMechanism?.MediaUploadHttpRequest?.UploadUrl;

        if (string.IsNullOrWhiteSpace(asset) || string.IsNullOrWhiteSpace(uploadUrl))
        {
            throw new InvalidOperationException("LinkedIn upload registration response was missing required fields.");
        }

        return (asset, uploadUrl);
    }

    private static async Task UploadBinaryAsync(string uploadUrl, ImageData image, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, uploadUrl)
        {
            Content = new ByteArrayContent(image.Content)
        };

        request.Content.Headers.ContentType = new MediaTypeHeaderValue(image.ContentType);
        var response = await UploadHttpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private LinkedInUgcPostRequest BuildPostRequest(string text, IReadOnlyList<UploadedImage> images)
    {
        var hasImages = images.Count > 0;
        return new LinkedInUgcPostRequest
        {
            Author = _options.AuthorUrn,
            SpecificContent = new LinkedInSpecificContent
            {
                ShareContent = new LinkedInShareContent
                {
                    ShareCommentary = new LinkedInTextValue
                    {
                        Text = text
                    },
                    ShareMediaCategory = hasImages ? "IMAGE" : "NONE",
                    Media = hasImages ? images.Select(image => new LinkedInMedia
                    {
                        MediaUrn = image.PlatformImageId,
                        Description = new LinkedInTextValue
                        {
                            Text = image.AltText
                        }
                    }).ToList() : null
                }
            },
            Visibility = new LinkedInVisibility()
        };
    }

    private async Task SetAuthHeadersAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var accessToken = await tokenProvider.GetAccessTokenAsync(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.TryAddWithoutValidation("X-Restli-Protocol-Version", "2.0.0");
    }

    private static string? ReadPostId(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("x-restli-id", out var values))
        {
            return values.FirstOrDefault();
        }

        return null;
    }

    private static string? BuildPostUrl(string? postId)
    {
        if (string.IsNullOrWhiteSpace(postId))
        {
            return null;
        }

        var encoded = Uri.EscapeDataString(postId);
        return $"https://www.linkedin.com/feed/update/{encoded}";
    }

    private static async Task<(string errorMessage, string errorCode)> ReadErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var statusCode = (int)response.StatusCode;

        try
        {
            var errorBody = await response.Content.ReadFromJsonAsync<LinkedInErrorResponse>(cancellationToken);
            var message = errorBody?.Message ?? errorBody?.ErrorDescription ?? $"HTTP {statusCode}";
            var code = MapErrorCode(statusCode);
            return (message, code);
        }
        catch (JsonException)
        {
            var fallback = await response.Content.ReadAsStringAsync(cancellationToken);
            var message = string.IsNullOrWhiteSpace(fallback) ? $"HTTP {statusCode}" : fallback;
            return (TrimError(message), MapErrorCode(statusCode));
        }
    }

    private static string TrimError(string message)
    {
        if (message.Length <= 500)
        {
            return message;
        }

        return message[..500];
    }

    private static string MapErrorCode(int statusCode)
    {
        return statusCode switch
        {
            401 => "AUTH_FAILED",
            403 => "AUTH_FAILED",
            429 => "RATE_LIMITED",
            400 => "VALIDATION_FAILED",
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
            ErrorMessage = TrimError(message),
            ErrorCode = errorCode,
            Error = exception ?? new InvalidOperationException(message)
        };
    }
}
using AngleSharp;
using AngleSharp.Html.Parser;
using BarretApi.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace BarretApi.Infrastructure.Services;

/// <summary>
/// Fetches a web page and extracts visible text using AngleSharp HTML parsing.
/// </summary>
public sealed class AngleSharpHtmlTextExtractor(
    HttpClient httpClient,
    ILogger<AngleSharpHtmlTextExtractor> logger) : IHtmlTextExtractor
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly ILogger<AngleSharpHtmlTextExtractor> _logger = logger;

    private const int MaxContentBytes = 512_000; // 500 KB
    private const int MaxRedirects = 5;
    private static readonly TimeSpan FetchTimeout = TimeSpan.FromSeconds(30);

    public async Task<string> ExtractTextAsync(string url, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        _logger.LogInformation("Fetching web page for text extraction: {Url}", url);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(FetchTimeout);

        HttpResponseMessage response;
        try
        {
            var uri = new Uri(url);
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            response.EnsureSuccessStatusCode();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Fetch timed out after {Timeout}s for {Url}", FetchTimeout.TotalSeconds, url);
            throw new HttpRequestException($"The request to fetch the web page timed out after {(int)FetchTimeout.TotalSeconds} seconds.");
        }

        var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;

        if (!contentType.Contains("html", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Non-HTML content type '{ContentType}' for {Url}", contentType, url);
            throw new InvalidOperationException("The target URL did not return HTML content.");
        }

        var contentLength = response.Content.Headers.ContentLength;

        if (contentLength > MaxContentBytes)
        {
            _logger.LogInformation(
                "Content exceeds max size ({ContentLength} > {MaxBytes}), will truncate for {Url}",
                contentLength, MaxContentBytes, url);
        }

        var html = await ReadContentWithLimitAsync(response, cts.Token);

        _logger.LogInformation("Fetched {Length} characters of HTML from {Url}", html.Length, url);

        return ParseHtmlToText(html);
    }

    private static async Task<string> ReadContentWithLimitAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        var buffer = new char[MaxContentBytes];
        var charsRead = await reader.ReadBlockAsync(buffer, cancellationToken);

        return new string(buffer, 0, charsRead);
    }

    private static string ParseHtmlToText(string html)
    {
        var context = BrowsingContext.New(Configuration.Default);
        var parser = context.GetService<IHtmlParser>()!;
        using var document = parser.ParseDocument(html);

        foreach (var element in document.QuerySelectorAll("script, style, noscript, svg, head"))
        {
            element.Remove();
        }

        return document.Body?.TextContent?.Trim() ?? string.Empty;
    }
}

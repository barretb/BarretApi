using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Core.Services;
using FastEndpoints;
using Microsoft.Extensions.Logging;

namespace BarretApi.Api.Features.WordCloud;

public sealed class GenerateWordCloudEndpoint(
    IHtmlTextExtractor textExtractor,
    TextAnalysisService textAnalysisService,
    IWordCloudGenerator wordCloudGenerator,
    ILogger<GenerateWordCloudEndpoint> logger)
    : Endpoint<GenerateWordCloudRequest>
{
    private readonly IHtmlTextExtractor _textExtractor = textExtractor;
    private readonly TextAnalysisService _textAnalysisService = textAnalysisService;
    private readonly IWordCloudGenerator _wordCloudGenerator = wordCloudGenerator;
    private readonly ILogger<GenerateWordCloudEndpoint> _logger = logger;

    private const int DefaultWidth = 800;
    private const int DefaultHeight = 600;
    private const int MaxWords = 100;

    public override void Configure()
    {
        Post("/api/word-cloud");

        Summary(s =>
        {
            s.Summary = "Generate word cloud from web page";
            s.Description = "Accepts a URL to a web page, fetches and parses the HTML to extract visible text, "
                + "removes common English stop words, counts word frequencies, and returns a PNG word cloud image.";
            s.ExampleRequest = new GenerateWordCloudRequest
            {
                Url = "https://en.wikipedia.org/wiki/.NET",
                Width = 1200,
                Height = 800
            };
            s.Responses[200] = "Word cloud PNG image generated successfully.";
            s.Responses[400] = "Request validation failed (malformed URL, invalid dimensions).";
            s.Responses[401] = "Missing or invalid X-Api-Key.";
            s.Responses[422] = "Page fetched but insufficient text content for word cloud generation.";
            s.Responses[502] = "Failed to fetch the target web page.";
        });
    }

    public override async Task HandleAsync(GenerateWordCloudRequest req, CancellationToken ct)
    {
        var url = req.Url!;
        var correlationId = Guid.NewGuid().ToString("N");

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["Url"] = url
        });

        _logger.LogInformation("Word cloud generation requested for {Url}", url);

        string text;
        try
        {
            text = await _textExtractor.ExtractTextAsync(url, ct);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to fetch web page: {Url}", url);
            await SendErrorResponseAsync("Failed to fetch the web page. The target URL is unreachable or returned an error.", 502, ct);
            return;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid content from {Url}: {Message}", url, ex.Message);
            await SendErrorResponseAsync(ex.Message, 502, ct);
            return;
        }

        var frequencies = _textAnalysisService.AnalyzeText(text, MaxWords);

        if (frequencies.Count == 0)
        {
            _logger.LogWarning("Insufficient text content from {Url}", url);
            await SendErrorResponseAsync(
                "The page contains insufficient text content to generate a word cloud.", 422, ct);
            return;
        }

        var options = new WordCloudOptions
        {
            Width = req.Width ?? DefaultWidth,
            Height = req.Height ?? DefaultHeight,
            MaxWords = MaxWords
        };

        var imageBytes = await _wordCloudGenerator.GenerateAsync(frequencies, options, ct);

        _logger.LogInformation(
            "Word cloud generated: {Size} bytes, {Width}x{Height}px for {Url}",
            imageBytes.Length, options.Width, options.Height, url);

        HttpContext.Response.ContentType = "image/png";
        HttpContext.Response.ContentLength = imageBytes.Length;
        await HttpContext.Response.Body.WriteAsync(imageBytes, ct);
    }

    private async Task SendErrorResponseAsync(string message, int statusCode, CancellationToken ct)
    {
        await Send.ResponseAsync(new { statusCode, message }, statusCode, ct);
    }
}

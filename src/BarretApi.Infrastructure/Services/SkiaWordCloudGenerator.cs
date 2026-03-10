using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using KnowledgePicker.WordCloud;
using KnowledgePicker.WordCloud.Coloring;
using KnowledgePicker.WordCloud.Drawing;
using KnowledgePicker.WordCloud.Layouts;
using KnowledgePicker.WordCloud.Primitives;
using KnowledgePicker.WordCloud.Sizers;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace BarretApi.Infrastructure.Services;

/// <summary>
/// Generates word cloud PNG images using KnowledgePicker.WordCloud with SkiaSharp rendering.
/// </summary>
public sealed class SkiaWordCloudGenerator(
    ILogger<SkiaWordCloudGenerator> logger) : IWordCloudGenerator
{
    private readonly ILogger<SkiaWordCloudGenerator> _logger = logger;

    public Task<byte[]> GenerateAsync(
        IReadOnlyList<WordFrequency> frequencies,
        WordCloudOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(frequencies);

        if (frequencies.Count == 0)
        {
            throw new ArgumentException("Frequencies must not be empty.", nameof(frequencies));
        }

        _logger.LogInformation(
            "Generating word cloud: {WordCount} words, {Width}x{Height}px",
            frequencies.Count, options.Width, options.Height);

        var entries = frequencies.Select(f => new WordCloudEntry(f.Word, f.Count));

        var wordCloudInput = new WordCloudInput(entries)
        {
            Width = options.Width,
            Height = options.Height,
            MinFontSize = options.MinFontSize,
            MaxFontSize = options.MaxFontSize
        };

        var sizer = new LogSizer(wordCloudInput);
        using var engine = new SkGraphicEngine(sizer, wordCloudInput);
        var layout = new SpiralLayout(wordCloudInput);
        var colorizer = new RandomColorizer();
        var generator = new WordCloudGenerator<SKBitmap>(wordCloudInput, engine, layout, colorizer);

        using var wordBitmap = generator.Draw();

        using var final = new SKBitmap(options.Width, options.Height);
        using var canvas = new SKCanvas(final);
        canvas.Clear(SKColors.White);
        canvas.DrawBitmap(wordBitmap, 0, 0);

        using var data = final.Encode(SKEncodedImageFormat.Png, 100);
        var bytes = data.ToArray();

        _logger.LogInformation("Word cloud generated: {Size} bytes", bytes.Length);

        return Task.FromResult(bytes);
    }
}

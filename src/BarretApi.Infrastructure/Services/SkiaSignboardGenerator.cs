using System.Reflection;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace BarretApi.Infrastructure.Services;

/// <summary>
/// Renders letterboard-style signboard images (white lightbox face, dark frame,
/// track lines, tile letters with occasional red/gray accents) using SkiaSharp.
/// </summary>
public sealed class SkiaSignboardGenerator(ILogger<SkiaSignboardGenerator> logger) : ISignboardGenerator
{
	private readonly ILogger<SkiaSignboardGenerator> _logger = logger;

	private const string FontResource = "BarretApi.Infrastructure.Fonts.Anton-Regular.ttf";

	private static readonly SKColor FrameColor = new(0x2B, 0x2B, 0x2B);
	private static readonly SKColor BoardColor = new(0xF7, 0xF5, 0xF0);
	private static readonly SKColor TrackLineColor = new(0xDE, 0xDA, 0xD2);
	private static readonly SKColor LetterColor = new(0x1E, 0x1E, 0x1E);
	private static readonly SKColor AccentRed = new(0xC2, 0x20, 0x26);
	private static readonly SKColor AccentGray = new(0x8F, 0x8C, 0x86);

	private const float FrameRatio = 0.045f;
	private const float BoardPaddingRatio = 0.06f;
	private const float RowSpacingFactor = 1.5f;
	private const float BaselineFactor = 0.36f;
	private const float FontStep = 2f;
	private const float MinFontSize = 4f;
	private const float MaxJitterDegrees = 1.6f;
	private const float MaxJitterOffsetRatio = 0.035f;

	public Task<byte[]> GenerateAsync(SignboardGenerationCommand command, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(command);
		cancellationToken.ThrowIfCancellationRequested();

		if (string.IsNullOrWhiteSpace(command.Text))
		{
			throw new ArgumentException("Text must not be empty.", nameof(command));
		}

		_logger.LogInformation(
			"Generating signboard: {Width}x{Height}, seed {Seed}, {Length} chars",
			command.Width,
			command.Height,
			command.Seed,
			command.Text.Length);

		var segments = NormalizeText(command.Text);
		var random = new Random(command.Seed);

		using var typeface = LoadTypeface();
		using var surface = SKSurface.Create(new SKImageInfo(command.Width, command.Height));
		var canvas = surface.Canvas;

		var frame = FrameRatio * Math.Min(command.Width, command.Height);
		var boardFace = new SKRect(frame, frame, command.Width - frame, command.Height - frame);

		canvas.Clear(FrameColor);

		using (var boardPaint = new SKPaint { Color = BoardColor, IsAntialias = true })
		{
			canvas.DrawRoundRect(boardFace, frame * 0.35f, frame * 0.35f, boardPaint);
		}

		var padding = BoardPaddingRatio * Math.Min(boardFace.Width, boardFace.Height);
		var textArea = new SKRect(
			boardFace.Left + padding,
			boardFace.Top + padding,
			boardFace.Right - padding,
			boardFace.Bottom - padding);

		var (fontSize, lines) = FitText(segments, typeface, textArea);
		var slotHeight = fontSize * RowSpacingFactor;

		// Last-resort safety net: even after FitText's shrink-to-fit loop, guard against
		// a degenerate case where the wrapped line count still exceeds what the text area
		// can hold. Clamp the rendered rows to the available capacity so the grid (and
		// gridTop) can never extend above the text area and off the top of the canvas.
		var slotCapacity = Math.Max(1, (int)(textArea.Height / slotHeight));
		var renderedLines = lines.Count > slotCapacity ? lines.Take(slotCapacity).ToList() : lines;

		var slotCount = Math.Max(renderedLines.Count, slotCapacity);
		var gridTop = textArea.MidY - slotCount * slotHeight / 2f;

		DrawTrackLines(canvas, boardFace, gridTop, slotHeight, slotCount);
		DrawLetterRows(canvas, renderedLines, typeface, fontSize, gridTop, slotHeight, slotCount, textArea, random);
		DrawInnerShadow(canvas, boardFace, frame);

		using var image = surface.Snapshot();
		using var data = image.Encode(SKEncodedImageFormat.Png, 100);
		return Task.FromResult(data.ToArray());
	}

	private static IReadOnlyList<string> NormalizeText(string text)
	{
		return text
			.ToUpperInvariant()
			.ReplaceLineEndings("\n")
			.Split('\n', StringSplitOptions.TrimEntries)
			.Where(s => s.Length > 0)
			.ToList();
	}

	private static SKTypeface LoadTypeface()
	{
		var assembly = Assembly.GetExecutingAssembly();
		using var stream = assembly.GetManifestResourceStream(FontResource)
			?? throw new InvalidOperationException($"Embedded font resource not found: {FontResource}");
		using var ms = new MemoryStream();
		stream.CopyTo(ms);
		ms.Position = 0;
		using var data = SKData.CreateCopy(ms.ToArray());
		return SKTypeface.FromData(data)
			?? throw new InvalidOperationException("Failed to load signboard font.");
	}

	private static (float FontSize, IReadOnlyList<string> Lines) FitText(
		IReadOnlyList<string> segments,
		SKTypeface typeface,
		SKRect textArea)
	{
		var startSize = Math.Max(MinFontSize, textArea.Height / RowSpacingFactor);

		for (var size = startSize; size >= MinFontSize; size -= FontStep)
		{
			using var font = new SKFont(typeface, size);
			var lines = WrapSegments(segments, font, textArea.Width);
			var fitsWidth = lines.All(l => MeasureLine(l, font) <= textArea.Width);
			var fitsHeight = lines.Count * size * RowSpacingFactor <= textArea.Height;

			if (fitsWidth && fitsHeight)
			{
				return (size, lines);
			}
		}

		using var minFont = new SKFont(typeface, MinFontSize);
		return (MinFontSize, WrapSegments(segments, minFont, textArea.Width));
	}

	private static List<string> WrapSegments(IReadOnlyList<string> segments, SKFont font, float maxWidth)
	{
		var lines = new List<string>();

		foreach (var segment in segments)
		{
			var current = string.Empty;

			foreach (var word in segment.Split(' ', StringSplitOptions.RemoveEmptyEntries))
			{
				if (MeasureLine(word, font) <= maxWidth)
				{
					var candidate = current.Length == 0 ? word : $"{current} {word}";

					if (MeasureLine(candidate, font) <= maxWidth || current.Length == 0)
					{
						current = candidate;
						continue;
					}

					lines.Add(current);
					current = word;
					continue;
				}

				// The word alone is wider than the board face (e.g. one very long run of
				// characters with no spaces). Hard-break it mid-word into chunks that each
				// fit maxWidth, flushing whatever line is already in progress first. Chunks
				// are concatenated with no separator so the word reconstructs exactly.
				var chunks = SplitOversizedWord(word, font, maxWidth).ToList();

				for (var i = 0; i < chunks.Count; i++)
				{
					if (current.Length > 0)
					{
						lines.Add(current);
						current = string.Empty;
					}

					var isLastChunk = i == chunks.Count - 1;
					if (isLastChunk)
					{
						current = chunks[i];
					}
					else
					{
						lines.Add(chunks[i]);
					}
				}
			}

			if (current.Length > 0)
			{
				lines.Add(current);
			}
		}

		return lines;
	}

	/// <summary>
	/// Splits <paramref name="word"/> into the fewest chunks that each measure within
	/// <paramref name="maxWidth"/>, cutting mid-word (no hyphenation) as a last resort.
	/// Returns the word unchanged as a single chunk when it already fits.
	/// </summary>
	private static IEnumerable<string> SplitOversizedWord(string word, SKFont font, float maxWidth)
	{
		if (MeasureLine(word, font) <= maxWidth)
		{
			yield return word;
			yield break;
		}

		var chunk = string.Empty;

		foreach (var c in word)
		{
			var candidate = chunk + c;

			if (chunk.Length > 0 && MeasureLine(candidate, font) > maxWidth)
			{
				yield return chunk;
				chunk = c.ToString();
			}
			else
			{
				chunk = candidate;
			}
		}

		if (chunk.Length > 0)
		{
			yield return chunk;
		}
	}

	private static float MeasureLine(string line, SKFont font)
	{
		var width = 0f;
		foreach (var c in line)
		{
			width += font.MeasureText(c.ToString());
		}

		return width;
	}

	private static void DrawTrackLines(SKCanvas canvas, SKRect boardFace, float gridTop, float slotHeight, int slotCount)
	{
		using var linePaint = new SKPaint
		{
			Color = TrackLineColor,
			IsAntialias = true,
			StrokeWidth = Math.Max(1.5f, slotHeight * 0.03f)
		};

		for (var i = 0; i <= slotCount; i++)
		{
			var y = gridTop + i * slotHeight;
			canvas.DrawLine(boardFace.Left + 4f, y, boardFace.Right - 4f, y, linePaint);
		}
	}

	private void DrawLetterRows(
		SKCanvas canvas,
		IReadOnlyList<string> lines,
		SKTypeface typeface,
		float fontSize,
		float gridTop,
		float slotHeight,
		int slotCount,
		SKRect textArea,
		Random random)
	{
		using var font = new SKFont(typeface, fontSize);
		using var letterPaint = new SKPaint { IsAntialias = true };
		using var tileFill = new SKPaint { Color = new SKColor(0xFF, 0xFF, 0xFF, 18), IsAntialias = true };
		using var tileBorder = new SKPaint
		{
			Color = new SKColor(0x00, 0x00, 0x00, 12),
			IsAntialias = true,
			IsStroke = true,
			StrokeWidth = 1f
		};

		var firstSlot = (slotCount - lines.Count) / 2;

		for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
		{
			var line = lines[lineIndex];
			var slotCenterY = gridTop + (firstSlot + lineIndex) * slotHeight + slotHeight / 2f;
			var baseline = slotCenterY + fontSize * BaselineFactor;
			var lineWidth = MeasureLine(line, font);
			var x = textArea.MidX - lineWidth / 2f;

			foreach (var c in line)
			{
				var glyph = c.ToString();
				var advance = font.MeasureText(glyph);

				if (!char.IsWhiteSpace(c))
				{
					var colorRoll = random.Next(24);
					letterPaint.Color = colorRoll == 0
						? AccentGray
						: colorRoll <= 3 ? AccentRed : LetterColor;

					var rotation = (random.NextSingle() * 2f - 1f) * MaxJitterDegrees;
					var offsetY = (random.NextSingle() * 2f - 1f) * MaxJitterOffsetRatio * fontSize;
					var centerX = x + advance / 2f;

					canvas.Save();
					canvas.RotateDegrees(rotation, centerX, slotCenterY);

					var tile = new SKRect(
						x - advance * 0.06f,
						slotCenterY - slotHeight * 0.46f + offsetY,
						x + advance * 1.06f,
						slotCenterY + slotHeight * 0.46f + offsetY);
					canvas.DrawRoundRect(tile, 2f, 2f, tileFill);
					canvas.DrawRoundRect(tile, 2f, 2f, tileBorder);

					canvas.DrawText(glyph, x, baseline + offsetY, SKTextAlign.Left, font, letterPaint);
					canvas.Restore();
				}

				x += advance;
			}
		}
	}

	private static void DrawInnerShadow(SKCanvas canvas, SKRect boardFace, float frame)
	{
		using var maskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, frame * 0.6f);
		using var shadowPaint = new SKPaint
		{
			Color = new SKColor(0x00, 0x00, 0x00, 40),
			IsAntialias = true,
			IsStroke = true,
			StrokeWidth = frame * 0.9f,
			MaskFilter = maskFilter
		};

		canvas.Save();
		canvas.ClipRoundRect(new SKRoundRect(boardFace, frame * 0.35f), antialias: true);
		canvas.DrawRoundRect(boardFace, frame * 0.35f, frame * 0.35f, shadowPaint);
		canvas.Restore();
	}
}

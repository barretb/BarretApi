# Signboard Image Generator Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `POST /api/signboard` — renders text as a letterboard-style lightbox sign PNG (raw PNG response), or posts the generated image to Bluesky/Mastodon/LinkedIn when `platforms` is supplied (JSON response).

**Architecture:** Follows the established REPR + Core/Infrastructure split: `ISignboardGenerator` (Core interface) with `SkiaSignboardGenerator` (Infrastructure, SkiaSharp + embedded font), `SignboardPostService` (Core, wraps `SocialPostService` exactly like `NasaGibsPostService`), and a FastEndpoints feature folder `Features/Signboard` with request/validator/response/endpoint.

**Tech Stack:** .NET 10, FastEndpoints, FluentValidation, SkiaSharp 4.150.1 (already referenced), xUnit + NSubstitute + Shouldly.

**Spec:** [spec.md](spec.md)

## Global Constraints

- Central Package Management: no new NuGet packages are needed; do NOT add any.
- `TreatWarningsAsErrors=true` — code must compile with zero warnings.
- C# files: file-scoped namespaces, tabs for indentation, CRLF, UTF-8 BOM, Allman braces.
- Primary constructors assigned to readonly fields; `Async` suffix on async methods; interfaces in Core, implementations in Infrastructure.
- Tests: xUnit, NSubstitute, Shouldly. Class naming `ClassName_MethodName_Tests`, method naming `DoesSomething_GivenSomeCondition`. Arrange-Act-Assert separated by blank lines only.
- Text limit: 200 chars. Dimensions: 400–2000 px, defaults 1200×900. Supported characters: A–Z (either case), 0–9, space, newline, `! ? . , ' " & @ # $ % - + / : ;`.
- New font asset: `Anton-Regular.ttf` (SIL OFL 1.1) from the Google Fonts repo, embedded resource in Infrastructure.
- Run all commands from repo root `C:\projects\BarretApi`.

---

### Task 1: Core contracts — character set, command, result, interface

**Files:**
- Create: `src/BarretApi.Core/Models/SignboardGenerationCommand.cs`
- Create: `src/BarretApi.Core/Models/SignboardPostResult.cs`
- Create: `src/BarretApi.Core/Services/SignboardCharacterSet.cs`
- Create: `src/BarretApi.Core/Interfaces/ISignboardGenerator.cs`
- Test: `tests/BarretApi.Core.UnitTests/Services/SignboardCharacterSet_FindUnsupported_Tests.cs`

**Interfaces:**
- Consumes: nothing (foundation task).
- Produces:
  - `SignboardGenerationCommand(string Text, int Width, int Height, int Seed)` — positional record.
  - `SignboardPostResult(int Width, int Height, int Seed, bool ImageAttached, IReadOnlyList<PlatformPostResult> PlatformResults)` — positional record.
  - `SignboardCharacterSet.FindUnsupported(string text)` returning `IReadOnlyList<char>` (distinct, in first-seen order).
  - `ISignboardGenerator.GenerateAsync(SignboardGenerationCommand command, CancellationToken cancellationToken = default)` returning `Task<byte[]>`.

- [ ] **Step 1: Write the failing test**

Create `tests/BarretApi.Core.UnitTests/Services/SignboardCharacterSet_FindUnsupported_Tests.cs`:

```csharp
using BarretApi.Core.Services;
using Shouldly;

namespace BarretApi.Core.UnitTests.Services;

public sealed class SignboardCharacterSet_FindUnsupported_Tests
{
	[Theory]
	[InlineData("HELLO WORLD")]
	[InlineData("hello world")]
	[InlineData("ABC123")]
	[InlineData("WOW! REALLY? YES.")]
	[InlineData("IT'S \"FINE\", OK & DONE @ 5% + #1 $9 - A/B: C;")]
	[InlineData("LINE ONE\nLINE TWO")]
	[InlineData("CRLF\r\nTEXT")]
	public void ReturnsEmpty_GivenOnlySupportedCharacters(string text)
	{
		var result = SignboardCharacterSet.FindUnsupported(text);

		result.ShouldBeEmpty();
	}

	[Theory]
	[InlineData("HELLO_WORLD", '_')]
	[InlineData("CAFÉ", 'É')]
	[InlineData("TAB\tHERE", '\t')]
	[InlineData("(PARENS)", '(')]
	public void ReturnsOffendingCharacter_GivenUnsupportedCharacter(string text, char expected)
	{
		var result = SignboardCharacterSet.FindUnsupported(text);

		result.ShouldContain(expected);
	}

	[Fact]
	public void ReturnsOffendingCharacters_GivenEmoji()
	{
		var result = SignboardCharacterSet.FindUnsupported("EMOJI \U0001F600");

		result.Count.ShouldBe(2);
	}

	[Fact]
	public void ReturnsDistinctCharactersInFirstSeenOrder_GivenRepeatedUnsupportedCharacters()
	{
		var result = SignboardCharacterSet.FindUnsupported("A_B_C~D~");

		result.ShouldBe(new[] { '_', '~' });
	}
}
```

(The emoji is a surrogate pair, so both halves register as unsupported chars — that is fine; the validator only needs a non-empty result to reject.)

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/BarretApi.Core.UnitTests --filter "FullyQualifiedName~SignboardCharacterSet" 2>&1 | tail -5`
Expected: build FAILURE — `SignboardCharacterSet` does not exist.

- [ ] **Step 3: Write the implementation**

Create `src/BarretApi.Core/Services/SignboardCharacterSet.cs`:

```csharp
namespace BarretApi.Core.Services;

/// <summary>
/// Defines the set of characters that can be rendered as letterboard tiles.
/// Shared by request validation and the signboard generator.
/// </summary>
public static class SignboardCharacterSet
{
	public const string SupportedPunctuation = "!?.,'\"&@#$%-+/:;";

	private static readonly HashSet<char> Supported = BuildSupportedSet();

	public static IReadOnlyList<char> FindUnsupported(string text)
	{
		ArgumentNullException.ThrowIfNull(text);

		var unsupported = new List<char>();
		foreach (var c in text)
		{
			if (!Supported.Contains(c) && !unsupported.Contains(c))
			{
				unsupported.Add(c);
			}
		}

		return unsupported;
	}

	private static HashSet<char> BuildSupportedSet()
	{
		var set = new HashSet<char> { ' ', '\n', '\r' };

		for (var c = 'A'; c <= 'Z'; c++)
		{
			set.Add(c);
		}

		for (var c = 'a'; c <= 'z'; c++)
		{
			set.Add(c);
		}

		for (var c = '0'; c <= '9'; c++)
		{
			set.Add(c);
		}

		foreach (var c in SupportedPunctuation)
		{
			set.Add(c);
		}

		return set;
	}
}
```

Create `src/BarretApi.Core/Models/SignboardGenerationCommand.cs`:

```csharp
namespace BarretApi.Core.Models;

/// <summary>
/// Input for signboard image generation. Text is rendered uppercase;
/// Seed drives deterministic accent-letter placement and jitter.
/// </summary>
public sealed record SignboardGenerationCommand(
	string Text,
	int Width,
	int Height,
	int Seed);
```

Create `src/BarretApi.Core/Models/SignboardPostResult.cs`:

```csharp
namespace BarretApi.Core.Models;

public sealed record SignboardPostResult(
	int Width,
	int Height,
	int Seed,
	bool ImageAttached,
	IReadOnlyList<PlatformPostResult> PlatformResults);
```

Create `src/BarretApi.Core/Interfaces/ISignboardGenerator.cs`:

```csharp
using BarretApi.Core.Models;

namespace BarretApi.Core.Interfaces;

/// <summary>
/// Generates letterboard-style signboard PNG images from text.
/// </summary>
public interface ISignboardGenerator
{
	Task<byte[]> GenerateAsync(SignboardGenerationCommand command, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/BarretApi.Core.UnitTests --filter "FullyQualifiedName~SignboardCharacterSet" 2>&1 | tail -5`
Expected: PASS (all tests green).

- [ ] **Step 5: Commit**

```bash
git add src/BarretApi.Core tests/BarretApi.Core.UnitTests/Services/SignboardCharacterSet_FindUnsupported_Tests.cs
git commit -m "feat: add signboard core contracts and character set"
```

---

### Task 2: Font asset + SkiaSignboardGenerator

**Files:**
- Create: `src/BarretApi.Infrastructure/Fonts/Anton-Regular.ttf` (downloaded — see Step 1)
- Modify: `src/BarretApi.Infrastructure/BarretApi.Infrastructure.csproj` (add EmbeddedResource)
- Create: `src/BarretApi.Infrastructure/Services/SkiaSignboardGenerator.cs`
- Test: `tests/BarretApi.Infrastructure.UnitTests/Services/SkiaSignboardGenerator_GenerateAsync_Tests.cs`

**Interfaces:**
- Consumes: `ISignboardGenerator`, `SignboardGenerationCommand` (Task 1).
- Produces: `SkiaSignboardGenerator(ILogger<SkiaSignboardGenerator> logger) : ISignboardGenerator` — registered in DI in Task 4.

- [ ] **Step 1: Download the font and embed it**

Anton (SIL OFL 1.1, single Regular weight, ~165 KB) from the official Google Fonts repository:

```bash
curl -L -o src/BarretApi.Infrastructure/Fonts/Anton-Regular.ttf https://github.com/google/fonts/raw/main/ofl/anton/Anton-Regular.ttf
```

Verify it downloaded as a real TTF (should print `TrueType` info, size roughly 160–170 KB):

```bash
ls -la src/BarretApi.Infrastructure/Fonts/Anton-Regular.ttf
```

In `src/BarretApi.Infrastructure/BarretApi.Infrastructure.csproj`, extend the existing EmbeddedResource ItemGroup:

```xml
	<ItemGroup>
		<EmbeddedResource Include="Fonts\JetBrainsMono-Bold.ttf" />
		<EmbeddedResource Include="Fonts\JetBrainsMono-Regular.ttf" />
		<EmbeddedResource Include="Fonts\Anton-Regular.ttf" />
	</ItemGroup>
```

- [ ] **Step 2: Write the failing tests**

Create `tests/BarretApi.Infrastructure.UnitTests/Services/SkiaSignboardGenerator_GenerateAsync_Tests.cs`:

```csharp
using BarretApi.Core.Models;
using BarretApi.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using SkiaSharp;

namespace BarretApi.Infrastructure.UnitTests.Services;

public sealed class SkiaSignboardGenerator_GenerateAsync_Tests
{
	private readonly SkiaSignboardGenerator _sut = new(
		Substitute.For<ILogger<SkiaSignboardGenerator>>());

	[Fact]
	public async Task ReturnsValidPngOfRequestedDimensions_GivenSimpleText()
	{
		var command = new SignboardGenerationCommand("HELLO WORLD", 800, 600, 42);

		var result = await _sut.GenerateAsync(command);

		result.Length.ShouldBeGreaterThan(0);
		IsPng(result).ShouldBeTrue();
		using var bitmap = SKBitmap.Decode(result);
		bitmap.Width.ShouldBe(800);
		bitmap.Height.ShouldBe(600);
	}

	[Fact]
	public async Task ReturnsByteIdenticalOutput_GivenSameTextAndSeed()
	{
		var command = new SignboardGenerationCommand("SAME SEED SAME SIGN", 600, 450, 1234);

		var first = await _sut.GenerateAsync(command);
		var second = await _sut.GenerateAsync(command);

		second.ShouldBe(first);
	}

	[Fact]
	public async Task ReturnsDifferentOutput_GivenDifferentSeeds()
	{
		var first = await _sut.GenerateAsync(new SignboardGenerationCommand("DIFFERENT SEEDS", 600, 450, 1));
		var second = await _sut.GenerateAsync(new SignboardGenerationCommand("DIFFERENT SEEDS", 600, 450, 2));

		second.ShouldNotBe(first);
	}

	[Fact]
	public async Task RendersFrameAndBoard_GivenAnyText()
	{
		var command = new SignboardGenerationCommand("A", 400, 400, 7);

		var result = await _sut.GenerateAsync(command);

		using var bitmap = SKBitmap.Decode(result);
		var corner = bitmap.GetPixel(2, 2);
		var offCenter = bitmap.GetPixel(bitmap.Width / 4, bitmap.Height / 2);
		corner.Red.ShouldBeLessThan((byte)80);
		offCenter.Red.ShouldBeGreaterThan((byte)180);
	}

	[Fact]
	public async Task HonorsExplicitNewlines_GivenMultiLineText()
	{
		var singleLine = await _sut.GenerateAsync(new SignboardGenerationCommand("AB", 600, 450, 5));
		var twoLines = await _sut.GenerateAsync(new SignboardGenerationCommand("A\nB", 600, 450, 5));

		twoLines.ShouldNotBe(singleLine);
	}

	[Fact]
	public async Task RendersLowercaseAsUppercase_GivenLowercaseText()
	{
		var lower = await _sut.GenerateAsync(new SignboardGenerationCommand("hello", 600, 450, 9));
		var upper = await _sut.GenerateAsync(new SignboardGenerationCommand("HELLO", 600, 450, 9));

		lower.ShouldBe(upper);
	}

	[Fact]
	public async Task Throws_GivenEmptyText()
	{
		var command = new SignboardGenerationCommand("   ", 600, 450, 1);

		await Should.ThrowAsync<ArgumentException>(() => _sut.GenerateAsync(command));
	}

	[Fact]
	public async Task ReturnsValidPng_GivenLongTextThatMustWrapAndShrink()
	{
		var text = "I USED TO THINK I WAS INDECISIVE BUT NOW I'M NOT SURE ABOUT ANYTHING AT ALL ANYMORE TODAY";
		var command = new SignboardGenerationCommand(text, 400, 400, 3);

		var result = await _sut.GenerateAsync(command);

		IsPng(result).ShouldBeTrue();
		using var bitmap = SKBitmap.Decode(result);
		bitmap.Width.ShouldBe(400);
		bitmap.Height.ShouldBe(400);
	}

	private static bool IsPng(byte[] bytes)
	{
		return bytes.Length > 8
			&& bytes[0] == 0x89 && bytes[1] == 0x50
			&& bytes[2] == 0x4E && bytes[3] == 0x47;
	}
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test tests/BarretApi.Infrastructure.UnitTests --filter "FullyQualifiedName~SkiaSignboardGenerator" 2>&1 | tail -5`
Expected: build FAILURE — `SkiaSignboardGenerator` does not exist.

- [ ] **Step 4: Write the implementation**

Create `src/BarretApi.Infrastructure/Services/SkiaSignboardGenerator.cs`. Model resource loading on `SkiaHeroImageGenerator` in the same folder.

```csharp
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
	private const float MinFontSize = 10f;
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

		var slotCount = Math.Max(lines.Count, (int)(textArea.Height / slotHeight));
		var gridTop = textArea.MidY - slotCount * slotHeight / 2f;

		DrawTrackLines(canvas, boardFace, gridTop, slotHeight, slotCount);
		DrawLetterRows(canvas, lines, typeface, fontSize, gridTop, slotHeight, slotCount, textArea, random);
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
				var candidate = current.Length == 0 ? word : $"{current} {word}";

				if (MeasureLine(candidate, font) <= maxWidth || current.Length == 0)
				{
					current = candidate;
					continue;
				}

				lines.Add(current);
				current = word;
			}

			if (current.Length > 0)
			{
				lines.Add(current);
			}
		}

		return lines;
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

					canvas.DrawText(glyph, x, baseline + offsetY, font, letterPaint);
					canvas.Restore();
				}

				x += advance;
			}
		}
	}

	private static void DrawInnerShadow(SKCanvas canvas, SKRect boardFace, float frame)
	{
		using var shadowPaint = new SKPaint
		{
			Color = new SKColor(0x00, 0x00, 0x00, 40),
			IsAntialias = true,
			IsStroke = true,
			StrokeWidth = frame * 0.9f,
			MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, frame * 0.6f)
		};

		canvas.Save();
		canvas.ClipRoundRect(new SKRoundRect(boardFace, frame * 0.35f), antialias: true);
		canvas.DrawRoundRect(boardFace, frame * 0.35f, frame * 0.35f, shadowPaint);
		canvas.Restore();
	}
}
```

Implementation notes for the engineer:

- `random` is consumed in strict draw order (per non-whitespace letter: one `Next` + two `NextSingle` calls), which is what makes same-seed output byte-identical. Never add a `random` call whose execution depends on anything non-deterministic.
- If `font.MeasureText(string)` does not exist under SkiaSharp 4.x, the overload is `font.MeasureText(ReadOnlySpan<char>)` — check `SkiaWordCloudGenerator.cs`/`SkiaHeroImageGenerator.cs` in the same folder for the measuring API they use and match it.
- If `ClipRoundRect` has a different signature, `canvas.ClipRoundRect(roundRect, SKClipOperation.Intersect, true)` is the long form.

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/BarretApi.Infrastructure.UnitTests --filter "FullyQualifiedName~SkiaSignboardGenerator" 2>&1 | tail -5`
Expected: PASS (8 tests).

If `RendersFrameAndBoard_GivenAnyText` fails on the off-center pixel: the sample point may be hitting a track line or letter tile. Adjust the sample point to `bitmap.GetPixel(bitmap.Width / 2, (int)(bitmap.Height * 0.12f))` (inside the board face, above the text grid) rather than weakening the assertion.

- [ ] **Step 6: Generate a visual sample for manual inspection**

Write a throwaway xUnit fact or a quick script is NOT needed — instead run this one-liner test addition temporarily? No. Do this: create `scratch/signboard-sample.csx`? No — simplest reliable route: add a temporary test that writes the PNG to disk, run it, view the file, then delete the test before committing:

```csharp
	[Fact]
	public async Task WriteSampleToDisk_TemporaryVisualCheck()
	{
		var command = new SignboardGenerationCommand(
			"I USED TO THINK\nI WAS INDECISIVE\nBUT NOW\nI'M NOT SURE", 1200, 900, 42);

		var result = await _sut.GenerateAsync(command);

		await File.WriteAllBytesAsync(Path.Combine(Path.GetTempPath(), "signboard-sample.png"), result);
	}
```

Run it, open `%TEMP%\signboard-sample.png`, confirm: dark frame, white board, track lines, centered uppercase lines, a few red letters, slight letter jitter. **Delete this test method afterwards.** If the look is off (letters overlapping rails, tiles too visible, accents too frequent), tune the constants (`RowSpacingFactor`, tile alpha values, the `colorRoll` thresholds) and re-check.

- [ ] **Step 7: Commit**

```bash
git add src/BarretApi.Infrastructure tests/BarretApi.Infrastructure.UnitTests/Services/SkiaSignboardGenerator_GenerateAsync_Tests.cs
git commit -m "feat: add Skia signboard generator with embedded Anton font"
```

---

### Task 3: SignboardPostService (Core)

**Files:**
- Create: `src/BarretApi.Core/Services/SignboardPostService.cs`
- Test: `tests/BarretApi.Core.UnitTests/Services/SignboardPostService_PostAsync_Tests.cs`

**Interfaces:**
- Consumes: `ISignboardGenerator`, `SignboardGenerationCommand`, `SignboardPostResult` (Task 1); existing `SocialPostService`, `SocialPost`, `ImageData`, `PlatformPostResult`.
- Produces:

```csharp
public virtual Task<SignboardPostResult> PostAsync(
	SignboardGenerationCommand command,
	string? caption,
	IReadOnlyList<string> hashtags,
	string? altText,
	IReadOnlyList<string> platforms,
	CancellationToken cancellationToken = default)
```

(`virtual` so the endpoint tests in Task 4 can substitute it, matching `NasaGibsPostService`.)

- [ ] **Step 1: Write the failing tests**

Create `tests/BarretApi.Core.UnitTests/Services/SignboardPostService_PostAsync_Tests.cs`. Mirror the fixture style of `NasaGibsPostService_PostAsync_Tests.cs` (real `SocialPostService` over substituted `ISocialPlatformClient`s):

```csharp
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Core.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace BarretApi.Core.UnitTests.Services;

public sealed class SignboardPostService_PostAsync_Tests
{
	private static readonly byte[] FakePng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01];

	private readonly ISignboardGenerator _generator = Substitute.For<ISignboardGenerator>();
	private readonly ISocialPlatformClient _blueskyClient;
	private readonly SignboardPostService _sut;

	public SignboardPostService_PostAsync_Tests()
	{
		_blueskyClient = Substitute.For<ISocialPlatformClient>();
		_blueskyClient.PlatformName.Returns("bluesky");
		_blueskyClient.MaxTextLength.Returns(300);
		_blueskyClient.MaxImageSizeBytes.Returns(1_048_576L);
		_blueskyClient.PostAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<ImageData>>(), Arg.Any<CancellationToken>())
			.Returns(new PlatformPostResult { Platform = "bluesky", Success = true, PostId = "post-1" });

		var textShorteningService = Substitute.For<ITextShorteningService>();
		textShorteningService.Shorten(Arg.Any<string>(), Arg.Any<int>())
			.Returns(callInfo => callInfo.Arg<string>());

		var hashtagService = Substitute.For<IHashtagService>();
		hashtagService.ProcessHashtags(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>())
			.Returns(callInfo => new HashtagProcessingResult
			{
				FinalText = callInfo.Arg<string>()!,
				AllHashtags = []
			});

		var imageResizer = Substitute.For<IImageResizer>();
		imageResizer.ResizeToFit(Arg.Any<byte[]>(), Arg.Any<long>())
			.Returns(callInfo => callInfo.Arg<byte[]>());

		var socialPostService = new SocialPostService(
			[_blueskyClient],
			textShorteningService,
			Substitute.For<ITextSplitterService>(),
			Substitute.For<IImageDownloadService>(),
			imageResizer,
			hashtagService,
			Substitute.For<ILogger<SocialPostService>>());

		_generator.GenerateAsync(Arg.Any<SignboardGenerationCommand>(), Arg.Any<CancellationToken>())
			.Returns(FakePng);

		_sut = new SignboardPostService(
			_generator,
			socialPostService,
			Substitute.For<ILogger<SignboardPostService>>());
	}

	[Fact]
	public async Task GeneratesImageWithSuppliedCommand_GivenValidRequest()
	{
		var command = new SignboardGenerationCommand("HELLO", 1200, 900, 42);

		var result = await _sut.PostAsync(command, null, [], null, ["bluesky"]);

		await _generator.Received(1).GenerateAsync(command, Arg.Any<CancellationToken>());
		result.Width.ShouldBe(1200);
		result.Height.ShouldBe(900);
		result.Seed.ShouldBe(42);
		result.ImageAttached.ShouldBeTrue();
	}

	[Fact]
	public async Task UsesSignTextAsCaption_GivenNoCaption()
	{
		var command = new SignboardGenerationCommand("HELLO WORLD", 1200, 900, 1);

		await _sut.PostAsync(command, null, [], null, ["bluesky"]);

		await _blueskyClient.Received(1).PostAsync(
			Arg.Is<string>(t => t.Contains("HELLO WORLD")),
			Arg.Any<IReadOnlyList<ImageData>>(),
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task UsesSuppliedCaption_GivenCaption()
	{
		var command = new SignboardGenerationCommand("HELLO", 1200, 900, 1);

		await _sut.PostAsync(command, "Custom caption here", [], null, ["bluesky"]);

		await _blueskyClient.Received(1).PostAsync(
			Arg.Is<string>(t => t.Contains("Custom caption here")),
			Arg.Any<IReadOnlyList<ImageData>>(),
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task AttachesPngWithDefaultAltText_GivenNoAltText()
	{
		var command = new SignboardGenerationCommand("LINE ONE\nLINE TWO", 1200, 900, 1);

		await _sut.PostAsync(command, null, [], null, ["bluesky"]);

		await _blueskyClient.Received(1).PostAsync(
			Arg.Any<string>(),
			Arg.Is<IReadOnlyList<ImageData>>(images =>
				images.Count == 1
				&& images[0].ContentType == "image/png"
				&& images[0].Content == FakePng
				&& images[0].AltText == "Letterboard sign reading: LINE ONE LINE TWO"),
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task UsesSuppliedAltText_GivenAltText()
	{
		var command = new SignboardGenerationCommand("HELLO", 1200, 900, 1);

		await _sut.PostAsync(command, null, [], "My alt text", ["bluesky"]);

		await _blueskyClient.Received(1).PostAsync(
			Arg.Any<string>(),
			Arg.Is<IReadOnlyList<ImageData>>(images => images[0].AltText == "My alt text"),
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task ReturnsPlatformResults_GivenSuccessfulPost()
	{
		var command = new SignboardGenerationCommand("HELLO", 1200, 900, 1);

		var result = await _sut.PostAsync(command, null, [], null, ["bluesky"]);

		result.PlatformResults.Count.ShouldBe(1);
		result.PlatformResults[0].Platform.ShouldBe("bluesky");
		result.PlatformResults[0].Success.ShouldBeTrue();
	}
}
```

**Important:** before finalizing this test, open `src/BarretApi.Core/Interfaces/ISocialPlatformClient.cs` and check the actual member names/signature (`PlatformName`, `MaxTextLength`, `MaxImageSizeBytes`, `PostAsync(...)`) plus how `NasaGibsPostService_PostAsync_Tests.CreateMockClient` sets up its substitutes — copy that helper's exact setup calls rather than the guesses above if they differ. The alt-text default uses uppercase text because the service uppercases before building alt text (see implementation below).

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/BarretApi.Core.UnitTests --filter "FullyQualifiedName~SignboardPostService" 2>&1 | tail -5`
Expected: build FAILURE — `SignboardPostService` does not exist.

- [ ] **Step 3: Write the implementation**

Create `src/BarretApi.Core/Services/SignboardPostService.cs`:

```csharp
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using Microsoft.Extensions.Logging;

namespace BarretApi.Core.Services;

/// <summary>
/// Generates a signboard image and posts it to social platforms,
/// following the same orchestration shape as NasaGibsPostService.
/// </summary>
public class SignboardPostService(
	ISignboardGenerator signboardGenerator,
	SocialPostService socialPostService,
	ILogger<SignboardPostService> logger)
{
	private readonly ISignboardGenerator _signboardGenerator = signboardGenerator;
	private readonly SocialPostService _socialPostService = socialPostService;
	private readonly ILogger<SignboardPostService> _logger = logger;

	public virtual async Task<SignboardPostResult> PostAsync(
		SignboardGenerationCommand command,
		string? caption,
		IReadOnlyList<string> hashtags,
		string? altText,
		IReadOnlyList<string> platforms,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(command);

		var imageBytes = await _signboardGenerator.GenerateAsync(command, cancellationToken);

		var displayText = command.Text.ToUpperInvariant().ReplaceLineEndings(" ");
		var resolvedCaption = string.IsNullOrWhiteSpace(caption) ? command.Text : caption;
		var resolvedAltText = string.IsNullOrWhiteSpace(altText)
			? $"Letterboard sign reading: {displayText}"
			: altText;

		_logger.LogInformation(
			"Posting signboard to {PlatformCount} platform(s), image size: {ImageSize} bytes",
			platforms.Count == 0 ? "all" : platforms.Count,
			imageBytes.Length);

		var socialPost = new SocialPost
		{
			Text = resolvedCaption,
			Hashtags = hashtags,
			Images =
			[
				new ImageData
				{
					Content = imageBytes,
					ContentType = "image/png",
					AltText = resolvedAltText,
					FileName = "signboard.png"
				}
			],
			TargetPlatforms = platforms.ToList()
		};

		var platformResults = await _socialPostService.PostAsync(socialPost, cancellationToken);

		return new SignboardPostResult(
			Width: command.Width,
			Height: command.Height,
			Seed: command.Seed,
			ImageAttached: true,
			PlatformResults: platformResults);
	}
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/BarretApi.Core.UnitTests --filter "FullyQualifiedName~SignboardPostService" 2>&1 | tail -5`
Expected: PASS (6 tests).

- [ ] **Step 5: Commit**

```bash
git add src/BarretApi.Core/Services/SignboardPostService.cs tests/BarretApi.Core.UnitTests/Services/SignboardPostService_PostAsync_Tests.cs
git commit -m "feat: add signboard post service"
```

---

### Task 4: API endpoint, validator, DI registration

**Files:**
- Create: `src/BarretApi.Api/Features/Signboard/GenerateSignboardRequest.cs`
- Create: `src/BarretApi.Api/Features/Signboard/GenerateSignboardResponse.cs`
- Create: `src/BarretApi.Api/Features/Signboard/GenerateSignboardValidator.cs`
- Create: `src/BarretApi.Api/Features/Signboard/GenerateSignboardEndpoint.cs`
- Modify: `src/BarretApi.Api/Program.cs` (two DI registrations, near the word-cloud registrations at ~line 194)
- Test: `tests/BarretApi.Api.UnitTests/Features/Signboard/GenerateSignboardValidator_Tests.cs`
- Test: `tests/BarretApi.Api.UnitTests/Features/Signboard/GenerateSignboardEndpoint_HandleAsync_Tests.cs`

**Interfaces:**
- Consumes: `ISignboardGenerator`, `SignboardGenerationCommand` (Task 1), `SignboardPostService.PostAsync(command, caption, hashtags, altText, platforms, ct)` (Task 3), existing `PlatformResult` DTO from `BarretApi.Api.Features.SocialPost` (`CreateSocialPostResponse.cs`).
- Produces: `POST /api/signboard` route.

- [ ] **Step 1: Write the failing validator tests**

Create `tests/BarretApi.Api.UnitTests/Features/Signboard/GenerateSignboardValidator_Tests.cs` (mirror assertion style of existing validator tests, e.g. `GenerateWordCloudValidator_Tests.cs` — open it and copy how it invokes the validator and asserts errors; typical shape below):

```csharp
using BarretApi.Api.Features.Signboard;
using FluentValidation.TestHelper;
using Shouldly;

namespace BarretApi.Api.UnitTests.Features.Signboard;

public sealed class GenerateSignboardValidator_Tests
{
	private readonly GenerateSignboardValidator _sut = new();

	[Fact]
	public void Passes_GivenMinimalValidRequest()
	{
		var request = new GenerateSignboardRequest { Text = "HELLO WORLD" };

		var result = _sut.TestValidate(request);

		result.IsValid.ShouldBeTrue();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void Fails_GivenMissingText(string? text)
	{
		var request = new GenerateSignboardRequest { Text = text };

		var result = _sut.TestValidate(request);

		result.ShouldHaveValidationErrorFor(x => x.Text);
	}

	[Fact]
	public void Fails_GivenTextOver200Characters()
	{
		var request = new GenerateSignboardRequest { Text = new string('A', 201) };

		var result = _sut.TestValidate(request);

		result.ShouldHaveValidationErrorFor(x => x.Text);
	}

	[Fact]
	public void Fails_GivenUnsupportedCharacters()
	{
		var request = new GenerateSignboardRequest { Text = "HELLO_WORLD~" };

		var result = _sut.TestValidate(request);

		result.ShouldHaveValidationErrorFor(x => x.Text);
		result.Errors.ShouldContain(e => e.ErrorMessage.Contains("'_'") && e.ErrorMessage.Contains("'~'"));
	}

	[Theory]
	[InlineData(399)]
	[InlineData(2001)]
	public void Fails_GivenWidthOutOfRange(int width)
	{
		var request = new GenerateSignboardRequest { Text = "HI", Width = width };

		var result = _sut.TestValidate(request);

		result.ShouldHaveValidationErrorFor(x => x.Width);
	}

	[Theory]
	[InlineData(399)]
	[InlineData(2001)]
	public void Fails_GivenHeightOutOfRange(int height)
	{
		var request = new GenerateSignboardRequest { Text = "HI", Height = height };

		var result = _sut.TestValidate(request);

		result.ShouldHaveValidationErrorFor(x => x.Height);
	}

	[Fact]
	public void Fails_GivenUnknownPlatform()
	{
		var request = new GenerateSignboardRequest { Text = "HI", Platforms = ["myspace"] };

		var result = _sut.TestValidate(request);

		result.ShouldHaveValidationErrorFor(x => x.Platforms);
	}

	[Fact]
	public void Passes_GivenAllKnownPlatformsAnyCase()
	{
		var request = new GenerateSignboardRequest { Text = "HI", Platforms = ["Bluesky", "MASTODON", "linkedin"] };

		var result = _sut.TestValidate(request);

		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public void Fails_GivenCaptionOver1000Characters()
	{
		var request = new GenerateSignboardRequest { Text = "HI", Caption = new string('a', 1001) };

		var result = _sut.TestValidate(request);

		result.ShouldHaveValidationErrorFor(x => x.Caption);
	}

	[Fact]
	public void Fails_GivenAltTextOver1500Characters()
	{
		var request = new GenerateSignboardRequest { Text = "HI", AltText = new string('a', 1501) };

		var result = _sut.TestValidate(request);

		result.ShouldHaveValidationErrorFor(x => x.AltText);
	}

	[Fact]
	public void Fails_GivenHashtagWithSpaces()
	{
		var request = new GenerateSignboardRequest { Text = "HI", Hashtags = ["has space"] };

		var result = _sut.TestValidate(request);

		result.ShouldHaveValidationErrorFor(x => x.Hashtags);
	}
}
```

If `FluentValidation.TestHelper` is not what existing validator tests use, mirror whatever they do (e.g. constructing `Validator.Validate(request)` and checking `result.Errors`) — consistency with the neighboring test files wins.

- [ ] **Step 2: Write the failing endpoint tests**

Create `tests/BarretApi.Api.UnitTests/Features/Signboard/GenerateSignboardEndpoint_HandleAsync_Tests.cs` (fixture mirrors `SatellitePostEndpoint_HandleAsync_Tests.cs`):

```csharp
using BarretApi.Api.Features.Signboard;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Core.Services;
using FastEndpoints;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace BarretApi.Api.UnitTests.Features.Signboard;

public sealed class GenerateSignboardEndpoint_HandleAsync_Tests
{
	private readonly ISignboardGenerator _generator = Substitute.For<ISignboardGenerator>();
	private readonly SignboardPostService _postService;
	private readonly ILogger<GenerateSignboardEndpoint> _logger = Substitute.For<ILogger<GenerateSignboardEndpoint>>();

	private static readonly byte[] FakePng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01];

	public GenerateSignboardEndpoint_HandleAsync_Tests()
	{
		var socialPostService = new SocialPostService(
			Array.Empty<ISocialPlatformClient>(),
			Substitute.For<ITextShorteningService>(),
			Substitute.For<ITextSplitterService>(),
			Substitute.For<IImageDownloadService>(),
			Substitute.For<IImageResizer>(),
			Substitute.For<IHashtagService>(),
			Substitute.For<ILogger<SocialPostService>>());

		_postService = Substitute.For<SignboardPostService>(
			_generator,
			socialPostService,
			Substitute.For<ILogger<SignboardPostService>>());

		_generator.GenerateAsync(Arg.Any<SignboardGenerationCommand>(), Arg.Any<CancellationToken>())
			.Returns(FakePng);
	}

	[Fact]
	public async Task ReturnsPngBytes_GivenNoPlatforms()
	{
		var ep = Factory.Create<GenerateSignboardEndpoint>(_generator, _postService, _logger);
		var req = new GenerateSignboardRequest { Text = "HELLO", Seed = 42 };

		await ep.HandleAsync(req, default);

		ep.HttpContext.Response.ContentType.ShouldBe("image/png");
		ep.HttpContext.Response.Headers["X-Signboard-Seed"].ToString().ShouldBe("42");
		await _generator.Received(1).GenerateAsync(
			Arg.Is<SignboardGenerationCommand>(c =>
				c.Text == "HELLO" && c.Width == 1200 && c.Height == 900 && c.Seed == 42),
			Arg.Any<CancellationToken>());
		await _postService.DidNotReceiveWithAnyArgs().PostAsync(default!, default, default!, default, default!, default);
	}

	[Fact]
	public async Task PostsToPlatforms_GivenPlatformsSupplied()
	{
		_postService.PostAsync(
				Arg.Any<SignboardGenerationCommand>(),
				Arg.Any<string?>(),
				Arg.Any<IReadOnlyList<string>>(),
				Arg.Any<string?>(),
				Arg.Any<IReadOnlyList<string>>(),
				Arg.Any<CancellationToken>())
			.Returns(new SignboardPostResult(1200, 900, 42, true,
			[
				new PlatformPostResult { Platform = "bluesky", Success = true, PostId = "p1" }
			]));

		var ep = Factory.Create<GenerateSignboardEndpoint>(_generator, _postService, _logger);
		var req = new GenerateSignboardRequest
		{
			Text = "HELLO",
			Seed = 42,
			Platforms = ["bluesky"],
			Caption = "My caption",
			Hashtags = ["fun"]
		};

		await ep.HandleAsync(req, default);

		ep.Response.Seed.ShouldBe(42);
		ep.Response.Width.ShouldBe(1200);
		ep.Response.Height.ShouldBe(900);
		ep.Response.Results.Count.ShouldBe(1);
		ep.Response.Results[0].Platform.ShouldBe("bluesky");
		ep.Response.Results[0].Success.ShouldBeTrue();
		await _generator.DidNotReceiveWithAnyArgs().GenerateAsync(default!, default);
	}

	[Fact]
	public async Task MapsFailureDetails_GivenFailedPlatform()
	{
		_postService.PostAsync(
				Arg.Any<SignboardGenerationCommand>(),
				Arg.Any<string?>(),
				Arg.Any<IReadOnlyList<string>>(),
				Arg.Any<string?>(),
				Arg.Any<IReadOnlyList<string>>(),
				Arg.Any<CancellationToken>())
			.Returns(new SignboardPostResult(1200, 900, 1, true,
			[
				new PlatformPostResult
				{
					Platform = "mastodon",
					Success = false,
					ErrorMessage = "Rate limit exceeded",
					ErrorCode = "RATE_LIMITED"
				}
			]));

		var ep = Factory.Create<GenerateSignboardEndpoint>(_generator, _postService, _logger);
		var req = new GenerateSignboardRequest { Text = "HELLO", Platforms = ["mastodon"] };

		await ep.HandleAsync(req, default);

		ep.Response.Results[0].Success.ShouldBeFalse();
		ep.Response.Results[0].Error.ShouldBe("Rate limit exceeded");
		ep.Response.Results[0].ErrorCode.ShouldBe("RATE_LIMITED");
	}
}
```

Note on status codes: `Factory.Create` unit tests can't easily assert the numeric status code sent via `Send.ResponseAsync`; per-platform mapping plus the `DetermineStatusCode` helper being copied verbatim from `SatellitePostEndpoint` is the accepted level of coverage in this codebase (see the satellite tests).

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test tests/BarretApi.Api.UnitTests --filter "FullyQualifiedName~Signboard" 2>&1 | tail -5`
Expected: build FAILURE — feature classes do not exist.

- [ ] **Step 4: Write the implementation**

Create `src/BarretApi.Api/Features/Signboard/GenerateSignboardRequest.cs`:

```csharp
namespace BarretApi.Api.Features.Signboard;

public sealed class GenerateSignboardRequest
{
	public string? Text { get; init; }
	public int? Width { get; init; }
	public int? Height { get; init; }
	public int? Seed { get; init; }
	public List<string>? Platforms { get; init; }
	public string? Caption { get; init; }
	public List<string>? Hashtags { get; init; }
	public string? AltText { get; init; }
}
```

Create `src/BarretApi.Api/Features/Signboard/GenerateSignboardResponse.cs`:

```csharp
using BarretApi.Api.Features.SocialPost;

namespace BarretApi.Api.Features.Signboard;

public sealed class GenerateSignboardResponse
{
	public int Width { get; init; }
	public int Height { get; init; }
	public int Seed { get; init; }
	public List<PlatformResult> Results { get; init; } = [];
	public DateTimeOffset PostedAt { get; init; }
}
```

Create `src/BarretApi.Api/Features/Signboard/GenerateSignboardValidator.cs`:

```csharp
using BarretApi.Core.Services;
using FastEndpoints;
using FluentValidation;

namespace BarretApi.Api.Features.Signboard;

public sealed class GenerateSignboardValidator : Validator<GenerateSignboardRequest>
{
	public GenerateSignboardValidator()
	{
		RuleFor(x => x.Text)
			.Must(t => !string.IsNullOrWhiteSpace(t))
			.WithMessage("Text is required.");

		RuleFor(x => x.Text)
			.MaximumLength(200)
			.When(x => !string.IsNullOrWhiteSpace(x.Text))
			.WithMessage("Text must not exceed 200 characters.");

		RuleFor(x => x.Text)
			.Must(t => SignboardCharacterSet.FindUnsupported(t!).Count == 0)
			.When(x => !string.IsNullOrWhiteSpace(x.Text))
			.WithMessage(x =>
			{
				var unsupported = string.Join(" ", SignboardCharacterSet
					.FindUnsupported(x.Text!)
					.Select(c => $"'{c}'"));
				return $"Text contains unsupported characters: {unsupported}. "
					+ $"Supported: A-Z, 0-9, space, newline, and {SignboardCharacterSet.SupportedPunctuation}";
			});

		RuleFor(x => x.Width)
			.InclusiveBetween(400, 2000)
			.When(x => x.Width is not null)
			.WithMessage("Width must be between 400 and 2000.");

		RuleFor(x => x.Height)
			.InclusiveBetween(400, 2000)
			.When(x => x.Height is not null)
			.WithMessage("Height must be between 400 and 2000.");

		RuleFor(x => x.Platforms)
			.Must(platforms => platforms!.All(p =>
				p.Equals("bluesky", StringComparison.OrdinalIgnoreCase) ||
				p.Equals("mastodon", StringComparison.OrdinalIgnoreCase) ||
				p.Equals("linkedin", StringComparison.OrdinalIgnoreCase)))
			.When(x => x.Platforms is not null && x.Platforms.Count > 0)
			.WithMessage("Each platform must be one of: bluesky, mastodon, linkedin.");

		RuleFor(x => x.Caption)
			.MaximumLength(1000)
			.When(x => !string.IsNullOrWhiteSpace(x.Caption))
			.WithMessage("Caption must not exceed 1000 characters.");

		RuleFor(x => x.AltText)
			.MaximumLength(1500)
			.When(x => !string.IsNullOrWhiteSpace(x.AltText))
			.WithMessage("AltText must not exceed 1500 characters.");

		RuleForEach(x => x.Hashtags)
			.Must(h => !string.IsNullOrWhiteSpace(h) && !h.Contains(' ') && h.Length <= 100)
			.When(x => x.Hashtags is not null && x.Hashtags.Count > 0)
			.WithMessage("Each hashtag must be non-empty, contain no spaces, and not exceed 100 characters.");
	}
}
```

Create `src/BarretApi.Api/Features/Signboard/GenerateSignboardEndpoint.cs`:

```csharp
using BarretApi.Api.Features.SocialPost;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using BarretApi.Core.Services;
using FastEndpoints;
using Microsoft.Extensions.Logging;

namespace BarretApi.Api.Features.Signboard;

public sealed class GenerateSignboardEndpoint(
	ISignboardGenerator generator,
	SignboardPostService postService,
	ILogger<GenerateSignboardEndpoint> logger)
	: Endpoint<GenerateSignboardRequest, GenerateSignboardResponse>
{
	private readonly ISignboardGenerator _generator = generator;
	private readonly SignboardPostService _postService = postService;
	private readonly ILogger<GenerateSignboardEndpoint> _logger = logger;

	private const int DefaultWidth = 1200;
	private const int DefaultHeight = 900;

	public override void Configure()
	{
		Post("/api/signboard");

		Summary(s =>
		{
			s.Summary = "Generate a letterboard signboard image";
			s.Description = "Renders the supplied text as a letterboard-style lightbox sign PNG. "
				+ "Returns the raw PNG by default; when platforms are supplied, posts the image "
				+ "to the targeted social platforms and returns per-platform results as JSON.";
			s.ExampleRequest = new GenerateSignboardRequest
			{
				Text = "I USED TO THINK\nI WAS INDECISIVE\nBUT NOW\nI'M NOT SURE",
				Seed = 42
			};
			s.Responses[200] = "PNG image (no platforms) or all targeted platforms succeeded (JSON).";
			s.Responses[207] = "Partial success: at least one platform succeeded and at least one failed.";
			s.Responses[400] = "Request validation failed.";
			s.Responses[401] = "Missing or invalid X-Api-Key.";
			s.Responses[500] = "Unexpected error during image generation.";
			s.Responses[502] = "All targeted platforms failed to post.";
		});
	}

	public override async Task HandleAsync(GenerateSignboardRequest req, CancellationToken ct)
	{
		var seed = req.Seed ?? Random.Shared.Next();
		var command = new SignboardGenerationCommand(
			Text: req.Text!,
			Width: req.Width ?? DefaultWidth,
			Height: req.Height ?? DefaultHeight,
			Seed: seed);

		var platforms = req.Platforms ?? [];

		_logger.LogInformation(
			"Signboard request: {Width}x{Height}, seed {Seed}, posting: {Posting}",
			command.Width,
			command.Height,
			seed,
			platforms.Count > 0);

		if (platforms.Count == 0)
		{
			await GenerateOnlyAsync(command, ct);
			return;
		}

		var result = await _postService.PostAsync(
			command,
			req.Caption,
			req.Hashtags ?? [],
			req.AltText,
			platforms,
			ct);

		var response = new GenerateSignboardResponse
		{
			Width = result.Width,
			Height = result.Height,
			Seed = result.Seed,
			Results = result.PlatformResults.Select(r => new PlatformResult
			{
				Platform = r.Platform,
				Success = r.Success,
				PostId = r.PostId,
				PostUrl = r.PostUrl,
				ShortenedText = r.PublishedText,
				Error = r.Success ? null : r.ErrorMessage,
				ErrorCode = r.Success ? null : r.ErrorCode
			}).ToList(),
			PostedAt = DateTimeOffset.UtcNow
		};

		var statusCode = DetermineStatusCode(result.PlatformResults);
		_logger.LogInformation("Signboard post completed with status {StatusCode}", statusCode);

		await Send.ResponseAsync(response, statusCode, ct);
	}

	private async Task GenerateOnlyAsync(SignboardGenerationCommand command, CancellationToken ct)
	{
		byte[] imageBytes;
		try
		{
			imageBytes = await _generator.GenerateAsync(command, ct);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogError(ex, "Signboard generation failed");
			await Send.ResponseAsync(
				new { statusCode = 500, message = "An unexpected error occurred during image generation." },
				500,
				ct);
			return;
		}

		_logger.LogInformation("Signboard generated: {Size} bytes", imageBytes.Length);

		HttpContext.Response.Headers["X-Signboard-Seed"] = command.Seed.ToString();
		HttpContext.Response.ContentType = "image/png";
		HttpContext.Response.ContentLength = imageBytes.Length;
		await HttpContext.Response.Body.WriteAsync(imageBytes, ct);
	}

	private static int DetermineStatusCode(IReadOnlyList<PlatformPostResult> results)
	{
		if (results.All(r => r.Success))
		{
			return 200;
		}

		if (results.Any(r => r.Success))
		{
			return 207;
		}

		return 502;
	}
}
```

Check `PlatformResult`'s actual property names in `src/BarretApi.Api/Features/SocialPost/CreateSocialPostResponse.cs` (expected: `Platform`, `Success`, `PostId`, `PostUrl`, `ShortenedText`, `Error`, `ErrorCode` — the same mapping `SatellitePostEndpoint.BuildResponse` uses) and adjust if they differ.

In `src/BarretApi.Api/Program.cs`, immediately after the word-cloud registrations (`builder.Services.AddSingleton<IWordCloudGenerator, SkiaWordCloudGenerator>();` around line 194), add:

```csharp
builder.Services.AddSingleton<ISignboardGenerator, SkiaSignboardGenerator>();
builder.Services.AddSingleton<SignboardPostService>();
```

(`SkiaSignboardGenerator` needs a `using BarretApi.Infrastructure.Services;` — already present for the other Skia services.)

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/BarretApi.Api.UnitTests --filter "FullyQualifiedName~Signboard" 2>&1 | tail -5`
Expected: PASS (validator + endpoint tests).

- [ ] **Step 6: Full solution build and test**

Run: `dotnet build 2>&1 | tail -3` then `dotnet test 2>&1 | tail -10`
Expected: build succeeds with 0 warnings; all tests pass.

- [ ] **Step 7: Commit**

```bash
git add src/BarretApi.Api tests/BarretApi.Api.UnitTests/Features/Signboard
git commit -m "feat: add POST /api/signboard endpoint with optional social posting"
```

---

### Task 5: README documentation + smoke test

**Files:**
- Modify: `README.md` (TOC entry + new endpoint section, placed after the word-cloud section to match TOC ordering)

**Interfaces:**
- Consumes: final request/response shapes from Task 4.

- [ ] **Step 1: Add TOC entry**

In the README Table of Contents, after the `POST /api/word-cloud` line, add:

```markdown
  - [POST /api/signboard — Generate Signboard Image](#post-apisignboard--generate-signboard-image)
```

- [ ] **Step 2: Add the endpoint section**

Insert after the `POST /api/word-cloud` section (before `GET /api/avatars/random`), matching the README's established formatting:

```markdown
### POST /api/signboard — Generate Signboard Image

Renders text as a letterboard-style lightbox sign (white board, dark frame, tile letters with occasional red accents) and returns it as a PNG. When `platforms` is supplied, the image is instead posted to the targeted social platforms and a JSON result is returned.

| Detail | Value |
|---|---|
| **Auth** | `X-Api-Key` header |
| **Content-Type** | `application/json` |
| **Response Content-Type** | `image/png` (generate-only) or `application/json` (posting mode) |

#### Request Body

| Field | Type | Required | Default | Description |
|---|---|---|---|---|
| `text` | `string` | Yes | — | Sign text (max 200 chars). Rendered uppercase. Newlines force line breaks; text auto-wraps otherwise. Supported characters: A–Z, 0–9, space, newline, and `! ? . , ' " & @ # $ % - + / : ;`. |
| `width` | `integer` | No | `1200` | Image width in pixels (400–2000). |
| `height` | `integer` | No | `900` | Image height in pixels (400–2000). |
| `seed` | `integer` | No | Random | Same text + seed + dimensions produce byte-identical output. The seed used is echoed in the `X-Signboard-Seed` response header (PNG mode) or the `seed` field (JSON mode). |
| `platforms` | `string[]` | No | — | When present and non-empty, posts the image to `bluesky`, `mastodon`, and/or `linkedin` and returns JSON results instead of the PNG. |
| `caption` | `string` | No | Sign text | Post body text when posting (max 1000 chars). |
| `hashtags` | `string[]` | No | — | Hashtags appended when posting (no spaces, max 100 chars each). |
| `altText` | `string` | No | Auto | Image alt text when posting (max 1500 chars). Defaults to `Letterboard sign reading: {text}`. |

#### Example — Generate a PNG

```bash
curl -X POST http://localhost:5000/api/signboard \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: YOUR_API_KEY" \
  -d '{"text": "I USED TO THINK\nI WAS INDECISIVE\nBUT NOW\nI'\''M NOT SURE", "seed": 42}' \
  --output signboard.png
```

#### Example — Generate and Post to Social Platforms

```http
POST /api/signboard
```

```json
{
  "text": "SORRY WE ARE OPEN",
  "platforms": ["bluesky", "mastodon"],
  "caption": "New sign day!",
  "hashtags": ["signboard"],
  "altText": "Letterboard sign reading: sorry we are open"
}
```

#### Response — 200 OK (Posting Mode)

```json
{
  "width": 1200,
  "height": 900,
  "seed": 42,
  "results": [
    {
      "platform": "bluesky",
      "success": true,
      "postId": "at://did:plc:abc123/app.bsky.feed.post/xyz789",
      "postUrl": "https://bsky.app/profile/handle.bsky.social/post/xyz789"
    }
  ],
  "postedAt": "2026-07-28T12:00:00+00:00"
}
```

#### Status Codes

| Code | Meaning |
|---|---|
| **200** | PNG generated (generate-only) or all targeted platforms succeeded (posting mode). |
| **207** | Partial success — at least one platform succeeded and at least one failed. |
| **400** | Request validation failed (missing/overlong text, unsupported characters, invalid dimensions or platform). |
| **401** | Missing or invalid `X-Api-Key`. |
| **500** | Unexpected error during image generation. |
| **502** | All targeted platforms failed to post. |
```

- [ ] **Step 3: Final verification**

Run: `dotnet build 2>&1 | tail -3` and `dotnet test 2>&1 | tail -10`
Expected: clean build, all tests pass.

- [ ] **Step 4: Commit**

```bash
git add README.md
git commit -m "docs: document POST /api/signboard endpoint"
```

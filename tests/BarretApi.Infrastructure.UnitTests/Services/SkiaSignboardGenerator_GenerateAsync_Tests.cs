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

	[Fact]
	public async Task NeverPaintsOutsideBoardFace_GivenSingleOverWideWord()
	{
		// 60 x 'W' with no spaces: a single word wider than the board face at any
		// reasonable font size. Must be hard-broken mid-word rather than overflowing.
		var text = new string('W', 60);
		var command = new SignboardGenerationCommand(text, 400, 400, 11);

		var result = await _sut.GenerateAsync(command);

		using var bitmap = SKBitmap.Decode(result);
		AssertNoInkOutsideFrameBand(bitmap);
	}

	[Fact]
	public async Task NeverPaintsOutsideBoardFace_GivenTooManyForcedLineBreaks()
	{
		// 60 explicit newline-separated single-character lines: forces far more rows
		// than fit at any font size down to the old floor, which used to push
		// gridTop negative and render rows off the top of the canvas.
		var text = string.Join('\n', Enumerable.Repeat("X", 60));
		var command = new SignboardGenerationCommand(text, 1200, 900, 17);

		var result = await _sut.GenerateAsync(command);

		using var bitmap = SKBitmap.Decode(result);
		AssertNoInkOutsideFrameBand(bitmap);
	}

	[Fact]
	public async Task NeverPaintsOutsideBoardFace_GivenTooManyForcedLineBreaksOnSmallCanvas()
	{
		var text = string.Join('\n', Enumerable.Repeat("X", 26));
		var command = new SignboardGenerationCommand(text, 400, 400, 23);

		var result = await _sut.GenerateAsync(command);

		using var bitmap = SKBitmap.Decode(result);
		AssertNoInkOutsideFrameBand(bitmap);
	}

	/// <summary>
	/// The outermost few pixels on every edge belong to the dark board frame,
	/// regardless of text content. Any pixel there that isn't dark means text (or
	/// tile artwork) painted outside the board face and got clipped by the canvas.
	/// </summary>
	private static void AssertNoInkOutsideFrameBand(SKBitmap bitmap)
	{
		const int band = 4;
		const byte maxDarkChannel = 80;

		void AssertFrame(int x, int y)
		{
			var pixel = bitmap.GetPixel(x, y);
			var isFrameColor = pixel.Red < maxDarkChannel && pixel.Green < maxDarkChannel && pixel.Blue < maxDarkChannel;
			isFrameColor.ShouldBeTrue($"Expected frame color at ({x},{y}) but found R={pixel.Red} G={pixel.Green} B={pixel.Blue}");
		}

		// Top and bottom bands, full width.
		for (var x = 0; x < bitmap.Width; x++)
		{
			for (var y = 0; y < band; y++)
			{
				AssertFrame(x, y);
				AssertFrame(x, bitmap.Height - 1 - y);
			}
		}

		// Left and right bands, full height.
		for (var y = 0; y < bitmap.Height; y++)
		{
			for (var x = 0; x < band; x++)
			{
				AssertFrame(x, y);
				AssertFrame(bitmap.Width - 1 - x, y);
			}
		}
	}

	private static bool IsPng(byte[] bytes)
	{
		return bytes.Length > 8
			&& bytes[0] == 0x89 && bytes[1] == 0x50
			&& bytes[2] == 0x4E && bytes[3] == 0x47;
	}
}

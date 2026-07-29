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

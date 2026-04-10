using BarretApi.Core.Models;
using BarretApi.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using SkiaSharp;

namespace BarretApi.Infrastructure.UnitTests.Services;

public sealed class SkiaHeroImageGenerator_GenerateAsync_Tests : IDisposable
{
	private readonly SkiaHeroImageGenerator _sut;
	private readonly string _tempDir;

	public SkiaHeroImageGenerator_GenerateAsync_Tests()
	{
		_tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(_tempDir);

		var facePath = Path.Combine(_tempDir, "face.png");
		var logoPath = Path.Combine(_tempDir, "logo.png");
		var bgPath = Path.Combine(_tempDir, "background.jpg");

		File.WriteAllBytes(facePath, CreateTestPng(100, 100, SKColors.CornflowerBlue));
		File.WriteAllBytes(logoPath, CreateTestPng(100, 100, SKColors.Orange));
		File.WriteAllBytes(bgPath, CreateTestJpeg(320, 180, SKColors.DarkSlateGray));

		var heroOptions = new HeroImageOptions
		{
			FaceImagePath = facePath,
			LogoImagePath = logoPath,
			DefaultBackgroundPath = bgPath,
			OutputWidth = 320,
			OutputHeight = 180
		};

		_sut = new SkiaHeroImageGenerator(
			Options.Create(heroOptions),
			Substitute.For<ILogger<SkiaHeroImageGenerator>>());
	}

	[Fact]
	public async Task ReturnsValidPngBytes_GivenTitleOnly()
	{
		var command = new HeroImageGenerationCommand("Getting Started with .NET 10");

		var result = await _sut.GenerateAsync(command);

		result.Length.ShouldBeGreaterThan(0);
		IsPng(result).ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnsValidPngBytes_GivenTitleAndSubtitle()
	{
		var command = new HeroImageGenerationCommand(
			"Getting Started with .NET 10",
			Subtitle: "A practical guide");

		var result = await _sut.GenerateAsync(command);

		result.Length.ShouldBeGreaterThan(0);
		IsPng(result).ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnsValidPngBytes_GivenCustomBackground()
	{
		var customBg = CreateTestJpeg(320, 180, SKColors.DarkRed);
		var command = new HeroImageGenerationCommand(
			"Custom Background Test",
			CustomBackgroundBytes: customBg);

		var result = await _sut.GenerateAsync(command);

		result.Length.ShouldBeGreaterThan(0);
		IsPng(result).ShouldBeTrue();
	}

	[Fact]
	public async Task ThrowsArgumentException_GivenEmptyTitle()
	{
		var command = new HeroImageGenerationCommand(string.Empty);

		await Should.ThrowAsync<ArgumentException>(() => _sut.GenerateAsync(command));
	}

	[Fact]
	public async Task ThrowsArgumentNullException_GivenNullCommand()
	{
		await Should.ThrowAsync<ArgumentNullException>(() => _sut.GenerateAsync(null!));
	}

	public void Dispose()
	{
		if (Directory.Exists(_tempDir))
		{
			Directory.Delete(_tempDir, recursive: true);
		}
	}

	private static bool IsPng(byte[] bytes)
	{
		// PNG magic bytes: 89 50 4E 47
		return bytes.Length >= 4
			&& bytes[0] == 0x89
			&& bytes[1] == 0x50
			&& bytes[2] == 0x4E
			&& bytes[3] == 0x47;
	}

	private static byte[] CreateTestPng(int width, int height, SKColor color)
	{
		using var bitmap = new SKBitmap(width, height);
		using var canvas = new SKCanvas(bitmap);
		canvas.Clear(color);
		using var image = SKImage.FromBitmap(bitmap);
		using var data = image.Encode(SKEncodedImageFormat.Png, 100);
		return data.ToArray();
	}

	private static byte[] CreateTestJpeg(int width, int height, SKColor color)
	{
		using var bitmap = new SKBitmap(width, height);
		using var canvas = new SKCanvas(bitmap);
		canvas.Clear(color);
		using var image = SKImage.FromBitmap(bitmap);
		using var data = image.Encode(SKEncodedImageFormat.Jpeg, 85);
		return data.ToArray();
	}
}

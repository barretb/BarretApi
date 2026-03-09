using BarretApi.Infrastructure.Services;
using Shouldly;

namespace BarretApi.Infrastructure.UnitTests.Services;

public sealed class SkiaImageResizer_ResizeToFit_Tests
{
	private readonly SkiaImageResizer _sut = new();

	[Fact]
	public void ReturnsOriginalBytes_GivenImageUnderLimit()
	{
		var imageBytes = CreateTestJpeg(100, 100, quality: 50);

		var result = _sut.ResizeToFit(imageBytes, 1_048_576);

		result.ShouldBeSameAs(imageBytes);
	}

	[Fact]
	public void ReturnsJpegBytes_GivenAnyInput()
	{
		var imageBytes = CreateTestPng(200, 200);

		var result = _sut.ResizeToFit(imageBytes, 1_048_576);

		// JPEG magic bytes: FF D8 FF
		result.Length.ShouldBeGreaterThan(2);
		result[0].ShouldBe((byte)0xFF);
		result[1].ShouldBe((byte)0xD8);
	}

	[Fact]
	public void ReturnsResizedImage_GivenImageOverLimit()
	{
		// Create a large enough image that would exceed a small limit
		var imageBytes = CreateTestJpeg(800, 600, quality: 95);
		var smallLimit = imageBytes.Length / 4;

		var result = _sut.ResizeToFit(imageBytes, smallLimit);

		result.Length.ShouldBeLessThanOrEqualTo(smallLimit);
	}

	[Fact]
	public void ThrowsArgumentException_GivenEmptyArray()
	{
		Should.Throw<ArgumentException>(() => _sut.ResizeToFit([], 1000));
	}

	[Fact]
	public void ThrowsArgumentException_GivenNullArray()
	{
		Should.Throw<ArgumentException>(() => _sut.ResizeToFit(null!, 1000));
	}

	[Fact]
	public void ThrowsArgumentOutOfRangeException_GivenZeroMaxBytes()
	{
		var imageBytes = CreateTestJpeg(100, 100, quality: 50);

		Should.Throw<ArgumentOutOfRangeException>(() => _sut.ResizeToFit(imageBytes, 0));
	}

	[Fact]
	public void MaintainsValidJpeg_GivenPngInput()
	{
		var pngBytes = CreateTestPng(400, 300);

		var result = _sut.ResizeToFit(pngBytes, 50_000);

		// Should still produce valid JPEG
		result[0].ShouldBe((byte)0xFF);
		result[1].ShouldBe((byte)0xD8);
	}

	[Fact]
	public void ReturnsImageWithinLimit_GivenVerySmallLimit()
	{
		var imageBytes = CreateTestJpeg(400, 300, quality: 90);
		var tinyLimit = 5_000L;

		var result = _sut.ResizeToFit(imageBytes, tinyLimit);

		result.Length.ShouldBeLessThanOrEqualTo((int)tinyLimit);
	}

	private static byte[] CreateTestJpeg(int width, int height, int quality)
	{
		using var bitmap = new SkiaSharp.SKBitmap(width, height);
		using var canvas = new SkiaSharp.SKCanvas(bitmap);
		canvas.Clear(SkiaSharp.SKColors.CornflowerBlue);
		using var paint = new SkiaSharp.SKPaint { Color = SkiaSharp.SKColors.Red };
		canvas.DrawCircle(width / 2f, height / 2f, Math.Min(width, height) / 3f, paint);
		using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
		using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Jpeg, quality);
		return data.ToArray();
	}

	private static byte[] CreateTestPng(int width, int height)
	{
		using var bitmap = new SkiaSharp.SKBitmap(width, height);
		using var canvas = new SkiaSharp.SKCanvas(bitmap);
		canvas.Clear(SkiaSharp.SKColors.Green);
		using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
		using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
		return data.ToArray();
	}
}

using BarretApi.Core.Interfaces;
using SkiaSharp;

namespace BarretApi.Infrastructure.Services;

/// <summary>
/// Resizes images using SkiaSharp with a quality-first strategy (85→45),
/// then dimension reduction as a fallback. Output is always JPEG.
/// </summary>
public sealed class SkiaImageResizer : IImageResizer
{
    private static readonly int[] QualitySteps = [85, 75, 65, 55, 45];

    public byte[] ResizeToFit(byte[] imageBytes, long maxBytes)
    {
        ArgumentNullException.ThrowIfNull(imageBytes);

        if (imageBytes.Length == 0)
        {
            throw new ArgumentException("Image bytes cannot be empty.", nameof(imageBytes));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);

        if (imageBytes.Length <= maxBytes && IsJpeg(imageBytes))
        {
            return imageBytes;
        }

        using var original = SKBitmap.Decode(imageBytes)
            ?? throw new InvalidOperationException("Failed to decode image.");

        // If already under limit but not JPEG, encode as JPEG at highest quality
        if (imageBytes.Length <= maxBytes)
        {
            return EncodeBitmap(original, QualitySteps[0]);
        }

        foreach (var quality in QualitySteps)
        {
            var encoded = EncodeBitmap(original, quality);
            if (encoded.Length <= maxBytes)
            {
                return encoded;
            }
        }

        var scale = 0.9f;
        while (scale >= 0.1f)
        {
            var newWidth = (int)(original.Width * scale);
            var newHeight = (int)(original.Height * scale);
            var info = new SKImageInfo(newWidth, newHeight);

            using var resized = original.Resize(info, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
            if (resized is null)
            {
                break;
            }

            foreach (var quality in QualitySteps)
            {
                var encoded = EncodeBitmap(resized, quality);
                if (encoded.Length <= maxBytes)
                {
                    return encoded;
                }
            }

            scale -= 0.1f;
        }

        throw new InvalidOperationException(
            $"Cannot resize image to fit within {maxBytes} bytes.");
    }

    private static byte[] EncodeBitmap(SKBitmap bitmap, int quality)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, quality);
        return data.ToArray();
    }

    private static bool IsJpeg(byte[] imageBytes)
    {
        return imageBytes.Length >= 2
            && imageBytes[0] == 0xFF
            && imageBytes[1] == 0xD8;
    }
}

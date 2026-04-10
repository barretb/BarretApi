using System.Reflection;
using BarretApi.Core.Interfaces;
using BarretApi.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace BarretApi.Infrastructure.Services;

/// <summary>
/// Generates branded hero images (1280x720 PNG) using SkiaSharp.
/// </summary>
public sealed class SkiaHeroImageGenerator(
IOptions<HeroImageOptions> options,
ILogger<SkiaHeroImageGenerator> logger) : IHeroImageGenerator
{
private readonly HeroImageOptions _options = options.Value;
private readonly ILogger<SkiaHeroImageGenerator> _logger = logger;

private const string BoldFontResource = "BarretApi.Infrastructure.Fonts.JetBrainsMono-Bold.ttf";
private const string RegularFontResource = "BarretApi.Infrastructure.Fonts.JetBrainsMono-Regular.ttf";
private const int TitleStartSize = 68;
private const int TitleMinSize = 20;
private const int SubtitleStartSize = 38;
private const int SubtitleMinSize = 16;
private const int OverlayAlpha = 153;
private const int OverlayImageHeight = 270;
private const int OverlayImagePadding = 30;
private const int TextSpacing = 16;
private const int OverlayImageGap = 10;
private const int TaglineFontSize = 26;
private const string TaglineText = "C# - .NET - SQL - Power Automate";

public Task<byte[]> GenerateAsync(HeroImageGenerationCommand command, CancellationToken cancellationToken = default)
{
ArgumentNullException.ThrowIfNull(command);

if (string.IsNullOrWhiteSpace(command.Title))
{
throw new ArgumentException("Title must not be empty.", nameof(command));
}

_logger.LogInformation(
"Generating hero image: title={Title}, hasSubtitle={HasSubtitle}, hasCustomBg={HasCustomBg}",
command.Title,
command.Subtitle is not null,
command.CustomBackgroundBytes is not null);

var boldFontBytes = LoadEmbeddedResource(BoldFontResource);
var regularFontBytes = LoadEmbeddedResource(RegularFontResource);

using var boldData = SKData.CreateCopy(boldFontBytes);
using var regularData = SKData.CreateCopy(regularFontBytes);
using var boldTypeface = SKTypeface.FromData(boldData);
using var regularTypeface = SKTypeface.FromData(regularData);

var width = _options.OutputWidth;
var height = _options.OutputHeight;

using var surface = SKSurface.Create(new SKImageInfo(width, height));
var canvas = surface.Canvas;

using var background = LoadBackground(command.CustomBackgroundBytes);
DrawScaledBackground(canvas, background, width, height);

using var overlayPaint = new SKPaint { Color = new SKColor(0, 0, 0, OverlayAlpha) };
canvas.DrawRect(SKRect.Create(0, 0, width, height), overlayPaint);

var overlayLift = (int)(height * 0.20f) - 60;

using var logo = SKBitmap.Decode(_options.LogoImagePath)
?? throw new InvalidOperationException("Failed to decode logo image.");
var (logoW, logoH) = ScaleDimensionsToHeight(logo.Width, logo.Height, OverlayImageHeight);
var logoRect = new SKRect(
OverlayImagePadding,
height - OverlayImagePadding - logoH - overlayLift,
OverlayImagePadding + logoW,
height - OverlayImagePadding - overlayLift);
using (var scaledLogo = logo.Resize(new SKImageInfo((int)logoW, (int)logoH), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear)))
{
if (scaledLogo is not null)
{
canvas.DrawBitmap(scaledLogo, logoRect);
}
}

using var face = SKBitmap.Decode(_options.FaceImagePath)
?? throw new InvalidOperationException("Failed to decode face image.");
var (faceW, faceH) = ScaleDimensionsToHeight(face.Width, face.Height, OverlayImageHeight);
var faceRect = new SKRect(
width - OverlayImagePadding - faceW,
height - OverlayImagePadding - faceH - overlayLift,
width - OverlayImagePadding,
height - OverlayImagePadding - overlayLift);
using (var scaledFace = face.Resize(new SKImageInfo((int)faceW, (int)faceH), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear)))
{
if (scaledFace is not null)
{
canvas.DrawBitmap(scaledFace, faceRect);
}
}

float textLeft = OverlayImagePadding + logoW + OverlayImagePadding;
float textRight = width - OverlayImagePadding - faceW - OverlayImagePadding;
float textAreaWidth = textRight - textLeft;
float textCenterX = textLeft + textAreaWidth / 2f;
float textZoneBottom = height - OverlayImagePadding - OverlayImageHeight - OverlayImageGap;

var hasSubtitle = !string.IsNullOrWhiteSpace(command.Subtitle);

var (titleFontSize, titleLines) = FitText(command.Title, boldTypeface, TitleStartSize, TitleMinSize, textAreaWidth);
var (subtitleFontSize, subtitleLines) = hasSubtitle
? FitText(command.Subtitle!, regularTypeface, SubtitleStartSize, SubtitleMinSize, textAreaWidth)
: (0f, Array.Empty<string>());

float titleLineHeight = titleFontSize * 1.2f;
float subtitleLineHeight = subtitleFontSize * 1.2f;
float titleBlockHeight = titleLines.Length * titleLineHeight;
float subtitleBlockHeight = hasSubtitle ? subtitleLines.Length * subtitleLineHeight + TextSpacing : 0f;
float totalTextHeight = titleBlockHeight + subtitleBlockHeight;
float textStartY = (textZoneBottom - totalTextHeight) / 2f;

using var boldFont = new SKFont(boldTypeface, titleFontSize);
using var titlePaint = new SKPaint { Color = SKColors.White, IsAntialias = true };

float y = textStartY + titleLineHeight;
foreach (var line in titleLines)
{
canvas.DrawText(line, textCenterX, y, SKTextAlign.Center, boldFont, titlePaint);
y += titleLineHeight;
}

if (hasSubtitle)
{
using var regularFont = new SKFont(regularTypeface, subtitleFontSize);
using var subtitlePaint = new SKPaint { Color = new SKColor(220, 220, 220, 255), IsAntialias = true };

y += TextSpacing;
foreach (var line in subtitleLines)
{
canvas.DrawText(line, textCenterX, y, SKTextAlign.Center, regularFont, subtitlePaint);
y += subtitleLineHeight;
}
}

var lowerZoneTop = textZoneBottom + OverlayImageGap;
var lowerZoneBottom = height - OverlayImagePadding;
var taglineY = (lowerZoneTop + lowerZoneBottom) / 2f + TaglineFontSize * 0.35f;
using var taglineFont = new SKFont(boldTypeface, TaglineFontSize);
using var taglinePaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
canvas.DrawText(TaglineText, width / 2f, taglineY, SKTextAlign.Center, taglineFont, taglinePaint);

using var image = surface.Snapshot();
using var data = image.Encode(SKEncodedImageFormat.Png, 100);
var bytes = data.ToArray();

_logger.LogInformation("Hero image generated: {Width}x{Height}, {Size} bytes", width, height, bytes.Length);

return Task.FromResult(bytes);
}

private SKBitmap LoadBackground(byte[]? customBytes)
{
if (customBytes is not null)
{
return SKBitmap.Decode(customBytes)
?? throw new InvalidOperationException("Failed to decode custom background image.");
}

return SKBitmap.Decode(_options.DefaultBackgroundPath)
?? throw new InvalidOperationException("Failed to decode default background image.");
}

private static void DrawScaledBackground(SKCanvas canvas, SKBitmap source, int targetWidth, int targetHeight)
{
var scaleX = (float)targetWidth / source.Width;
var scaleY = (float)targetHeight / source.Height;
var scale = Math.Max(scaleX, scaleY);

int scaledW = (int)(source.Width * scale);
int scaledH = (int)(source.Height * scale);
int offsetX = (scaledW - targetWidth) / 2;
int offsetY = (scaledH - targetHeight) / 2;

using var resized = source.Resize(
new SKImageInfo(scaledW, scaledH),
new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));

if (resized is null)
{
return;
}

var srcRect = new SKRect(offsetX, offsetY, offsetX + targetWidth, offsetY + targetHeight);
var destRect = SKRect.Create(0, 0, targetWidth, targetHeight);
canvas.DrawBitmap(resized, srcRect, destRect);
}

private static (float width, float height) ScaleDimensionsToHeight(int sourceWidth, int sourceHeight, int targetHeight)
{
var ratio = (float)targetHeight / sourceHeight;
return (sourceWidth * ratio, targetHeight);
}

private static (float fontSize, string[] lines) FitText(
string text,
SKTypeface typeface,
int startSize,
int minSize,
float maxWidth)
{
for (var size = (float)startSize; size >= minSize; size -= 2)
{
using var font = new SKFont(typeface, size);
var measured = font.MeasureText(text);
if (measured <= maxWidth)
{
return (size, [text]);
}
}

var midpoint = text.Length / 2;
var breakAt = FindBreakNear(text, midpoint);
if (breakAt > 0)
{
var line1 = text[..breakAt].TrimEnd();
var line2 = text[breakAt..].TrimStart();
return (minSize, [line1, line2]);
}

return (minSize, [text]);
}

private static int FindBreakNear(string text, int near)
{
for (var i = near; i >= 0; i--)
{
if (text[i] == ' ')
{
return i;
}
}

for (var i = near + 1; i < text.Length; i++)
{
if (text[i] == ' ')
{
return i;
}
}

return -1;
}

private static byte[] LoadEmbeddedResource(string resourceName)
{
var assembly = Assembly.GetExecutingAssembly();
using var stream = assembly.GetManifestResourceStream(resourceName)
?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");

using var ms = new MemoryStream();
stream.CopyTo(ms);
return ms.ToArray();
}
}
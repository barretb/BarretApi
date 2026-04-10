# Research: Hero Image Generator

**Feature Branch**: `009-hero-image-generator`
**Date**: 2026-04-10

## R-001: Image Composition Library for .NET

**Decision**: Use SkiaSharp 3.119.2 (already in project)

**Rationale**: SkiaSharp is already a dependency in `BarretApi.Infrastructure` and is used by the existing word cloud generator (`SkiaWordCloudGenerator`) and image resizer (`SkiaImageResizer`). It provides all required capabilities: bitmap loading/decoding, canvas drawing, image compositing, text rendering with custom fonts, alpha blending for fade effects, and PNG/JPEG encoding. No additional image processing library is needed.

**Alternatives considered**:

- **ImageSharp (SixLabors)**: Full-featured cross-platform image library. Rejected because SkiaSharp is already in the project and well-understood; adding a second image library creates unnecessary dependency surface.
- **System.Drawing**: Legacy Windows-only API. Rejected for cross-platform incompatibility (runs on Linux via Aspire).
- **Magick.NET (ImageMagick)**: Powerful but heavyweight. Rejected due to large native dependency and no additional capability needed beyond SkiaSharp.

---

## R-002: Font Selection — Tech-Themed, Readable at Hero Image Scale

**Decision**: Use **JetBrains Mono** (Bold for title, Regular for subtitle)

**Rationale**: JetBrains Mono is the strongest "tech identity" font available — it is instantly recognizable in developer communities, tagged as "Technology", "Monospaced", and "Futuristic" on Google Fonts. It has excellent readability at large sizes, multiple weights (Regular 400 through ExtraBold 800), and is licensed under the SIL Open Font License 1.1, allowing free bundling in the application. While monospace fonts use more horizontal space than proportional ones, the dynamic text scaling requirement (FR-012) handles this — the text sizing algorithm will reduce font size for longer strings to fit the available layout area.

**Alternatives considered**:

- **Fira Code**: Also monospace and tech-themed (OFL licensed). Rejected because JetBrains Mono has wider weight range (up to ExtraBold 800) providing better title/subtitle contrast, and is more widely recognized in the tech community.
- **Exo 2**: Proportional geometric sans-serif with tech feel. Rejected because while more space-efficient, it lacks the distinctive "code editor" personality that makes hero images feel authentically tech-themed.
- **Share Tech**: Proportional tech-themed font. Rejected because it only offers Regular 400 weight — no bold variant, giving poor title/subtitle visual differentiation.
- **Orbitron**: Futuristic geometric sans-serif. Rejected because it prioritizes aesthetics over readability; characters are hard to distinguish at smaller subtitle sizes.

**Font file deployment**: The JetBrains Mono `.ttf` files (Bold and Regular) will be stored in `src/BarretApi.Infrastructure/Fonts/` as embedded resources. This keeps the rendering dependency self-contained within the Infrastructure layer where SkiaSharp rendering occurs, with no file path configuration required.

---

## R-003: Image Layout Strategy for 1280×720 Canvas

**Decision**: Fixed-position layout with percentage-based regions and dynamic text scaling

**Rationale**: A 1280×720 canvas provides standard YouTube thumbnail and blog header dimensions. The layout is divided into three regions:

1. **Background layer**: Full canvas (1280×720), scaled/cropped to fill, with a semi-transparent dark overlay (black at ~60% opacity) to fade the background image.
2. **Lower-left region**: Logo image positioned with ~30px padding from bottom and left edges, scaled to ~180px height maintaining aspect ratio.
3. **Lower-right region**: Face image positioned with ~30px padding from bottom and right edges, scaled to ~180px height maintaining aspect ratio.
4. **Text region**: The horizontal area between the logo's right edge and the face's left edge, vertically centered on the canvas. Title rendered in JetBrains Mono Bold; subtitle (when present) rendered below title in JetBrains Mono Regular at a smaller size.

Text scaling algorithm:
- Start with a base title font size (e.g., 56px) and subtitle font size (e.g., 32px)
- Measure text width using `SKPaint.MeasureText()`
- If text exceeds available width, reduce font size incrementally until it fits
- If text still exceeds at minimum readable size, enable word wrapping
- Maintain a minimum gap between title bottom and subtitle top

**Alternatives considered**:

- **Percentage-based dynamic positioning**: Calculate all positions as percentages of canvas size. Rejected for the initial release because the canvas size is fixed at 1280×720 (per assumptions), making percentage math unnecessary overhead. Can be added if configurable dimensions are needed later.
- **Template-based overlay**: Pre-render a static overlay image with logo/face positions. Rejected because it prevents dynamic text positioning and makes the layout inflexible.

---

## R-004: File Upload Handling for Custom Background

**Decision**: Use FastEndpoints' built-in `IFormFile` binding for multipart/form-data upload

**Rationale**: FastEndpoints supports `IFormFile` in request models for file uploads. The endpoint receives the file, validates format (JPEG/PNG by checking magic bytes) and size (≤10 MB), then passes the decoded `SKBitmap` to the generator service. This follows the existing FastEndpoints conventions in the project.

**Alternatives considered**:

- **Base64 encoding in JSON body**: Encode the image as a base64 string in a JSON request. Rejected because it inflates payload size by ~33%, has no streaming capability, and is less idiomatic for file uploads.
- **URL reference**: Accept a URL to the background image and fetch it server-side. Rejected because it adds network dependency, latency, and potential SSRF security risks.

---

## R-005: Background Fade/Dim Technique

**Decision**: Draw a semi-transparent black rectangle over the background image using SkiaSharp alpha blending

**Rationale**: After compositing the background image (scaled to fill the 1280×720 canvas), draw a filled rectangle covering the full canvas using `SKPaint` with color `SKColors.Black` and alpha value ~153 (60% opacity). This is the simplest approach with SkiaSharp and produces consistent, predictable results. The exact alpha value can be tuned during implementation.

**Implementation approach**:

```
canvas.DrawBitmap(backgroundBitmap, destRect)  // Draw background
canvas.DrawRect(fullCanvasRect, fadePaint)       // Overlay semi-transparent black
```

**Alternatives considered**:

- **Gaussian blur + darken**: Blur the background then darken. Rejected because blur is computationally expensive and the spec only requires fade/dim, not blur.
- **Reduce brightness channel**: Modify pixel brightness values directly. Rejected because per-pixel manipulation is slower than a single overlay draw call.

---

## R-006: Output Format Selection

**Decision**: Return PNG by default

**Rationale**: PNG preserves text sharpness and logo/face transparency (the face image `barretcircle2.png` is PNG, potentially with transparency). PNG is lossless and produces clean text rendering. File size for a 1280×720 image is typically 500KB–2MB, acceptable for web delivery. The `Content-Type` response header will be `image/png`.

**Alternatives considered**:

- **JPEG**: Smaller file size but lossy compression creates artifacts around text edges and logo boundaries. Rejected as default but could be offered as a query parameter option in a future iteration.
- **WebP**: Modern format with good compression. Rejected because SkiaSharp's WebP support varies by platform and PNG is universally supported.

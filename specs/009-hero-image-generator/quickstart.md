# Quickstart: Hero Image Generator

**Feature Branch**: `009-hero-image-generator`
**Date**: 2026-04-10

## Prerequisites

- .NET 10.0 SDK
- The following asset files present in `src/BarretApi.Api/images/`:
  - `barretcircle2.png` (face image)
  - `barret-blake-logo-1024.png` (logo image)
  - `generic-background.jpg` (default background)
- JetBrains Mono font files (Bold + Regular `.ttf`) bundled as embedded resources in `BarretApi.Infrastructure`

## Build & Run

```bash
# Build the solution
dotnet build

# Run via Aspire AppHost
dotnet run --project src/BarretApi.AppHost/BarretApi.AppHost.csproj
```

## Generate a Hero Image

### Title Only

```bash
curl -X POST http://localhost:5000/api/hero-image \
  -F "title=Getting Started with .NET 10" \
  -o hero-image.png
```

### Title + Subtitle

```bash
curl -X POST http://localhost:5000/api/hero-image \
  -F "title=Blazor Deep Dive" \
  -F "subtitle=Part 3: Component Lifecycle" \
  -o hero-image.png
```

### Title + Subtitle + Custom Background

```bash
curl -X POST http://localhost:5000/api/hero-image \
  -F "title=Azure Functions Masterclass" \
  -F "subtitle=Serverless Architecture Patterns" \
  -F "backgroundImage=@/path/to/my-background.jpg" \
  -o hero-image.png
```

## Verify Output

Open `hero-image.png` and confirm:

1. Image is 1280×720 pixels
2. Background is visibly faded/dimmed
3. Face image appears in the lower-right corner
4. Logo image appears in the lower-left corner
5. Title text is rendered in a tech-themed font (JetBrains Mono Bold)
6. Subtitle (if provided) appears below the title in a smaller size
7. All text fits between the logo and face images with no overlap

## Run Tests

```bash
dotnet test
```

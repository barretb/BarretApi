# Quickstart: Webpage Word Cloud Generator

**Feature Branch**: `004-webpage-word-cloud`

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for Aspire AppHost)
- A valid API key configured in the Aspire AppHost (see main [README.md](../../README.md#authentication))

## New NuGet Packages

Add to `Directory.Packages.props` (Central Package Management):

```xml
<PackageVersion Include="AngleSharp" Version="1.4.0" />
<PackageVersion Include="KnowledgePicker.WordCloud" Version="1.3.2" />
```

Package references (no versions — managed centrally):

- `BarretApi.Infrastructure.csproj` — `AngleSharp`, `KnowledgePicker.WordCloud`
- No new packages needed in `BarretApi.Core.csproj` or `BarretApi.Api.csproj`

## Build & Run

```bash
# Build entire solution
dotnet build

# Run with Aspire AppHost
dotnet run --project src/BarretApi.AppHost/BarretApi.AppHost.csproj

# Run tests
dotnet test
```

## Try the Endpoint

### Generate Word Cloud (default dimensions)

```bash
curl -X POST http://localhost:5000/api/word-cloud \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: YOUR_API_KEY" \
  -d '{"url": "https://en.wikipedia.org/wiki/.NET"}' \
  --output word-cloud.png
```

### Generate Word Cloud (custom dimensions)

```bash
curl -X POST http://localhost:5000/api/word-cloud \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: YOUR_API_KEY" \
  -d '{"url": "https://en.wikipedia.org/wiki/.NET", "width": 1200, "height": 800}' \
  --output word-cloud.png
```

### Expected Response

- **200 OK** — PNG image binary in response body, `Content-Type: image/png`
- **400 Bad Request** — Invalid URL format or dimensions out of range
- **401 Unauthorized** — Missing or invalid API key
- **422 Unprocessable Entity** — URL fetched but insufficient text content
- **502 Bad Gateway** — Target URL unreachable, not HTML, or timed out

## Configuration

No new configuration parameters are needed. The endpoint uses `HttpClient` with default settings from the existing infrastructure.

| Aspect | Value |
|--------|-------|
| Fetch timeout | 30 seconds |
| Max redirects | 5 |
| Max HTML size | 500 KB |
| Max words in cloud | 100 |
| Default image size | 800 x 600 px |
| Min image size | 200 x 200 px |
| Max image size | 2000 x 2000 px |

## Architecture Overview

```text
POST /api/word-cloud
        │
        ▼
GenerateWordCloudEndpoint (Api)
        │
        ├── IHtmlTextExtractor.ExtractTextAsync(url)
        │       └── AngleSharpHtmlTextExtractor (Infrastructure)
        │           Fetches HTML via HttpClient, parses with AngleSharp,
        │           strips scripts/styles, returns visible text
        │
        ├── TextAnalysisService.AnalyzeText(text, maxWords)
        │       └── Core service
        │           Tokenizes, lowercases, strips punctuation,
        │           removes stop words, counts frequencies, returns top N
        │
        └── IWordCloudGenerator.GenerateAsync(frequencies, options)
                └── SkiaWordCloudGenerator (Infrastructure)
                    Uses KnowledgePicker.WordCloud + SkiaSharp
                    to render PNG word cloud image
```

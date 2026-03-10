# BarretApi Development Guidelines

Auto-generated from all feature plans. Last updated: 2026-02-28

## Active Technologies
- C# (latest) on .NET `net10.0` + FastEndpoints 8, FastEndpoints.Swagger, Microsoft.Extensions.* logging/options, Azure.Data.Tables (planned), Azure.Identity (planned) (001-rss-blog-posting)
- Azure Table Storage for blog-post promotion tracking records (001-rss-blog-posting)
- C# (latest) on .NET `net10.0` + FastEndpoints 8, FluentValidation 11, Microsoft.Extensions.* logging/options, existing HttpClient-based platform adapters (001-linkedin-posting)
- N/A for LinkedIn posting itself (no new persistent store); existing API behavior remains stateless per request for direct post endpoint (001-linkedin-posting)
- C# latest / .NET 10.0 (`net10.0`) + FastEndpoints 8.x, FluentValidation (via FastEndpoints), System.ServiceModel.Syndication (002-rss-random-post)
- N/A (stateless — no tracking of previously posted entries) (002-rss-random-post)
- C# latest / .NET 10.0 + FastEndpoints 8.x, SkiaSharp (new — image resizing), Microsoft.Extensions.Logging, Microsoft.Extensions.Options (001-nasa-apod-post)
- N/A (stateless — no persistence required for APOD posts) (001-nasa-apod-post)
- C# (latest) / .NET 10.0 (`net10.0`) + FastEndpoints 8.x, AngleSharp 1.4.0 (HTML parsing), KnowledgePicker.WordCloud 1.3.2 (image generation), SkiaSharp 3.119.2 (already in project) (004-webpage-word-cloud)
- N/A — stateless request/response, no persistence (004-webpage-word-cloud)

- C# / .NET 10.0 (`net10.0`), latest language features, nullable reference types enabled + FastEndpoints 7.x, Aspire 13 (AppHost + ServiceDefaults), FluentValidation, Microsoft.Extensions.Http (for HttpClientFactory with Polly retry) (001-social-post-api)

## Project Structure

```text
backend/
frontend/
tests/
```

## Commands

# Add commands for C# / .NET 10.0 (`net10.0`), latest language features, nullable reference types enabled

## Code Style

C# / .NET 10.0 (`net10.0`), latest language features, nullable reference types enabled: Follow standard conventions

## Recent Changes
- 004-webpage-word-cloud: Added C# (latest) / .NET 10.0 (`net10.0`) + FastEndpoints 8.x, AngleSharp 1.4.0 (HTML parsing), KnowledgePicker.WordCloud 1.3.2 (image generation), SkiaSharp 3.119.2 (already in project)
- 001-nasa-apod-post: Added C# latest / .NET 10.0 + FastEndpoints 8.x, SkiaSharp (new — image resizing), Microsoft.Extensions.Logging, Microsoft.Extensions.Options
- 002-rss-random-post: Added C# latest / .NET 10.0 (`net10.0`) + FastEndpoints 8.x, FluentValidation (via FastEndpoints), System.ServiceModel.Syndication


<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->

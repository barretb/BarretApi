# BarretApi Development Guidelines

Auto-generated from all feature plans. Last updated: 2026-02-28

## Active Technologies
- C# (latest) on .NET `net10.0` + FastEndpoints 8, FastEndpoints.Swagger, Microsoft.Extensions.* logging/options, Azure.Data.Tables (planned), Azure.Identity (planned) (001-rss-blog-posting)
- Azure Table Storage for blog-post promotion tracking records (001-rss-blog-posting)
- C# (latest) on .NET `net10.0` + FastEndpoints 8, FluentValidation 11, Microsoft.Extensions.* logging/options, existing HttpClient-based platform adapters (001-linkedin-posting)
- N/A for LinkedIn posting itself (no new persistent store); existing API behavior remains stateless per request for direct post endpoint (001-linkedin-posting)
- C# latest / .NET 10.0 (`net10.0`) + FastEndpoints 8.x, FluentValidation (via FastEndpoints), System.ServiceModel.Syndication (002-rss-random-post)
- N/A (stateless — no tracking of previously posted entries) (002-rss-random-post)

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
- 002-rss-random-post: Added C# latest / .NET 10.0 (`net10.0`) + FastEndpoints 8.x, FluentValidation (via FastEndpoints), System.ServiceModel.Syndication
- 001-linkedin-posting: Added C# (latest) on .NET `net10.0` + FastEndpoints 8, FluentValidation 11, Microsoft.Extensions.* logging/options, existing HttpClient-based platform adapters
- 001-rss-blog-posting: Added C# (latest) on .NET `net10.0` + FastEndpoints 8, FastEndpoints.Swagger, Microsoft.Extensions.* logging/options, Azure.Data.Tables (planned), Azure.Identity (planned)


<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->

# Quickstart: DiceBear Random Avatar

**Feature**: 007-dicebear-avatar
**Date**: 2026-03-15

## Overview

This feature adds a `GET /api/avatars/random` endpoint that generates random avatar images using the DiceBear API. No parameters are required — just call the endpoint and get an avatar.

## Prerequisites

- The BarretApi solution builds successfully (`dotnet build`)
- The Aspire AppHost is configured and running

## Quick Test

### Generate a random avatar (no parameters)

```bash
curl -H "X-Api-Key: YOUR_KEY" http://localhost:5000/api/avatars/random --output avatar.svg
```

### Generate with a specific style

```bash
curl -H "X-Api-Key: YOUR_KEY" "http://localhost:5000/api/avatars/random?style=pixel-art" --output avatar.svg
```

### Generate a PNG avatar

```bash
curl -H "X-Api-Key: YOUR_KEY" "http://localhost:5000/api/avatars/random?format=png" --output avatar.png
```

### Generate a reproducible avatar with a seed

```bash
curl -H "X-Api-Key: YOUR_KEY" "http://localhost:5000/api/avatars/random?style=adventurer&seed=john-doe" --output avatar.svg
```

### Full parameters

```bash
curl -H "X-Api-Key: YOUR_KEY" "http://localhost:5000/api/avatars/random?style=bottts&format=webp&seed=my-user" --output avatar.webp
```

## Expected Behavior

| Scenario | Result |
|----------|--------|
| No parameters | Random style, random seed, SVG format returned |
| Style only | Specified style, random seed, SVG format |
| Format only | Random style, random seed, specified format |
| Seed only | Random style, specified seed, SVG format |
| All parameters | Specified style, seed, and format |
| Invalid style | 400 error with list of valid styles |
| Invalid format | 400 error with list of valid formats |
| Seed > 256 chars | 400 validation error |
| DiceBear unavailable | 502 error with retry message |

## Implementation Layers

1. **API Layer** (`BarretApi.Api/Features/Avatar/`): FastEndpoints endpoint with FluentValidation
2. **Core Layer** (`BarretApi.Core/Interfaces/`): `IDiceBearAvatarClient` interface and models
3. **Infrastructure Layer** (`BarretApi.Infrastructure/DiceBear/`): Typed `HttpClient` calling DiceBear API

## Running Tests

```bash
dotnet test
```

Tests cover:

- Endpoint validation (invalid style, format, seed length)
- Client URL construction for all style/format/seed combinations
- Error handling for upstream failures
- Random style and seed generation defaults

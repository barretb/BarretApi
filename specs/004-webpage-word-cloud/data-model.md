# Data Model: Webpage Word Cloud Generator

**Feature Branch**: `004-webpage-word-cloud`
**Date**: 2026-03-09

## Overview

This feature is stateless — no persistent storage is needed. All entities exist only during request processing. The data flows through a pipeline: URL → HTML → visible text → word frequencies → PNG image.

## Entities

### GenerateWordCloudRequest (API Layer)

The user's input submitted to the endpoint.

| Field | Type | Required | Default | Constraints | Description |
|-------|------|----------|---------|-------------|-------------|
| Url | string | Yes | — | Must be a well-formed absolute HTTP or HTTPS URL | The target web page to analyze |
| Width | int? | No | 800 | Min: 200, Max: 2000 | Output image width in pixels |
| Height | int? | No | 600 | Min: 200, Max: 2000 | Output image height in pixels |

**Validation Rules**:

- `Url` must not be empty and must parse as a valid absolute URI with scheme `http` or `https`
- `Width` and `Height`, when provided, must be between 200 and 2000 inclusive
- `Width` and `Height` are independent — either or both may be omitted to use defaults

### WordCloudOptions (Core Layer)

Resolved configuration passed to the word cloud generator after defaults are applied.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| Width | int | Yes | Output image width in pixels (200–2000) |
| Height | int | Yes | Output image height in pixels (200–2000) |
| MaxWords | int | Yes | Maximum number of words in the cloud (default: 100) |
| MinFontSize | int | Yes | Minimum font size for least frequent words (default: 10) |
| MaxFontSize | int | Yes | Maximum font size for most frequent words (default: 64) |

### WordFrequency (Core Layer)

A word-count pair representing how many times a meaningful word appears in the extracted page content.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| Word | string | Yes | The lowercase, punctuation-stripped word |
| Count | int | Yes | Number of occurrences in the extracted text (> 0) |

**Invariants**:

- `Word` is always lowercase
- `Word` contains no punctuation
- `Word` is at least 3 characters long
- `Word` is not in the English stop word list
- `Count` is always greater than zero

## Interfaces

### IHtmlTextExtractor

Fetches a web page by URL and extracts visible text content.

| Method | Signature | Description |
|--------|-----------|-------------|
| ExtractTextAsync | `Task<string> ExtractTextAsync(string url, CancellationToken cancellationToken)` | Fetches the URL, parses HTML, strips scripts/styles/tags, returns visible text |

**Error conditions**:

- Throws `ArgumentException` if URL is null/empty
- Throws `HttpRequestException` if the page cannot be fetched (network error, HTTP error status)
- Throws `InvalidOperationException` if content type is not HTML
- Respects a 30-second timeout and 5-redirect limit
- Processes at most 500 KB of HTML content

### IWordCloudGenerator

Generates a word cloud PNG image from word frequencies.

| Method | Signature | Description |
|--------|-----------|-------------|
| GenerateAsync | `Task<byte[]> GenerateAsync(IReadOnlyList<WordFrequency> frequencies, WordCloudOptions options, CancellationToken cancellationToken)` | Renders a word cloud image and returns PNG bytes |

**Error conditions**:

- Throws `ArgumentException` if frequencies is null or empty

## Services

### TextAnalysisService (Core)

Processes raw text into ranked word frequencies.

| Method | Signature | Description |
|--------|-----------|-------------|
| AnalyzeText | `IReadOnlyList<WordFrequency> AnalyzeText(string text, int maxWords)` | Tokenizes, normalizes, filters stop words, counts, and returns top N by frequency |

**Processing pipeline**:

1. Split text into tokens on whitespace and common delimiters
2. Convert to lowercase
3. Strip punctuation characters
4. Discard tokens shorter than 3 characters
5. Remove English stop words
6. Count occurrences of each remaining word
7. Sort by count descending
8. Return top `maxWords` entries as `WordFrequency` list

### EnglishStopWords (Core)

Static utility providing O(1) stop word lookup.

| Method | Signature | Description |
|--------|-----------|-------------|
| IsStopWord | `static bool IsStopWord(string word)` | Returns true if the word is in the English stop word list |

**Implementation**: `FrozenSet<string>` with `StringComparer.OrdinalIgnoreCase` containing approximately 175 standard English stop words.

## Data Flow

```text
Client Request                    API Layer                    Core Layer                    Infrastructure
─────────────                    ─────────                    ──────────                    ──────────────
POST /api/word-cloud  ──────▶  GenerateWordCloudEndpoint
  { url, width?, height? }        │
                                  │ validate request
                                  │
                                  ├──▶ IHtmlTextExtractor      ──────────────────────▶  AngleSharpHtmlTextExtractor
                                  │      .ExtractTextAsync(url)                           fetch URL, parse HTML,
                                  │                                                       strip scripts/styles,
                                  │    ◀─── raw visible text ◀────────────────────────    return text
                                  │
                                  ├──▶ TextAnalysisService
                                  │      .AnalyzeText(text, 100)
                                  │      tokenize → lowercase → strip punctuation
                                  │      → remove stop words → count → top 100
                                  │
                                  │    ◀─── List<WordFrequency>
                                  │
                                  ├──▶ IWordCloudGenerator     ──────────────────────▶  SkiaWordCloudGenerator
                                  │      .GenerateAsync(                                  KnowledgePicker.WordCloud
                                  │         frequencies,                                  + SkiaSharp
                                  │         options)                                      → SKBitmap → PNG bytes
                                  │
                                  │    ◀─── byte[] (PNG) ◀────────────────────────────
                                  │
                                  ▼
                               Return PNG image
                               Content-Type: image/png
```

## State Transitions

N/A — this feature is fully stateless. Each request is processed independently with no side effects.

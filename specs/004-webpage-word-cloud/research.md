# Research: Webpage Word Cloud Generator — Library Selection

**Date**: 2026-03-09
**Context**: .NET 10.0 / C# project with SkiaSharp 3.119.2 already in use

---

## 1. HTML Text Extraction Libraries

### Option A: AngleSharp ⭐ RECOMMENDED

| Attribute | Detail |
|---|---|
| **NuGet Package** | `AngleSharp` |
| **Latest Stable Version** | 1.4.0 |
| **License** | MIT |
| **Target Frameworks** | .NET Standard 2.0, .NET 8.0, .NET Framework 4.6.2 |
| **Total Downloads** | ~230M |
| **Last Updated** | ~4 months ago (late 2025) |
| **Maintainer** | Florian Rappl / .NET Foundation |
| **GitHub Stars** | High (well-established project) |

**Visible-text extraction support**: Yes — full W3C DOM API. You can traverse the DOM tree and read `TextContent` on any element. Script and style elements can be filtered by tag name (`script`, `style`, `noscript`) before extracting text. The `TextContent` property on the `body` element already concatenates visible text, making extraction straightforward:

```csharp
var config = Configuration.Default.WithDefaultLoader();
var context = BrowsingContext.New(config);
var document = await context.OpenAsync(url);

// Remove script/style elements before extracting text
foreach (var el in document.QuerySelectorAll("script, style, noscript"))
    el.Remove();

string visibleText = document.Body?.TextContent ?? string.Empty;
```

**Advantages**:

- Standards-compliant HTML5 parser (W3C spec, same as modern browsers)
- Full DOM API including `querySelector`/`querySelectorAll`
- LINQ-friendly collections
- Built-in document loading (`WithDefaultLoader()`) — can fetch pages via URL directly
- .NET Foundation project — strong governance, active maintenance
- Excellent performance, minimal memory allocations
- Extensible architecture (CSS support via `AngleSharp.Css` plugin)
- Well-documented with active community (Gitter, StackOverflow)

**Disadvantages**:

- Slightly larger API surface than needed for simple text extraction (full DOM)
- Built-in loader uses its own HTTP stack; may want to use a custom `HttpClient` for consistency with Aspire/resilience policies

---

### Option B: HtmlAgilityPack

| Attribute | Detail |
|---|---|
| **NuGet Package** | `HtmlAgilityPack` |
| **Latest Stable Version** | 1.12.4 |
| **License** | MIT |
| **Target Frameworks** | .NET Standard 2.0, .NET 7.0, .NET Framework 3.5 |
| **Total Downloads** | ~301M |
| **Last Updated** | ~5 months ago (late 2025) |
| **Maintainer** | ZZZ Projects |

**Visible-text extraction support**: Yes — via XPath or manual DOM traversal. You can load HTML, select `//body` and then filter out `script`/`style` nodes. The `InnerText` property provides concatenated text:

```csharp
var web = new HtmlWeb();
var doc = web.Load(url);
doc.DocumentNode.SelectNodes("//script|//style|//noscript")
    ?.ToList().ForEach(n => n.Remove());
string text = doc.DocumentNode.SelectSingleNode("//body")?.InnerText ?? "";
```

**Advantages**:

- The most widely downloaded HTML parser on NuGet (~301M downloads)
- Simple, well-known API — many Stack Overflow answers available
- XPath support for precise querying
- Tolerant of malformed HTML
- Lightweight, minimal dependencies

**Disadvantages**:

- **Does not implement the W3C DOM standard** — uses its own bespoke API
- Parser is not HTML5 spec-compliant (doesn't handle error recovery like browsers)
- No CSS selector support (`querySelectorAll` equivalent) natively
- Maintained by ZZZ Projects (commercial entity) — less community governance than .NET Foundation
- `HtmlWeb.Load()` is synchronous; async support requires extra work
- API feels dated compared to AngleSharp

---

### Comparison Summary

| Feature | AngleSharp 1.4.0 | HtmlAgilityPack 1.12.4 |
|---|---|---|
| License | MIT | MIT |
| HTML5 Spec Compliance | ✅ Full W3C spec | ❌ Custom tolerant parser |
| CSS Selectors | ✅ `querySelectorAll` | ❌ XPath only |
| Built-in URL Loading | ✅ Async `OpenAsync(url)` | ⚠️ `HtmlWeb.Load()` (sync) |
| Text Extraction | ✅ `.TextContent` (W3C) | ✅ `.InnerText` (custom) |
| .NET Standard 2.0 | ✅ | ✅ |
| .NET Foundation | ✅ | ❌ |
| Total Downloads | ~230M | ~301M |
| Active Maintenance | ✅ | ✅ |

**Recommendation**: **AngleSharp 1.4.0** — Standards-compliant, async-first, .NET Foundation backed, and provides a cleaner API for DOM manipulation. While HtmlAgilityPack has more total downloads (historical advantage), AngleSharp is the more modern choice and better suited for a new project. Its built-in loader and W3C DOM make text extraction clean and reliable.

---

## 2. Word Cloud Image Generation

### Option A: KnowledgePicker.WordCloud ⭐ RECOMMENDED

| Attribute | Detail |
|---|---|
| **NuGet Package** | `KnowledgePicker.WordCloud` |
| **Latest Stable Version** | 1.3.2 |
| **License** | MIT |
| **Target Framework** | .NET Standard 2.0 |
| **Total Downloads** | ~38K |
| **Last Updated** | December 3, 2024 |
| **GitHub Stars** | 35 |
| **GitHub** | [knowledgepicker/word-cloud](https://github.com/knowledgepicker/word-cloud) |

**SkiaSharp integration**: ✅ **Native** — the library's only drawing engine is SkiaSharp-based (`SkGraphicEngine`). Outputs `SKBitmap` directly. Already updated to SkiaSharp 3.119.2 (matching this project's version exactly).

**Layout algorithm**: Spiral placement inspired by the original Wordle algorithm, with **Quadtree** collision detection for fast performance.

**Output formats**: SKBitmap → PNG, or SVG via manual rendering of layout items.

**Key features**:

- `WordCloudInput` accepts a collection of `WordCloudEntry(word, count)`
- Configurable width, height, min/max font size
- Logarithmic font sizing (`LogSizer`)
- Spiral layout (`SpiralLayout`)
- Colorizers: `RandomColorizer`, `SpecificColorizer`
- Custom typeface support

**Usage example** (matches project needs exactly):

```csharp
var entries = frequencies.Select(p => new WordCloudEntry(p.Key, p.Value));
var wordCloud = new WordCloudInput(entries) { Width = 800, Height = 600, MinFontSize = 10, MaxFontSize = 64 };
var sizer = new LogSizer(wordCloud);
using var engine = new SkGraphicEngine(sizer, wordCloud);
var layout = new SpiralLayout(wordCloud);
var wcg = new WordCloudGenerator<SKBitmap>(wordCloud, engine, layout, new RandomColorizer());

using var final = new SKBitmap(wordCloud.Width, wordCloud.Height);
using var canvas = new SKCanvas(final);
canvas.Clear(SKColors.White);
using var bitmap = wcg.Draw();
canvas.DrawBitmap(bitmap, 0, 0);

using var data = final.Encode(SKEncodedImageFormat.Png, 100);
// Return data as byte[] or stream
```

**Advantages**:

- **Purpose-built for this exact use case** — word cloud from word frequencies → PNG
- **Already uses SkiaSharp** — no additional graphics dependency needed
- SkiaSharp version already matches project's version (3.119.2)
- Clean, well-documented API with working console app example
- Quadtree-based layout is fast even for large word sets
- Actively maintained (last commit ~2 weeks ago, dependencies kept up to date via Renovate)
- Used in production by KnowledgePicker.com
- MIT license, .NET Standard 2.0

**Disadvantages**:

- Relatively low download count (~38K) — but niche use case
- Only supports logarithmic font sizing (no linear option)
- Only spiral layout (no alternative layout algorithms)
- Small contributor base (primarily 1 developer)

---

### Option B: Sdcb.WordCloud

| Attribute | Detail |
|---|---|
| **NuGet Package** | `Sdcb.WordCloud` |
| **Latest Stable Version** | 2.0.1 |
| **License** | MIT |
| **Target Framework** | .NET Standard 2.0 |
| **Total Downloads** | ~7K |
| **Last Updated** | October 9, 2024 |
| **GitHub Stars** | 98 |
| **GitHub** | [sdcb/Sdcb.WordCloud](https://github.com/sdcb/Sdcb.WordCloud) |

**SkiaSharp integration**: ✅ Native — uses SkiaSharp internally, outputs `SKBitmap`. The `ToSKBitmap()` method generates the bitmap directly.

**Layout algorithm**: Port of the Python [word_cloud](https://github.com/amueller/word_cloud) library's placement algorithm.

**Key features**:

- Multiple text orientations (horizontal, vertical, prefer horizontal, prefer vertical, random)
- Mask support (words fill a shape)
- SVG and JSON output in addition to bitmap
- Custom font support via `FontManager`
- Richer feature set than KnowledgePicker

**Advantages**:

- More features than KnowledgePicker (masks, text orientations, SVG, JSON serialization)
- Higher GitHub stars (98 vs 35) — more community interest
- Cleaner single-call API: `WordCloud.Create(options).ToSKBitmap()`
- Cross-platform, no System.Drawing dependency

**Disadvantages**:

- **Last committed 2 years ago** — no recent activity on GitHub
- Lower NuGet downloads (~7K)
- Fewer contributors (4 total)
- Less actively maintained than KnowledgePicker
- Prefix-reserved NuGet ID suggests a single-developer project scope

---

### Option C: WordCloudSharp

| Attribute | Detail |
|---|---|
| **NuGet Package** | `WordCloudSharp` |
| **Latest Stable Version** | 1.1.0 |
| **License** | Custom (check repo) |
| **Target Framework** | .NET Standard 2.0 |
| **Total Downloads** | ~7.4K |
| **Last Updated** | January 10, 2023 |

**Not recommended** — uses `System.Drawing` (not SkiaSharp), unclear license, low activity, last updated over 3 years ago.

---

### Option D: Custom SkiaSharp Implementation

Since the project already depends on SkiaSharp 3.119.2, a fully custom word cloud renderer is feasible but significant effort.

**Algorithm approaches**:

1. **Spiral Placement (Wordle-style)**: Place the largest word at center. For each subsequent word, start at center and spiral outward (Archimedean spiral) until a non-overlapping position is found. Check collisions via bounding rectangles or pixel-based hit testing.

2. **Quadtree Collision Detection**: Build a quadtree of placed words' bounding boxes. For each new word, query the quadtree to efficiently check for overlaps. This is what KnowledgePicker.WordCloud uses.

3. **Pixel-based Collision (Integral Image)**: Render each word to a small bitmap, use integral images (summed-area tables) to quickly detect overlapping pixels. This is what Python's `word_cloud` library uses for accurate collision detection with non-rectangular text.

**Estimated effort**: 3–5 days for a basic implementation, 1–2 weeks for a polished one with proper collision detection, font sizing, and randomization.

**Verdict**: Not worth building custom when KnowledgePicker.WordCloud already provides exactly what's needed with native SkiaSharp support and matching version.

---

### Comparison Summary

| Feature | KnowledgePicker 1.3.2 | Sdcb.WordCloud 2.0.1 | Custom SkiaSharp |
|---|---|---|---|
| License | MIT | MIT | N/A |
| SkiaSharp Native | ✅ (3.119.2) | ✅ | ✅ |
| PNG Output | ✅ | ✅ | ✅ |
| Layout Algorithm | Spiral + Quadtree | Spiral (Python port) | Must implement |
| Active Maintenance | ✅ (2 weeks ago) | ⚠️ (2 years ago) | N/A |
| Mask Support | ❌ | ✅ | Must implement |
| Text Orientations | ❌ (horizontal only) | ✅ (5 modes) | Must implement |
| Implementation Effort | Zero — drop-in | Zero — drop-in | 3–14 days |
| API Simplicity | Good | Very Good | N/A |

**Recommendation**: **KnowledgePicker.WordCloud 1.3.2** — actively maintained, native SkiaSharp 3.119.2 support (exact version match with the project), MIT license, proven Quadtree-based layout algorithm, and simple API that maps directly to the feature requirements. Sdcb.WordCloud has more features but is essentially abandoned (last commit 2 years ago). The extra features (masks, text orientations) are not needed for the spec's requirements.

---

## 3. English Stop Words

### Option A: Embedded Static `HashSet<string>` ⭐ RECOMMENDED

**Approach**: Define a `static readonly FrozenSet<string>` or `HashSet<string>` containing standard English stop words directly in the codebase.

**Standard list sizes**:

- **Minimal** (NLTK-style): ~30–40 words (articles, prepositions, conjunctions, pronouns)
- **Standard** (Fox/Porter-style): ~175–425 words (includes common verbs, adverbs, and auxiliary words)
- **Aggressive** (Google-derived): Up to 10,000 most common English words (overkill for word clouds)

A **standard list of ~175 words** is ideal for word cloud generation. This covers:

- Articles: a, an, the
- Prepositions: in, on, at, to, for, with, by, from, of, about, etc.
- Conjunctions: and, but, or, nor, yet, so
- Pronouns: I, you, he, she, it, we, they, me, him, her, us, them, etc.
- Auxiliary verbs: is, am, are, was, were, be, been, being, have, has, had, do, does, did, will, would, shall, should, can, could, may, might, must
- Common adverbs: very, just, also, not, only, more, most, now, then, etc.
- Common words: this, that, these, those, which, who, what, when, where, how, etc.

**Advantages**:

- Zero additional dependencies
- Instant `O(1)` lookup with `FrozenSet<string>` (.NET 8+) or `HashSet<string>`
- Complete control over which words are included
- Easy to maintain and extend
- No transitive dependency issues or version conflicts
- Can use `FrozenSet<string>` for optimal read performance since the set never changes

**Disadvantages**:

- Must curate the list yourself (one-time effort)
- No multi-language support without adding more lists

---

### Option B: dotnet-stop-words NuGet Package

| Attribute | Detail |
|---|---|
| **NuGet Package** | `dotnet-stop-words` |
| **Latest Stable Version** | 1.1.0 |
| **License** | BSD-3-Clause |
| **Target Framework** | .NET Standard 2.0 |
| **Total Downloads** | ~202K |
| **Last Updated** | May 11, 2020 (~6 years ago) |
| **GitHub** | [hklemp/dotnet-stop-words](https://github.com/hklemp/dotnet-stop-words) |

**Features**:

- Stop word lists for 27+ languages (Arabic, English, French, German, Spanish, etc.)
- Simple API: `StopWords.GetStopWords("en")` returns `string[]`
- Extension method: `"some text".RemoveStopWords("en")`
- Based on the well-known [Alir3z4/stop-words](https://github.com/Alir3z4/stop-words) collection

**Advantages**:

- Multilingual support out of the box
- Community-curated word lists
- Permissive license (BSD-3-Clause)

**Disadvantages**:

- **Not actively maintained** — last commit 3 years ago, last release 6 years ago
- Has a dependency on `Newtonsoft.Json` (unnecessary for this project)
- Only 13 GitHub stars, 3 contributors
- Adding a NuGet dependency for what amounts to a static string array is overkill
- No performance optimization (returns `string[]`, not a `HashSet` or `FrozenSet`)

---

### Recommendation

**Embed a static `FrozenSet<string>` (~175 words)** directly in the codebase. Reasons:

1. **Zero dependencies** — no transitive `Newtonsoft.Json` or other baggage
2. **Optimal performance** — `FrozenSet<string>` with `StringComparer.OrdinalIgnoreCase` provides `O(1)` case-insensitive lookup
3. **Full control** — easy to add/remove words based on word cloud quality feedback
4. **Simplicity** — a single static class, ~30 lines of code
5. The spec only requires English stop words — no need for multi-language support
6. Industry standard practice — most text processing tools embed their stop word lists

Example implementation pattern:

```csharp
public static class EnglishStopWords
{
    private static readonly FrozenSet<string> Words = new[]
    {
        "a", "about", "above", "after", "again", "against", "all", "am", "an", "and",
        "any", "are", "aren't", "as", "at", "be", "because", "been", "before", "being",
        "below", "between", "both", "but", "by", "can", "can't", "cannot", "could",
        "couldn't", "did", "didn't", "do", "does", "doesn't", "doing", "don't", "down",
        "during", "each", "few", "for", "from", "further", "get", "got", "had", "hadn't",
        "has", "hasn't", "have", "haven't", "having", "he", "her", "here", "hers",
        "herself", "him", "himself", "his", "how", "i", "if", "in", "into", "is",
        "isn't", "it", "its", "itself", "just", "let", "me", "might", "more", "most",
        "must", "mustn't", "my", "myself", "no", "nor", "not", "now", "of", "off",
        "on", "once", "only", "or", "other", "ought", "our", "ours", "ourselves",
        "out", "over", "own", "per", "same", "shall", "shan't", "she", "should",
        "shouldn't", "so", "some", "such", "than", "that", "the", "their", "theirs",
        "them", "themselves", "then", "there", "these", "they", "this", "those",
        "through", "to", "too", "under", "until", "up", "us", "very", "was", "wasn't",
        "we", "were", "weren't", "what", "when", "where", "which", "while", "who",
        "whom", "why", "will", "with", "won't", "would", "wouldn't", "you", "your",
        "yours", "yourself", "yourselves"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public static bool IsStopWord(string word) => Words.Contains(word);
}
```

---

## Summary of Recommendations

| Area | Recommendation | Package Version | License |
|---|---|---|---|
| **HTML Text Extraction** | **AngleSharp** | 1.4.0 | MIT |
| **Word Cloud Generation** | **KnowledgePicker.WordCloud** | 1.3.2 | MIT |
| **Stop Words** | **Embedded static `FrozenSet<string>`** | N/A (no package) | N/A |

### New Packages to Add to Directory.Packages.props

```xml
<PackageVersion Include="AngleSharp" Version="1.4.0" />
<PackageVersion Include="KnowledgePicker.WordCloud" Version="1.3.2" />
```

### Key Architectural Notes

1. **AngleSharp** can load pages directly via `BrowsingContext.OpenAsync(url)`, but for consistency with Aspire's `HttpClient` resilience and service discovery patterns, consider fetching HTML via `HttpClient` first, then parsing the string with AngleSharp's `HtmlParser`.

2. **KnowledgePicker.WordCloud** already depends on SkiaSharp and its version has been updated to match 3.119.2 — no version conflicts expected.

3. **Stop words** should live in the Core project as a static utility, since they're a domain concept (text processing) not an infrastructure concern.

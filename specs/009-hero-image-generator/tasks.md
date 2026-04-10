# Tasks: Hero Image Generator

**Input**: Design documents from `/specs/009-hero-image-generator/`
**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/hero-image-api.md](contracts/hero-image-api.md)

**Tests**: Included — constitution mandates test coverage for all business logic.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to
- Exact file paths are included in every task description

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Acquire and embed font files — the only external asset acquisition needed before coding can begin. No new packages required (SkiaSharp already in project).

- [x] T001 Download JetBrains Mono Bold (`JetBrainsMono-Bold.ttf`) and Regular (`JetBrainsMono-Regular.ttf`) from `https://fonts.google.com/specimen/JetBrains+Mono` (OFL 1.1 licensed) and place both `.ttf` files in `src/BarretApi.Infrastructure/Fonts/`
- [x] T002 Add both `.ttf` files as `<EmbeddedResource>` items in `src/BarretApi.Infrastructure/BarretApi.Infrastructure.csproj` so they are bundled into the assembly at build time

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core types and abstractions that ALL user stories depend on. Must be complete before any user story work begins.

**⚠️ CRITICAL**: No user story implementation can start until this phase is complete.

- [x] T003 [P] Create `HeroImageOptions` in `src/BarretApi.Core/Models/HeroImageOptions.cs` — strongly-typed configuration record with properties: `FaceImagePath` (string, required), `LogoImagePath` (string, required), `DefaultBackgroundPath` (string, required), `OutputWidth` (int, default 1280), `OutputHeight` (int, default 720); add `SectionName = "HeroImage"` constant
- [x] T004 [P] Create `HeroImageGenerationCommand` in `src/BarretApi.Core/Models/HeroImageGenerationCommand.cs` — immutable record with properties: `Title` (string, required), `Subtitle` (string?, nullable), `CustomBackgroundBytes` (byte[]?, nullable)
- [x] T005 [P] Create `IHeroImageGenerator` in `src/BarretApi.Core/Interfaces/IHeroImageGenerator.cs` — interface with single method: `Task<byte[]> GenerateAsync(HeroImageGenerationCommand command, CancellationToken cancellationToken = default)`
- [x] T006 [P] Create `GenerateHeroImageRequest` in `src/BarretApi.Api/Features/HeroImage/GenerateHeroImageRequest.cs` — sealed class with properties: `Title` (string?), `Subtitle` (string?), `BackgroundImage` (IFormFile?)

**Checkpoint**: All core types exist — user story implementation can now begin

---

## Phase 3: User Story 1 — Generate Hero Image with Title Only (Priority: P1) 🎯 MVP

**Goal**: A caller can POST a title string to `POST /api/hero-image` and receive a 1280×720 PNG with face image (lower-right), logo (lower-left), faded generic background, and title text rendered in JetBrains Mono Bold between the two overlay images.

**Independent Test**: `curl -X POST http://localhost:5000/api/hero-image -F "title=Getting Started with .NET 10" -o hero.png` — verify `hero.png` is a valid 1280×720 PNG containing recognizable face, logo, faded background, and title text. Verify empty/missing title returns HTTP 400.

### Implementation for User Story 1

- [x] T007 [US1] Create `SkiaHeroImageGenerator` in `src/BarretApi.Infrastructure/Services/SkiaHeroImageGenerator.cs` implementing `IHeroImageGenerator` — constructor takes `IOptions<HeroImageOptions>` and `ILogger<SkiaHeroImageGenerator>`; in `GenerateAsync`: (1) load embedded font bytes for JetBrains Mono Bold via `Assembly.GetExecutingAssembly().GetManifestResourceStream()`, (2) load default background from `options.DefaultBackgroundPath` using `SKBitmap.Decode`, scale to fill 1280×720, (3) draw background on canvas, then draw full-canvas semi-transparent black rectangle (`SKColors.Black` at alpha ~153 / 60%) for fade effect, (4) load and scale logo from `options.LogoImagePath` to ~180px height maintaining aspect ratio, composite lower-left with 30px padding, (5) load and scale face from `options.FaceImagePath` to ~180px height maintaining aspect ratio, composite lower-right with 30px padding, (6) calculate text region width as canvas width minus logo right-edge minus right-margin of face region, (7) render title text in JetBrains Mono Bold starting at 56px — use `SKPaint.MeasureText()` and reduce font size by 2px increments until text fits within the text region width or minimum 18px is reached; if still too long, break into up to 2 lines, (8) encode using `SKEncodedImageFormat.Png` at quality 100 and return `byte[]`
- [x] T008 [P] [US1] Create `GenerateHeroImageValidator` in `src/BarretApi.Api/Features/HeroImage/GenerateHeroImageValidator.cs` — `Validator<GenerateHeroImageRequest>` with rules: title `NotEmpty()` with message "Title is required.", title `MaximumLength(200)` when not empty with message "Title must not exceed 200 characters."
- [x] T009 [US1] Create `GenerateHeroImageEndpoint` in `src/BarretApi.Api/Features/HeroImage/GenerateHeroImageEndpoint.cs` — `Endpoint<GenerateHeroImageRequest>` with constructor taking `IHeroImageGenerator` and `ILogger<GenerateHeroImageEndpoint>`; `Configure()` sets `Post("/api/hero-image")` and adds `Summary` with description, example, and 200/400/422/500 response docs; `HandleAsync()` maps request to `HeroImageGenerationCommand` (subtitle null if empty/whitespace, CustomBackgroundBytes null for now), calls `_generator.GenerateAsync()`, sets response `Content-Type: image/png`, writes bytes to response body
- [x] T010 [US1] Register `IHeroImageGenerator` → `SkiaHeroImageGenerator` (scoped) and configure `HeroImageOptions` in `src/BarretApi.Api/Program.cs` — use `IWebHostEnvironment.ContentRootPath` to resolve absolute paths to `images/barretcircle2.png`, `images/barret-blake-logo-1024.png`, and `images/generic-background.jpg`; add `builder.Services.Configure<HeroImageOptions>(...)` binding the resolved absolute paths inline (no appsettings.json entry needed)

**Checkpoint**: US1 fully functional — `POST /api/hero-image` with title only returns a valid hero PNG

---

## Phase 4: User Story 2 — Generate Hero Image with Title and Subtitle (Priority: P2)

**Goal**: A caller can include an optional `subtitle` form field; the generated image renders the subtitle below the title in JetBrains Mono Regular at a visibly smaller font size, with correct spacing and no overlap with face or logo images.

**Independent Test**: `curl -X POST http://localhost:5000/api/hero-image -F "title=Blazor Deep Dive" -F "subtitle=Part 3: Component Lifecycle" -o hero.png` — verify both title and subtitle appear on image, title is visibly larger, both texts fit between the logo and face images. Verify overlong subtitle is scaled/wrapped without overlap.

### Implementation for User Story 2

- [x] T011 [US2] Extend `SkiaHeroImageGenerator.GenerateAsync` in `src/BarretApi.Infrastructure/Services/SkiaHeroImageGenerator.cs` to handle subtitle: (1) load JetBrains Mono Regular embedded font, (2) when `command.Subtitle` is non-null and non-whitespace, compute subtitle text region as same horizontal zone as title, (3) render subtitle in JetBrains Mono Regular starting at 32px — apply same dynamic scaling (reduce by 2px until fits or minimum 14px, then wrap to 2 lines), (4) vertically stack title + spacing + subtitle centered in the canvas area above the lower-third (where face/logo live); when only title is present, vertically center title in the same zone
- [x] T012 [P] [US2] Add subtitle validation rule to `GenerateHeroImageValidator` in `src/BarretApi.Api/Features/HeroImage/GenerateHeroImageValidator.cs` — `RuleFor(x => x.Subtitle).MaximumLength(300).When(x => !string.IsNullOrWhiteSpace(x.Subtitle)).WithMessage("Subtitle must not exceed 300 characters.")`

**Checkpoint**: US2 fully functional — title + subtitle render together with correct hierarchy and layout

---

## Phase 5: User Story 3 — Generate Hero Image with Custom Background (Priority: P3)

**Goal**: A caller can upload a JPEG or PNG background image (max 10 MB) via `backgroundImage` form field; the uploaded image (faded with the same dark overlay) is used instead of the generic background.

**Independent Test**: `curl -X POST http://localhost:5000/api/hero-image -F "title=Azure Functions" -F "backgroundImage=@my-bg.jpg" -o hero.png` — verify the returned PNG uses the uploaded image as the background (with fading). Verify non-image files return 400, corrupt image returns 422, file > 10 MB returns 400.

### Implementation for User Story 3

- [x] T013 [US3] Extend `GenerateHeroImageEndpoint.HandleAsync` in `src/BarretApi.Api/Features/HeroImage/GenerateHeroImageEndpoint.cs` to read `request.BackgroundImage` when present: copy `IFormFile` stream to `byte[]` using `MemoryStream`; attempt `SKBitmap.Decode(bytes)` and return 422 if decoding fails; pass bytes as `CustomBackgroundBytes` in the `HeroImageGenerationCommand`
- [x] T014 [P] [US3] Add background image validation rules to `GenerateHeroImageValidator` in `src/BarretApi.Api/Features/HeroImage/GenerateHeroImageValidator.cs` — when `BackgroundImage` is not null: check `ContentType` is `image/jpeg` or `image/png` with message "Background image must be JPEG or PNG format."; check `Length <= 10_485_760` with message "Background image must not exceed 10 MB."
- [x] T015 [US3] Extend `SkiaHeroImageGenerator.GenerateAsync` in `src/BarretApi.Infrastructure/Services/SkiaHeroImageGenerator.cs` — extract background-loading logic into a private helper method; when `command.CustomBackgroundBytes` is not null, use `SKBitmap.Decode(command.CustomBackgroundBytes)` instead of loading from the default path; the scale-to-fill and fade overlay logic is identical for both paths

**Checkpoint**: All 3 user stories fully functional and independently testable

---

## Phase 6: Tests & Polish

**Purpose**: Unit test coverage for business logic and documentation update.

- [x] T016 [P] Create `SkiaHeroImageGenerator_GenerateAsync_Tests.cs` in `tests/BarretApi.Infrastructure.UnitTests/Services/` — xUnit tests with Arrange-Act-Assert using real asset files (face, logo, background from `src/BarretApi.Api/images/` copied as test content files) or small synthetic in-memory images; test cases: `ReturnsValidPngBytes_GivenTitleOnly`, `ReturnsValidPngBytes_GivenTitleAndSubtitle`, `ReturnsValidPngBytes_GivenCustomBackground`, `ThrowsArgumentException_GivenNullCommand`; use `Shouldly` for assertions (`imageBytes.Length.ShouldBeGreaterThan(0)`)
- [x] T017 [P] Create `GenerateHeroImageValidator_Tests.cs` in `tests/BarretApi.Api.UnitTests/Features/HeroImage/` — xUnit `[Theory]` + `[InlineData]` tests with NSubstitute/Shouldly; test cases: `ReturnsNoErrors_GivenValidTitleOnly`, `ReturnsErrors_GivenEmptyTitle`, `ReturnsErrors_GivenTitleExceeding200Chars`, `ReturnsErrors_GivenSubtitleExceeding300Chars`, `ReturnsErrors_GivenBackgroundImageOver10MB`, `ReturnsErrors_GivenBackgroundImageNotJpegOrPng`
- [x] T018 Update `README.md` with `POST /api/hero-image` endpoint documentation — add to the existing Feature Endpoints section with description, request format (multipart/form-data fields), response format (1280×720 PNG), example `curl` commands for all three scenarios (title-only, title + subtitle, custom background), and validation error examples

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — acquire font files first; T001 and T002 can run sequentially
- **Phase 2 (Foundational)**: Depends on Phase 1 completion — T003, T004, T005, T006 can all run in parallel
- **Phase 3 (US1)**: Depends on Phase 2. T008 [P] can run concurrent with T007. T009 depends on T007. T010 depends on T007 + T009
- **Phase 4 (US2)**: Depends on Phase 3. T011 depends on T007 (generator). T012 [P] can run concurrent with T011
- **Phase 5 (US3)**: Depends on Phase 4. T013 and T015 sequential (both touch same files). T014 [P] can run with T013
- **Phase 6 (Polish)**: Depends on Phase 5. T016 and T017 can run in parallel

### User Story Dependencies

| Story | Can start after | Dependencies on other stories |
|-------|-----------------|-------------------------------|
| US1 (P1) | Phase 2 complete | None — fully standalone |
| US2 (P2) | Phase 3 complete | Extends US1's generator and validator |
| US3 (P3) | Phase 4 complete | Extends US1 and US2's generator, endpoint, and validator |

### Parallel Opportunities Per Story

**US1 (Phase 3)**:

```
T007 (SkiaHeroImageGenerator) ──────────────────────── T009 (Endpoint) ── T010 (DI reg)
T008 (Validator) [P] ──────────────────────────────────────────────────────────────────
```

**US2 (Phase 4)**:

```
T011 (Generator subtitle) ─────────────────────────────────────────────
T012 (Validator subtitle) [P] ─────────────────────────────────────────
```

**US3 (Phase 5)**:

```
T013 (Endpoint file binding) ── T015 (Generator custom bg)
T014 (Validator bg rules) [P] ─────────────────────────
```

**Polish (Phase 6)**:

```
T016 (Generator tests) [P] ────────────────────────────
T017 (Validator tests) [P] ────────────────────────────
T018 (README) ─────────────────────────────────────────
```

---

## Implementation Strategy

**Suggested MVP scope**: Complete Phase 1, Phase 2, and Phase 3 (US1) only — this delivers a fully working hero image endpoint that can generate branded images from a title string.

**Incremental delivery**:

1. Phase 1 + 2 + 3 → ship US1 (title-only hero image) — usable for most content creation needs
2. Phase 4 → ship US2 (add subtitle rendering) — increases content expressiveness
3. Phase 5 → ship US3 (custom background upload) — enables per-content visual variety
4. Phase 6 → tests and docs — ensures quality and discoverability


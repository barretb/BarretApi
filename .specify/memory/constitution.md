<!--
  Sync Impact Report
  ==================
  Version change: 0.0.0 → 1.0.0 (MAJOR: initial ratification)
  Modified principles: N/A (initial version)
  Added sections:
    - Core Principles (7 principles)
    - Technology Stack & Build Configuration
    - Development Workflow & Quality Gates
    - Governance
  Removed sections: N/A
  Templates requiring updates:
    - .specify/templates/plan-template.md ✅ compatible
      (Constitution Check section is generic; plan authors fill
      gates from this constitution)
    - .specify/templates/spec-template.md ✅ compatible
      (no constitution-specific references; requirements and
      scenarios remain generic)
    - .specify/templates/tasks-template.md ✅ compatible
      (phase structure and test-first guidance align with
      Principle III)
  Follow-up TODOs: None
-->

# BarretApi Constitution

## Core Principles

### I. Code Quality & Consistency (NON-NEGOTIABLE)

- All projects MUST enable `TreatWarningsAsErrors`.
- File-scoped namespaces MUST be used in every `.cs` file;
  block-scoped namespaces are forbidden.
- Code MUST follow Allman-style bracing, tab indentation, and
  CRLF line endings as defined in `.editorconfig`.
- Naming conventions MUST be enforced:
  - Private fields: `_camelCase`
  - Constants: `PascalCase`
  - Interfaces: `I` prefix
  - Async methods: `Async` suffix
- Primary constructors MUST assign parameters to readonly fields.
- Pattern matching MUST be preferred over `is`-with-cast or
  `as`-with-null-check.
- `dotnet format` MUST pass with zero violations before any code
  is merged.

**Rationale**: Uniform style eliminates cognitive overhead during
reviews and prevents entire classes of bugs caught by
warnings-as-errors.

### II. Clean Architecture & REPR Design

- The REPR (Request-Endpoint-Response) pattern MUST be used for
  all API endpoints via FastEndpoints 7.x or greater.
- Domain-Driven Design principles MUST guide domain modelling
  where applicable.
- Classes MUST have a single responsibility; composition MUST be
  preferred over inheritance.
- Built-in .NET Dependency Injection MUST be the sole DI
  mechanism; external DI containers are forbidden.
- API layer messages MUST use `*Request` / `*Response` suffixes.
- Use-case layer messages MUST use `*Command` / `*Query` suffixes.
- DTOs MUST be used for data transfer between layers; domain
  entities MUST NOT be exposed directly.
- DTO names MUST use `*Details` or `*Summary` suffixes to
  indicate level of detail.
- Interfaces and their implementations MUST NOT reside in the
  same project.

**Rationale**: Clean boundaries between layers ensure testability,
replaceability, and clear ownership of each concern.

### III. Test-Driven Quality Assurance

- xUnit MUST be the sole test framework for all test projects.
- Tests MUST follow the Arrange-Act-Assert pattern with
  whitespace separation (no inline comments labelling sections).
- Test class naming: `ClassName_MethodName_Tests.cs`.
- Test method naming: `DoesSomething_GivenSomeCondition` —
  reading the fully qualified name MUST form a natural-language
  sentence.
- `[Fact]` for single-case tests; `[Theory]` with `[InlineData]`
  for parameterised tests.
- NSubstitute MUST be used for mocking; Shouldly MUST be used
  for assertions.
- Commercially licensed testing libraries (e.g., FluentAssertions,
  Moq) are forbidden.
- Unit and integration tests MUST be separated into distinct
  projects or folders.
- Only code with business logic under the team's control MUST be
  tested; framework code MUST NOT be tested.

**Rationale**: Consistent, enforceable testing conventions make
tests readable-as-documentation and keep the build free from
licensing risk.

### IV. Centralized Configuration via Aspire

- All service configuration and service discovery MUST be managed
  by the Aspire AppHost project.
- No project other than the AppHost MAY contain
  `appsettings.json`, `appsettings.{Environment}.json`, or
  User Secrets.
- `IConfiguration` and `IOptions<T>` MUST be used for
  configuration access; strongly-typed settings classes MUST be
  created for complex configurations.
- Sensitive data MUST never be hardcoded in source; User Secrets
  in the AppHost MUST be used for development secrets.

**Rationale**: A single configuration source of truth eliminates
environment drift and prevents secrets from leaking into non-host
projects.

### V. Secure by Design

- OWASP secure coding guidelines MUST be followed.
- All user inputs MUST be validated and sanitized.
- Parameterized queries or ORM features MUST be used; raw string
  concatenation in queries is forbidden.
- Authentication and authorization mechanisms MUST be implemented
  for every protected endpoint.
- All communications MUST use HTTPS.
- FluentValidation MUST be preferred for complex validation in UI
  layers; invalid inputs in UI layers MUST produce user-friendly
  messages rather than exceptions.
- Invalid inputs in non-UI layers MUST throw exceptions via guard
  clauses.
- HTTP endpoints MUST return appropriate status codes for failures
  (e.g., 400, 401, 403, 404).

**Rationale**: Security is a first-class concern; baking it into
coding standards prevents vulnerabilities from reaching production.

### VI. Observability & Structured Logging

- `Microsoft.Extensions.Logging` MUST be the sole logging
  abstraction.
- Log messages MUST use structured logging (message templates with
  named placeholders).
- Appropriate log levels MUST be applied: Trace, Debug,
  Information, Warning, Error, Critical.
- Correlation IDs MUST be included when tracing requests across
  services.
- Sensitive information MUST NOT appear in log output.

**Rationale**: Structured, levelled logging enables efficient
troubleshooting in distributed systems without compromising
security.

### VII. Simplicity & Maintainability

- Methods MUST be kept under 20 lines; longer methods MUST be
  refactored.
- Argument lists exceeding 4 parameters MUST use parameter
  objects.
- `async`/`await` MUST be used for all I/O-bound operations.
- Code MUST follow YAGNI — features not yet required MUST NOT be
  implemented speculatively.

**Rationale**: Small, focused units of code are easier to read,
test, and maintain; premature abstraction is a liability.

## Technology Stack & Build Configuration

- **Framework**: .NET 10.0 (`net10.0`), Aspire 13
- **Language**: Latest C# features enabled; nullable reference
  types and implicit usings enabled project-wide.
- **Build**: MSBuild Central Package Management
  (`ManagePackageVersionsCentrally=true`).
  - All package versions MUST be declared in
    `Directory.Packages.props`.
  - Individual `.csproj` files MUST NOT specify package versions.
- **Shared Properties**: `Directory.Build.props` MUST define
  compiler settings inherited by every project.
- **Solution Format**: `Template.slnx` (XML format).
- **Project Layout**:
  - Production code in `src/`
  - Test code in `tests/`

## Development Workflow & Quality Gates

- Production projects MUST reside in `src/`; test projects in
  `tests/`.
- `dotnet build` MUST complete with zero errors and zero warnings
  before a PR is opened.
- `dotnet test` MUST pass for all test projects before merge.
- `dotnet format` MUST report zero violations before merge.
- Documentation MUST be written in Markdown with proper heading
  hierarchy, syntax-highlighted code snippets, and blank lines
  before and after lists and code blocks.
- New packages MUST be added via Central Package Management
  (`Directory.Packages.props`) — never inline in a `.csproj`.

## Governance

- This constitution supersedes all other project practices and
  conventions where conflicts arise.
- All pull requests and code reviews MUST verify compliance with
  these principles.
- Complexity beyond what the principles allow MUST be explicitly
  justified in the PR description.
- Amendments to this constitution MUST include:
  1. A description of the change and its rationale.
  2. An updated version number following semantic versioning:
     - MAJOR: backward-incompatible governance or principle
       changes.
     - MINOR: new principle/section added or materially expanded.
     - PATCH: clarifications, wording, or non-semantic
       refinements.
  3. A migration plan if existing code is affected.
  4. An updated Sync Impact Report (HTML comment at the top of
     this file).
- Compliance reviews SHOULD be conducted quarterly to ensure the
  constitution reflects current project needs.
- Use `AGENTS.md` for runtime development guidance that
  supplements this constitution.

**Version**: 1.0.0 | **Ratified**: 2026-02-28 | **Last Amended**: 2026-02-28

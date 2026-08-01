# Code Review Assistant

[![Tests: 6 passing](https://img.shields.io/badge/tests-6%20passing-brightgreen)](#tests)

A small, deterministic C# command-line tool that reviews C# source code locally. It uses Roslyn syntax trees and semantic models and does not send source code to an AI service.

The analyzer detects:

- methods longer than 50 lines (`CRA001`);
- unclear one- or two-character local variable names (`CRA002`);
- repeated non-trivial statements (`CRA003`).

Each file receives a maintainability score out of 100. This score is a lightweight signal, not a substitute for compiler diagnostics or human review.

## Requirements

- .NET SDK 10

## Build and run

```bash
dotnet build
dotnet run --project src/CodeReviewAssistant -- path/to/file-or-directory
```

To review this repository:

```bash
dotnet run --project src/CodeReviewAssistant -- src
```

## Examples

### Example 1: clean source code

Run the analyzer against [`examples/CleanCalculator.cs`](examples/CleanCalculator.cs):

```bash
dotnet run --project src/CodeReviewAssistant -- examples/CleanCalculator.cs
```

The analyzer finds no maintainability issues and awards the file a score of 100:

![Terminal output for a clean code review](docs/images/clean-review.svg)

### Example 2: source code that needs review

[`examples/NeedsReview.cs`](examples/NeedsReview.cs) contains a short local variable name and a repeated statement. Analyze it with:

```bash
dotnet run --project src/CodeReviewAssistant -- examples/NeedsReview.cs
```

Roslyn identifies both findings with their source lines and applies the corresponding score penalties:

![Terminal output for a review with findings](docs/images/review-findings.svg)

## Tests

The smoke-test project avoids an external test framework and can be run directly:

```bash
dotnet run --project tests/CodeReviewAssistant.Tests
```

## Current scope

Roslyn provides formatting-independent parsing and symbol-aware analysis. The current rules remain intentionally focused: each file is compiled independently, so analysis that requires a complete project compilation is not yet available. Future versions should add project/solution loading, configuration, and machine-readable output for CI integrations.

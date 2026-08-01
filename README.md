# Code Review Assistant

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

## Tests

The smoke-test project avoids an external test framework and can be run directly:

```bash
dotnet run --project tests/CodeReviewAssistant.Tests
```

## Current scope

Roslyn provides formatting-independent parsing and symbol-aware analysis. The current rules remain intentionally focused: each file is compiled independently, so analysis that requires a complete project compilation is not yet available. Future versions should add project/solution loading, configuration, and machine-readable output for CI integrations.

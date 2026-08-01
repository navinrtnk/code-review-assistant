# Code Review Assistant

A small, deterministic C# command-line tool that reviews C# source code locally. It does not send source code to an AI service.

The initial version detects:

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

The first commit avoids external test-framework packages, so the smoke-test project can run in an offline environment:

```bash
dotnet run --project tests/CodeReviewAssistant.Tests
```

## Current scope

This is an intentionally small foundation. Its syntax recognition is heuristic and currently targets ordinary C# formatting. A production version should use Roslyn syntax trees and semantic models, add configuration, and expose machine-readable output for CI integrations.

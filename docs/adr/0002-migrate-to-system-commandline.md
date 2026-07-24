# ADR-0002: Migrate to System.CommandLine 2.0 (supersedes ADR-0001 point 5)

## Status

Accepted — 2026-07-23

## Context

ADR-0001 (point 5) chose a small hand-written CLI argument parser for
`v0.1.0-alpha.1` because `System.CommandLine` was still pre-release at the
time, and explicitly said this would be "revisited... once
`System.CommandLine` (or an alternative) has a stable release."

`System.CommandLine` 2.0.0 reached stable GA in November 2025 (current:
2.0.10), ships with zero dependencies, and is the library used by the
`dotnet` CLI itself. That condition has been met.

The hand-rolled parser (`ParsedArguments`) also had real gaps that would
only grow: no `--help`/`--version` per-subcommand text, no argument
validation beyond "is this a number", no tab completion, and every new
flag meant editing a manual switch list by hand.

## Decision

Replace `Cedar.BenchmarkGate.Tool.Commands.ParsedArguments` and the manual
dispatch in `Program.cs` with `System.CommandLine` 2.0 (GA API, not the old
beta4 API — the two are meaningfully different: `SetAction` instead of
`SetHandler`, `ParseResult` passed directly instead of `InvocationContext`,
no `CommandLineBuilder`/middleware).

`check` and `capture` become `Command` instances with typed `Option<T>`
declarations; each command's action still delegates to the same
`CheckCommand.Run` / `CaptureCommand.Run` static methods so the actual
orchestration logic (parse → load baseline → evaluate → report → exit
code) is unchanged — only argument acquisition changes.

## Consequences

- `Cedar.BenchmarkGate.Tool.Commands.ParsedArguments` and
  `CliArgumentException` are removed.
- `Program.cs` becomes declarative: build a `RootCommand`, add `check` and
  `capture` as subcommands, invoke.
- `--help`/`--version` are provided by `System.CommandLine` itself rather
  than hand-written text in `Program.cs`.
- Exit code mapping still goes through `Core.Evaluation.ExitCodes` — this
  library only owns argument acquisition, not the decision of what exit
  code means what (that stays a Core concern per ADR-0001).
- `System.CommandLine` is added to `Directory.Packages.props` and
  referenced from `Cedar.BenchmarkGate.Tool` only — `Core` and the
  `BenchmarkDotNet` adapter must not take a dependency on it.

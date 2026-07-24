# ADR-0001: The CLI is the product and BenchmarkDotNet is the measurement engine

## Status

Accepted — 2026-07-20

**Point 5 (CLI argument parsing) superseded by [ADR-0002](0002-migrate-to-system-commandline.md) — 2026-07-23.**

## Context

Cedar.BenchmarkGate exists to turn BenchmarkDotNet output into an enforceable
performance contract, without requiring a SaaS account, hosted database, or
network access at evaluation time.

We need an early, explicit decision about what the product *is*, so that
later decisions (packaging, distribution, architecture layering) have a
consistent reference point instead of being re-litigated per feature.

## Decision

1. **The CLI (`cedar-benchmark-gate`) is the product.** It is distributed as
   a .NET tool via NuGet. Every other distribution channel — the GitHub
   Action, and later an embeddable library — is a thin adapter over the same
   CLI behavior, not a parallel implementation.

2. **BenchmarkDotNet remains the measurement engine.** Cedar.BenchmarkGate
   never executes benchmarks itself. It only consumes BenchmarkDotNet's full
   JSON exporter output. This keeps the tool local-first: no orchestration
   of test runs, no assumptions about CI topology, no network calls during
   `check`.

3. **Baselines and policies are reviewable repository artifacts** (committed
   JSON files), not rows in a hosted database. A performance budget change
   must be visible in a pull-request diff, same as any other code change.

4. **Dependency direction is one-way**: `Tool → BenchmarkDotNet adapter →
   Core`. `Core` has zero dependency on BenchmarkDotNet, CLI frameworks, or
   I/O — it is pure domain model and pure evaluation functions. This is what
   lets the same evaluation logic be reused by the future GitHub Action and
   library without duplicating regression logic in each adapter.

5. **CLI argument parsing**: for v0.1.0-alpha.1 we use a small, explicit,
   hand-written argument parser rather than `System.CommandLine`. Rationale:
   `System.CommandLine` was still pre-release at the time of writing and
   pulling in an unstable dependency for a v0.1 alpha isn't justified yet.
   The parsing surface for `check` and `capture` is small enough to write by
   hand without meaningfully increasing maintenance cost. This will be
   revisited in a follow-up ADR once `System.CommandLine` (or an alternative)
   has a stable release, or once the command surface grows enough that a
   hand-rolled parser becomes the more expensive choice.

## Consequences

- Every command implementation must go through `Core` for evaluation logic;
  `Tool` and the BenchmarkDotNet adapter are orchestration and I/O only.
- The GitHub Action (deferred past v0.1) will shell out to the installed
  tool rather than re-implementing evaluation — it inherits this ADR by
  construction.
- Because `Core` has no I/O, its evaluation functions are trivially unit-
  testable without file-system or process fixtures.
- Revisiting the CLI parser decision is expected and tracked, not a sign the
  ADR failed — see point 5 above.

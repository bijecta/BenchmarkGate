# Architecture

## Product boundary

BenchmarkGate's purpose is not to replace BenchmarkDotNet, PerfView,
dotnet-trace, or application profilers. It sits above them and turns their
evidence into reproducible engineering decisions.

```
Benchmark frameworks
BenchmarkDotNet, (later: Crank, custom JSON/CSV)
                    │
                    ▼
              Result readers
                    │
                    ▼
       Normalized BenchmarkGate snapshot
                    │
          ┌─────────┴─────────┐
          ▼                   ▼
   Static baseline      Historical baseline
          │                   │
          └─────────┬─────────┘
                    ▼
                Compare
                    │
          ┌─────────┴─────────┐
          ▼                   ▼
  `compare` reports       Check (policy)
                                │
                                ▼
                        `check` reports
```

The permanent boundaries, one job each:

- **Capture** — converts native benchmark output into evidence
- **Validate** — determines whether evidence/configuration is trustworthy
- **Compare** — describes what changed
- **Check** — decides whether the change is acceptable
- **History** — selects and aggregates prior evidence
- **Verify** — establishes evidence integrity
- **Report** — presents results to humans and machines

A later, deliberately separate boundary (post-1.0, see `ROADMAP.md`):

- **Explain** — connects a regression with diagnostic evidence

Explain is not part of Check. Check decides; Explain diagnoses. This split
exists so the gate's pass/fail contract never depends on best-effort
correlation logic — a build fails or doesn't based on Check alone, and
Explain adds understanding without ever changing that decision.

Compare and Check are split for the same reason Explain is split from
Check: `compare` describes what changed without requiring a policy;
`check` applies a policy on top of the *same* comparison. Both commands
call the identical Compare step — there is one implementation of "what
changed," consumed two ways, never two implementations that happen to
agree. See ADR-0005 (comparison facts vs. evaluation verdicts)

## Solution layout

```
BenchmarkGate.sln
├── src/
│   ├── BenchmarkGate.Core/
│   ├── BenchmarkGate.BenchmarkDotNet/
│   ├── BenchmarkGate.Storage.FileSystem/
│   ├── BenchmarkGate.Reporting/
│   └── BenchmarkGate.Tool/
├── tests/
│   ├── BenchmarkGate.Core.Tests/
│   ├── BenchmarkGate.BenchmarkDotNet.Tests/
│   ├── BenchmarkGate.Tool.Tests/
│   ├── BenchmarkGate.IntegrationTests/
│   └── BenchmarkGate.PerformanceTests/
├── docs/
│   ├── architecture/
│   ├── adr/
│   ├── schemas/
│   └── governance/
└── samples/
```

### `BenchmarkGate.Core`

Must remain provider-neutral and CLI-neutral. No dependency on
BenchmarkDotNet, `System.CommandLine`, or any I/O. Pure domain model and
pure evaluation functions — never prints, never exits the process.

```
Core/
├── Model/
├── Identity/
├── Metrics/
├── Validation/
├── Comparison/
├── Evaluation/
├── History/
└── Integrity/
```

(`Diagnostics/` and `Explanation/` are added post-1.0, not before.)

### `BenchmarkGate.BenchmarkDotNet`

BenchmarkDotNet result parsing, identity mapping, unit normalization,
environment extraction, diagnoser normalization. No gate decisions live
here — this is a reader, not an evaluator.

### `BenchmarkGate.Storage.FileSystem`

History directory layout, manifest reading, snapshot retrieval, atomic
append, locking, checkpoint storage.

### `BenchmarkGate.Reporting`

Console, JSON, Markdown, JUnit, GitHub annotations. Consumes results, never
recomputes them.

### `BenchmarkGate.Tool`

`System.CommandLine` argument acquisition, dependency composition,
exit-code translation, stdout/stderr handling. Commands stay thin —
orchestration only (parse → load → evaluate → report → exit code), no
evaluation rules.

## Dependency direction (ADR-0001)

```
Tool → adapters (BenchmarkDotNet / Storage / Reporting) → Core
```

Core references none of the others. This is the single rule that's been
load-bearing since v0.1 — every layer above Core can depend on Core, Core
depends on nothing above it.

## Versioned artifacts (ADR-0004)

Every persisted artifact carries `"schemaVersion"`: snapshot, policy,
comparison, decision, history manifest, checkpoint. A schema-version
mismatch is a load-time error with a clear message, not a silent
best-effort read. Breaking schema changes are acceptable pre-1.0 with no
migration path (see `BASELINE-GOVERNANCE.md` once that exists);
post-1.0, schema stability is part of the v1.0.0 guarantee.

## Canonical identity and units

- Benchmark identity is centralized in `BenchmarkIdentity`
  (`type.method|job=X|params`), independent of parameter declaration
  order.
- Metric units are normalized at the boundary (duration → nanoseconds,
  memory → bytes, throughput → ops/sec) so Core never has to reason about
  mixed units.
- Metric direction (`lower-is-better` / `higher-is-better`) is explicit
  metadata on every metric, never assumed.

## ADRs

Architecture decisions are recorded in `docs/adr/` as they're actually
made — not written in a batch ahead of time. See `docs/adr/README.md` for
the index and status conventions (Proposed / Accepted / Superseded /
Deprecated / Rejected).

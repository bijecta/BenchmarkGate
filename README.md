<div align="center">
  <img src="./.github/assets/benchmarkgate-icon.svg" width="40" height="40" alt="BenchmarkGate" />

  # BenchmarkGate

  A local-first performance contract gate for BenchmarkDotNet and CI.

  ![build](https://img.shields.io/github/actions/workflow/status/Bijecta/BenchmarkGate/ci.yml?branch=main&style=flat-square&label=build)
  ![nuget](https://img.shields.io/nuget/v/Bijecta.BenchmarkGate.Tool?style=flat-square&label=nuget)
  ![license](https://img.shields.io/badge/license-Apache--2.0-4B5163?style=flat-square)
</div>

---

**Status:** `v0.2.0-alpha.1`. See [ROADMAP.md](ROADMAP.md) for what's
shipped and what's next.

## What this is

BenchmarkGate turns BenchmarkDotNet output into an enforceable
performance contract:

- No SaaS account, hosted database, or network connection required to
  evaluate benchmarks.
- Baselines and policies are plain, reviewable JSON files committed to your
  repository — a performance-budget change shows up in a pull-request diff,
  same as any other code change.
- Per-metric thresholds (mean time, allocation, ...) with separate warning
  and failure tiers, plus a stability gate that flags noisy measurements
  before they're evaluated as a regression.
- Runs identically locally and in CI.

## Install

```bash
dotnet tool install --global Bijecta.BenchmarkGate.Tool --version 0.2.0-alpha.1
```

## Quick start

```bash
benchmark-gate capture --results ./BenchmarkDotNet.Artifacts/results --output ./benchmarks/baseline.json --suite my-suite
benchmark-gate check --results ./BenchmarkDotNet.Artifacts/results --baseline ./benchmarks/baseline.json --policy ./benchmarks/policy.json
```

`check` reads BenchmarkDotNet's full-JSON output, compares it against the
committed baseline under the rules in `policy.json`, and exits non-zero the
moment a benchmark regresses past the failure threshold — a gate your
performance numbers have to pass before merge.

`policy.json` defines a stability gate and per-metric thresholds:

```json
{
  "schemaVersion": 1,
  "stability": { "minimumMeasurements": 10, "maximumCoefficientOfVariation": 0.05 },
  "metrics": {
    "meanNanoseconds": { "direction": "lower-is-better", "warningPercent": 7.5, "failurePercent": 15, "minimumAbsoluteChange": 100 },
    "allocatedBytesPerOperation": { "direction": "lower-is-better", "warningPercent": 1, "failurePercent": 5, "minimumAbsoluteChange": 1024 }
  }
}
```

A benchmark whose measurements don't meet the stability bar is reported as
`Unstable` rather than evaluated as a pass or regression. A metric crossing
`warningPercent` but not `failurePercent` is reported as `Warning` — visible
in every report, and only affects the process exit code if you pass
`--fail-on-warning`.

Useful flags on `check`:

| Flag | Purpose |
|---|---|
| `--markdown <path>` | Write a GitHub-friendly Markdown summary |
| `--json <path>` | Write a machine-readable decision document |
| `--junit <path>` | Write a JUnit XML report for CI test-result UIs |
| `--fail-on-warning` | Make a Warning-only suite exit non-zero |
| `--quiet` | Suppress console output (reports/exit code still work) |

## What this is not

- Not a benchmark execution service — BenchmarkDotNet remains the
  measurement engine; this tool only evaluates its output.
- Not a hosted dashboard or continuous-benchmarking platform. If you want
  historical trend storage and a web UI out of the box, look at established
  hosted platforms instead.

## Why

Correctness includes performance correctness. A silent regression that
nobody reads in a console log is still a regression. BenchmarkGate makes
performance a CI-enforced contract — a build that goes red, not a report
that might get read.

## Status / roadmap

See [ROADMAP.md](ROADMAP.md).

## License

Apache-2.0. See [LICENSE](LICENSE).
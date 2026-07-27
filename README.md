<div align="center">
  <img src="./src/BenchmarkGate.Tool/icon.png" width="64" height="64" alt="BenchmarkGate" />

  # BenchmarkGate

  A local-first performance contract gate for BenchmarkDotNet and CI.

  ![build](https://img.shields.io/github/actions/workflow/status/Bijecta/BenchmarkGate/ci.yml?branch=main&style=flat-square&label=build)
  ![nuget](https://img.shields.io/nuget/v/Bijecta.BenchmarkGate.Tool?style=flat-square&label=nuget)
  ![license](https://img.shields.io/badge/license-Apache--2.0-4B5163?style=flat-square)
</div>

---

**Status:** pre-alpha. First working release targeted as `v0.1.0-alpha.1`
— not yet published to NuGet.

## What this is

BenchmarkGate turns BenchmarkDotNet output into an enforceable
performance contract:

- No SaaS account, hosted database, or network connection required to
  evaluate benchmarks.
- Baselines and policies are plain, reviewable JSON files committed to your
  repository — a performance-budget change shows up in a pull-request diff,
  same as any other code change.
- Runs identically locally and in CI.

## Install

Not yet published. Once `v0.1.0-alpha.1` ships:

```bash
dotnet tool install --global Bijecta.BenchmarkGate.Tool --version 0.1.0-alpha.1
```

## Quick start

```bash
benchmark-gate capture --results ./BenchmarkDotNet.Artifacts/results --output ./benchmarks/baseline.json
benchmark-gate check --results ./BenchmarkDotNet.Artifacts/results --baseline ./benchmarks/baseline.json --threshold-percent 10
```

`check` reads BenchmarkDotNet's full-JSON output, compares it against the
committed baseline, and exits non-zero the moment a benchmark regresses past
the threshold — a gate your performance numbers have to pass before merge.

## What this is not

- Not a benchmark execution service — BenchmarkDotNet remains the
  measurement engine; this tool only evaluates its output.
- Not a hosted dashboard or continuous-benchmarking platform. If you want
  historical trend storage and a web UI out of the box, look at established
  hosted platforms instead (comparison coming once the alternatives section
  is written — see the roadmap).

## Why

Correctness includes performance correctness. A silent regression that
nobody reads in a console log is still a regression. BenchmarkGate makes
performance a CI-enforced contract — a build that goes red, not a report
that might get read.

## Status / roadmap

Currently building the `v0.1.0-alpha.1` vertical slice:

- [x] Repository skeleton, central package management, ADR-0001
- [x] BenchmarkDotNet JSON parsing (full-JSON exporter, single job per v0.1 — see parser XML docs)
- [x] Stable benchmark identity
- [x] Mean-time comparison against a committed baseline
- [x] `check` / `capture` commands
- [x] Console, Markdown, and JSON decision reports
- [x] Documented exit codes (subset used by v0.1; full table in `Core.Evaluation.ExitCodes`)
- [x] Packaged as a .NET tool, dogfooded against real CedarRecon benchmark
      results (deliberate regression correctly detected and cleared)
- [ ] Published to NuGet.org

**Known v0.1 simplifications** (documented inline in code, revisit for v0.2):
- Every parsed benchmark is assigned a fixed `"Default"` job — no multi-job
  disambiguation yet.
- `check` takes `--threshold-percent` / `--minimum-absolute-change-ns`
  directly rather than a full `policy.json` (the master-spec policy schema).
- Baseline schema is reduced (no provenance/environment blocks yet).

See `docs/adr/` for architecture decisions as they're made.

## License

Apache-2.0. See [LICENSE](LICENSE).
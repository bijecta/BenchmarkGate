# BenchmarkGate

> A local-first performance contract gate designed for BenchmarkDotNet and
> repository-controlled CI workflows.

**Status:** pre-alpha skeleton. Not yet functional. First working release
is targeted as `v0.1.0-alpha.1`.

## What this is

BenchmarkGate turns BenchmarkDotNet output into an enforceable
performance contract:

- No SaaS account, hosted database, or network connection required to
  evaluate benchmarks.
- Baselines and policies are plain, reviewable JSON files committed to your
  repository — a performance-budget change shows up in a pull-request diff,
  same as any other code change.
- Runs identically locally and in CI.

## What this is not

- Not a benchmark execution service — BenchmarkDotNet remains the
  measurement engine; this tool only evaluates its output.
- Not a hosted dashboard or continuous-benchmarking platform. If you want
  historical trend storage and a web UI out of the box, look at established
  hosted platforms instead (comparison coming once the alternatives section
  is written — see the roadmap).

## Status / roadmap

Currently building the `v0.1.0-alpha.1` vertical slice:

- [x] Repository skeleton, central package management, ADR-0001
- [x] BenchmarkDotNet JSON parsing (full-JSON exporter, single job per v0.1 — see parser XML docs)
- [x] Stable benchmark identity
- [x] Mean-time comparison against a committed baseline
- [x] `check` / `capture` commands
- [x] Console, Markdown, and JSON decision reports
- [x] Documented exit codes (subset used by v0.1; full table in `Core.Evaluation.ExitCodes`)
- [ ] Packaged as a .NET tool, dogfooded against
      [CedarRecon Issue #5](https://github.com/AamiriYouness/CedarRecon/issues/5) — Day 4

**Known v0.1 simplifications** (documented inline in code, revisit for v0.2):
- Every parsed benchmark is assigned a fixed `"Default"` job — no multi-job
  disambiguation yet.
- `check` takes `--threshold-percent` / `--minimum-absolute-change-ns`
  directly rather than a full `policy.json` (the master-spec policy schema).
- Baseline schema is reduced (no provenance/environment blocks yet).

See `docs/adr/` for architecture decisions as they're made.

## Quick start

Not yet available — coming with the `v0.1.0-alpha.1` release.

## License

Apache-2.0. See [LICENSE](LICENSE).

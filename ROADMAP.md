# Roadmap

## v0.1.0-alpha.1 — shipped

- [x] Repository skeleton, central package management, ADR-0001
- [x] BenchmarkDotNet JSON parsing (full-JSON exporter)
- [x] Stable benchmark identity
- [x] Mean-time comparison against a committed baseline
- [x] `check` / `capture` commands
- [x] Console, Markdown, and JSON decision reports
- [x] Documented exit codes
- [x] Packaged as a .NET tool, dogfooded against real CedarRecon benchmark
      results (deliberate regression correctly detected and cleared)
- [x] Published to NuGet.org as `v0.1.0-alpha.1`

## v0.2.0-alpha.1 — shipped

- [x] `policy.json` — per-metric direction/warning/failure thresholds plus
      a stability gate, replacing the v0.1 `--threshold-percent` /
      `--minimum-absolute-change-ns` flags
- [x] `Warning` status (between `Passed` and `Regressed`), with
      `--fail-on-warning` controlling whether it affects the exit code
- [x] `Unstable` status — a stability gate (measurement count, coefficient
      of variation) runs before any metric is evaluated
- [x] Allocation/memory regression tracking (`allocatedBytesPerOperation`),
      alongside mean time — metrics are stored in an extensible
      `IReadOnlyDictionary<string, double>`, not hardcoded fields
- [x] Multi-job identity — job is extracted from BenchmarkDotNet's
      `DisplayInfo` field (there is no structured `Job` field in its JSON
      export), replacing the v0.1 hardcoded `"Default"` placeholder
- [x] Baseline schema bumped to v2 (`metrics` object per entry, replacing
      the single `meanNanoseconds` field) — a deliberate breaking change,
      pre-1.0, no migration path
- [x] JUnit XML report (`--junit`), one `<testcase>` per (benchmark,
      metric) pair, for native CI test-result UI integration
- [x] Per-metric unit formatting (`IMetricFormatter`/`MetricFormatters`) —
      nanoseconds vs. bytes vs. unitless, instead of one formatter assuming
      everything is a duration
- [x] `ReportWriteException` / `BaselineWriteException` — report and
      baseline write failures are a controlled CLI outcome
      (`ExitCodes.OutputWriteFailure`) instead of an unhandled stack trace
- [x] Fixed a time-of-check/time-of-use race in `capture --overwrite`
      handling — overwrite is now enforced atomically inside the file
      writer, not by a preceding `File.Exists` check
- [x] Published to NuGet.org as `v0.2.0-alpha.1`

## Deferred / not yet started

- [ ] Environment validation (OS/runtime/CPU compatibility between baseline
      and current run)
- [ ] `validate` / `compare` commands
- [ ] Completeness policy — how missing/new benchmarks are handled beyond
      the current hardcoded behavior
- [ ] Provenance/environment blocks in the baseline schema
- [ ] Baseline schema migration tooling (currently: re-run `capture`)
- [ ] net8.0 multi-targeting, so the tool can be installed on machines with
      only the .NET 8 LTS runtime (currently net10.0-only)

See `docs/adr/` for architecture decisions as they're made.
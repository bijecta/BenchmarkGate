# Changelog

All notable changes to BenchmarkGate are documented here.

## [Unreleased]

### Added
- `Core.Comparison` namespace: `MetricDescriptor`, `OptimizationDirection`,
  `ChangeDirection` vocabulary types, `PercentDeltaCalculator` /
  `PercentDeltaResult` / `PercentDeltaStatus` for policy-free percentage-delta
  calculation, and `MetricCatalog` — a closed, built-in lookup covering
  `meanNanoseconds` and `allocatedBytesPerOperation` — for known-metric
  optimization direction. No epsilon substitution for a zero reference; no
  guessed values for NaN/Infinity input. Foundation for the v0.4.0 `compare`
  pipeline (#21).

## [0.3.0-alpha.1] - 03/08/2026

### Added
- Validation infrastructure (`ValidationResult`, `ValidationDiagnostic`,
  `DiagnosticDescriptor`, `DiagnosticSeverity`) in `Core.Validation` (#3)
- `PolicyValidator` and `Core.Policy` document model (#4)
- `SnapshotValidator` and `Core.Baseline` document model (#5)
- `ObservationValidator`/`ObservationSetValidator` for BenchmarkDotNet
  input, adapter-owned per ADR-0003 (#6)
- `benchmark-gate validate` command — validates policy/baseline/results
  without evaluating them, via `--policy`/`--baseline`/`--results`,
  colorized grouped console output, optional `--json` report,
  `ExitCodes.ValidationFailed = 12` (#7)
- `docs/EXIT-CODES.md` — authoritative exit code reference (#8)

### Changed
- `PolicyFileException`/`BaselineFileException`/`BenchmarkResultParseException`
  now expose a structured `ValidationResult` alongside their message
- `BenchmarkBaseline`'s duplicate-identity guard now throws
  `ArgumentException` instead of a raw `InvalidOperationException`
- `ExitCodes` moved to its own file, `Commands/ExitCodes.cs`

### Known limitation
- Malformed BenchmarkDotNet parameter fragments (e.g. `"N1000000"`
  without `=`) are still silently discarded by
  `BdnParameterStringParser` — tracked separately (#16)

## [0.2.0-alpha.1]

### Added
- `policy.json` file format (`PolicyFile.Load`) — per-metric `direction`/`warningPercent`/`failurePercent`/`minimumAbsoluteChange`, plus a `stability` block (`minimumMeasurements`, `maximumCoefficientOfVariation`). Schema-versioned, strict unknown-property rejection (`JsonUnmappedMemberHandling.Disallow`, .NET 8+), full numeric-range validation (rejects non-finite/negative values, empty metric names, `warningPercent >= failurePercent`)
- `Warning` and `Unstable` statuses (`BenchmarkGateStatus`), between `Passed`/`Regressed` and as a stability-gate outcome respectively
- `GatePolicy`/`MetricPolicy`/`StabilityPolicy` — replaces the single-threshold `RegressionPolicy`
- `MetricDecision` — per-metric outcome (name, status, baseline/current/delta, explanation); `BenchmarkDecision.Status` is now a worst-wins aggregate across a benchmark's metrics
- `IMetricFormatter`/`MetricFormatters` — per-metric-name unit formatting (nanoseconds, bytes, unitless fallback), replacing a nanosecond-only formatter that mislabeled allocation values
- Allocation/memory metric support end-to-end: `BdnMemoryDto`/`Statistics.N`/`StandardDeviation` (confirmed against real BenchmarkDotNet output), `BenchmarkObservation.Metrics` dictionary, `BaselineEntry.Metrics`
- Multi-job identity — extracted from BenchmarkDotNet's `DisplayInfo` field via regex (no structured `Job` field exists in BDN's JSON export; confirmed against real fixtures, both bare-token and parenthesized-parameter shapes), falling back to `"Default"`
- `JunitReporter` — JUnit XML report (`--junit`), one `<testcase>` per (benchmark, metric) pair; `Warning` only renders as `<failure>` when `--fail-on-warning` is set, keeping the report consistent with the process exit code
- `ReportWriteException` — `MarkdownReporter`/`JsonDecisionReporter`/`JunitReporter` wrap I/O failures instead of letting them escape as raw stack traces; new `ExitCodes.OutputWriteFailure`
- `BaselineWriteException` — `BaselineFile.WriteCandidate` write failures (including overwrite-false conflicts) are wrapped instead of escaping raw
- `AtomicFileWriter.Write`/`WriteJson` gained an `overwrite` parameter (default `true`), enforced atomically via `File.Move(..., overwrite)` — closes a time-of-check/time-of-use race in `capture --overwrite` handling
- `CaptureCommand` validation: empty/whitespace suite names and zero-observation results are now rejected instead of silently producing a meaningless or empty baseline
- `ROADMAP.md`, split out of the README
- Test coverage: `PolicyFileTests`, `BaselineFileTests`, `MetricFormatterTests`, `JunitReporterTests`, `ConsoleReporterTests`, `MarkdownReporterTests`, `MarkdownBuilderTests`, `JsonDecisionReporterTests`, `CheckCommandTests`, `CaptureCommandTests`, plus `AtomicFileWriterTests` coverage for the new `overwrite: false` path

### Changed
- CLI `check`: `--threshold-percent`/`--minimum-absolute-change-ns` replaced with `--policy <path>`; added `--junit <path>` and `--fail-on-warning`
- `SuiteDecision.ExitCode` (property) → `GetExitCode(failOnWarning)` (method)
- `RegressionEvaluator` rewritten: a stability check (measurement count, coefficient of variation) gates a benchmark to `Unstable` before any metric is compared; otherwise loops `GatePolicy.Metrics`, skipping metrics absent from either side, then aggregates worst-wins
- `BenchmarkObservation`/`BaselineEntry` — single `MeanNanoseconds`/`meanNanoseconds` field replaced with a `Metrics: IReadOnlyDictionary<string, double>` dictionary
- Baseline file `schemaVersion` bumped 1 → 2 (breaking, no migration): `benchmarks[].meanNanoseconds` → `benchmarks[].metrics`. Old baseline files are rejected outright with a message pointing at `capture`
- `JsonDecisionReporter` schema bumped 1 → 2: flat baseline/current/delta fields replaced with a per-benchmark `metrics` array; `Write` now takes `failOnWarning`
- `ConsoleReporter`/`MarkdownReporter` — one row per (benchmark, metric) pair instead of a single mean-time row; suite summaries gained Warning/Unstable counts
- `MarkdownBuilder.FormatNanoseconds` removed — reporters call `Core.Evaluation.MetricFormatters` directly instead of duplicating unit-formatting logic
- README rewritten for the v0.2 CLI surface (`--policy` example, flags table); roadmap moved to `ROADMAP.md`

### Fixed
- `RegressionPolicy`/`BenchmarkGateStatus` — the v0.1→v0.2 Core rewrite (which an earlier handoff described as already done) had not actually landed on the branch; this release is the real implementation
- `capture --overwrite` time-of-check/time-of-use race (see `AtomicFileWriter` above)

### Removed
- `RegressionPolicy.cs` — superseded by `GatePolicy`
- `--threshold-percent`/`--minimum-absolute-change-ns` CLI flags — no deprecation shim
# Changelog

All notable changes to BenchmarkGate are documented here.

## [Unreleased] — v0.2.0

### Added
- `Warning` and `Unstable` statuses to `BenchmarkGateStatus` (Core)
- `GatePolicy`/`MetricPolicy`/`StabilityPolicy` records — replaces the single-threshold `RegressionPolicy`, supports per-metric direction/warning/failure thresholds plus a stability gate
- `MetricDecision` record — per-metric outcome (name, status, baseline/current/delta values, explanation)
- `ExitCodes.Warning = 9` — new exit code for suites with only Warning-status benchmarks when `--fail-on-warning` is set
- `IMetricFormatter` / `MetricFormatters` — per-metric-name unit formatting (nanoseconds for `meanNanoseconds`, bytes for `allocatedBytesPerOperation`, unitless fallback for unregistered metrics). Replaces the nanosecond-only `FormatNanoseconds`, which mislabeled allocation values with time-unit suffixes.
- `MetricFormatterTests` — boundary-value coverage for unit-switch thresholds (999/1000 ns, 1023/1024 bytes) and registry fallback behavior
- `BdnStatisticsDto.N` / `StandardDeviation` — measurement count and stddev for the stability gate
- `BdnMemoryDto` (`BytesAllocatedPerOperation`) and `BdnBenchmarkDto.Memory` — allocation metric support (unverified against real BenchmarkDotNet output, needs confirmation before parser wiring)
- `BdnJobDto` (`ResolvedId`) and `BdnBenchmarkDto.Job` — multi-job identity support, replacing the hardcoded "Default" placeholder (unverified field shape, parser must retain the "Default" fallback)
- `BdnStatisticsDto.N` / `StandardDeviation` — measurement count and stddev for the stability gate (confirmed against real BenchmarkDotNet output)
- `BdnMemoryDto` (`BytesAllocatedPerOperation`) and `BdnBenchmarkDto.Memory` — allocation metric support (confirmed against real output)
- `BdnBenchmarkDto.DisplayInfo` — raw display string containing the job identifier (e.g. `Job-SNYTAA(...)`), for multi-job identity extraction; there is no structured `Job` field in BenchmarkDotNet's JSON export
- `job-with-parentheses.json` test fixture and coverage — confirms the `DisplayInfo` job-token regex handles both observed shapes (bare token like `DefaultJob`, and parenthesized like `Job-SNYTAA(IterationCount=10, ...)`)
- Baseline file schema bumped `schemaVersion` 1 → 2 (breaking, no migration): `benchmarks[].meanNanoseconds` replaced with `benchmarks[].metrics` (object keyed by metric name). v0.1 baseline files are rejected outright with a message directing the user to re-run `capture`.
- `JunitReporter` — JUnit XML report writer, one `<testcase>` per (benchmark, metric) pair. Regressed/Missing/Unstable always render as `<failure>`; Warning only renders as `<failure>` when `--fail-on-warning` is set, so the JUnit pass/fail signal matches the process exit code instead of contradicting it. Covered by `JunitReporterTests`.
- `PolicyFile.Load(path)` — deserializes `policy.json` into a `GatePolicy`, replacing the `--threshold-percent`/`--minimum-absolute-change-ns` CLI flags. Schema-versioned (`schemaVersion: 1`), stream-based JSON read, typed `PolicyFileException` on malformed input. Validates numeric ranges (`minimumMeasurements > 0`; finite, non-negative `maximumCoefficientOfVariation`/`warningPercent`/`failurePercent`/`minimumAbsoluteChange`; non-empty metric names) and rejects a metric whose `warningPercent >= failurePercent`, since that would make `Warning` unreachable for that metric. Uses `JsonUnmappedMemberHandling.Disallow` (.NET 8+) so a typo'd property name fails loudly instead of being silently ignored. `direction` is strictly case-sensitive by design.
- `PolicyFileTests` — coverage for missing/malformed file, schema version, stability/metric validation, direction handling, and strict unknown-property rejection.


### Changed
- `BenchmarkObservation` — single `MeanNanoseconds` field replaced with a `Metrics` dictionary; added `MeasurementCount` and `StandardDeviationNanoseconds` for stability evaluation
- `BaselineEntry.MeanNanoseconds` (double) replaced with `BaselineEntry.Metrics` (`IReadOnlyDictionary<string, double>`), same keys as `BenchmarkObservation.Metrics`. `BenchmarkBaseline` itself (Suite, dedup-by-identity, TryFind) is unchanged.
- `BenchmarkDecision` — flat single-metric fields replaced with `Metrics: IReadOnlyList<MetricDecision>`; `Status` is now a worst-wins aggregate across metrics
- `SuiteDecision.ExitCode` (property) replaced with `GetExitCode(bool failOnWarning)` (method), since exit code now depends on the `--fail-on-warning` flag
- `SuiteDecision` gained `WarningCount`/`UnstableCount`
- `RegressionEvaluatorTests` rewritten for `GatePolicy`/multi-metric shapes; added coverage for the stability gate and `--fail-on-warning` exit-code behavior
- `BenchmarkDotNetResultParser` — builds a `Metrics` dictionary (mean always, allocation when a `Memory` block is present) instead of a single `MeanNanoseconds` value; populates `MeasurementCount`/`StandardDeviationNanoseconds` from `Statistics.N`/`StandardDeviation`; extracts job identity from the free-text `DisplayInfo` field via regex (no structured `Job` field exists in BenchmarkDotNet's export), falling back to `"Default"` when absent or unrecognized
- `MarkdownBuilder.FormatNanoseconds` removed — reporters now call `Core.Evaluation.MetricFormatters.For(metricName).Format(value)` directly, since Tool already depends on Core (ADR-0001) and duplicating per-metric-unit formatting a second time wasn't a real tradeoff
- `MarkdownReporter`/`ConsoleReporter` — one row per (benchmark, metric) pair instead of a single mean-time row; suite summary gains Warning/Unstable counts
- `JsonDecisionReporter` — schema bumped 1 → 2: flat baseline/current/delta fields replaced with a nested `metrics` array per benchmark; `Write` now takes a `failOnWarning` flag since `SuiteDecision.ExitCode` is a method


### Removed
- `RegressionPolicy.cs` — superseded by `GatePolicy`
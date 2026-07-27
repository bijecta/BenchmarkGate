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


### Changed
- `BenchmarkObservation` — single `MeanNanoseconds` field replaced with a `Metrics` dictionary; added `MeasurementCount` and `StandardDeviationNanoseconds` for stability evaluation
- `BaselineEntry.MeanNanoseconds` (double) replaced with `BaselineEntry.Metrics` (`IReadOnlyDictionary<string, double>`), same keys as `BenchmarkObservation.Metrics`. `BenchmarkBaseline` itself (Suite, dedup-by-identity, TryFind) is unchanged.
- `BenchmarkDecision` — flat single-metric fields replaced with `Metrics: IReadOnlyList<MetricDecision>`; `Status` is now a worst-wins aggregate across metrics
- `SuiteDecision.ExitCode` (property) replaced with `GetExitCode(bool failOnWarning)` (method), since exit code now depends on the `--fail-on-warning` flag
- `SuiteDecision` gained `WarningCount`/`UnstableCount`
- `RegressionEvaluatorTests` rewritten for `GatePolicy`/multi-metric shapes; added coverage for the stability gate and `--fail-on-warning` exit-code behavior


### Removed
- `RegressionPolicy.cs` — superseded by `GatePolicy`
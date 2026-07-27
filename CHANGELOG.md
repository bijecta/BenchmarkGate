# Changelog

All notable changes to BenchmarkGate are documented here.

## [Unreleased] — v0.2.0

### Added
- `Warning` and `Unstable` statuses to `BenchmarkGateStatus` (Core)
- `GatePolicy`/`MetricPolicy`/`StabilityPolicy` records — replaces the single-threshold `RegressionPolicy`, supports per-metric direction/warning/failure thresholds plus a stability gate
- `MetricDecision` record — per-metric outcome (name, status, baseline/current/delta values, explanation)
- `ExitCodes.Warning = 9` — new exit code for suites with only Warning-status benchmarks when `--fail-on-warning` is set

### Changed
- `BenchmarkObservation` — single `MeanNanoseconds` field replaced with a `Metrics` dictionary; added `MeasurementCount` and `StandardDeviationNanoseconds` for stability evaluation
- `BaselineEntry.MeanNanoseconds` (double) replaced with `BaselineEntry.Metrics` (`IReadOnlyDictionary<string, double>`), same keys as `BenchmarkObservation.Metrics`. `BenchmarkBaseline` itself (Suite, dedup-by-identity, TryFind) is unchanged.
- `BenchmarkDecision` — flat single-metric fields replaced with `Metrics: IReadOnlyList<MetricDecision>`; `Status` is now a worst-wins aggregate across metrics
- `SuiteDecision.ExitCode` (property) replaced with `GetExitCode(bool failOnWarning)` (method), since exit code now depends on the `--fail-on-warning` flag
- `SuiteDecision` gained `WarningCount`/`UnstableCount`

### Removed
- `RegressionPolicy.cs` — superseded by `GatePolicy`
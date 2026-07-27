# Changelog

All notable changes to BenchmarkGate are documented here.

## [Unreleased] — v0.2.0

### Added
- `Warning` and `Unstable` statuses to `BenchmarkGateStatus` (Core)
- `GatePolicy`/`MetricPolicy`/`StabilityPolicy` records — replaces the single-threshold `RegressionPolicy`, supports per-metric direction/warning/failure thresholds plus a stability gate

### Changed
- `BenchmarkObservation` — single `MeanNanoseconds` field replaced with a `Metrics` dictionary; added `MeasurementCount` and `StandardDeviationNanoseconds` for stability evaluation
- `BaselineEntry.MeanNanoseconds` (double) replaced with `BaselineEntry.Metrics` (`IReadOnlyDictionary<string, double>`), same keys as `BenchmarkObservation.Metrics`. `BenchmarkBaseline` itself (Suite, dedup-by-identity, TryFind) is unchanged.

### Removed
- `RegressionPolicy.cs` — superseded by `GatePolicy`
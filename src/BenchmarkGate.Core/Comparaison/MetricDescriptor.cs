namespace Bijecta.BenchmarkGate.Core.Comparison;

/// <summary>
/// Describes the semantics of a known metric: its name, which direction of
/// change is favorable, and its canonical unit.
/// </summary>
/// <param name="Name">
/// The metric's canonical name, matching the key used in
/// <c>BenchmarkObservation.Metrics</c> / <c>BaselineEntry.Metrics</c>.
/// </param>
/// <param name="Direction">Which direction of change is favorable for this metric.</param>
/// <param name="Unit">
/// The metric's canonical unit (e.g. "ns", "bytes"). Used for display and
/// for unit-compatibility checks in <c>BenchmarkComparisonEngine</c> —
/// not used by <see cref="PercentDeltaCalculator"/>, which is unit-agnostic.
/// </param>
public sealed record MetricDescriptor(string Name, OptimizationDirection Direction, string Unit);
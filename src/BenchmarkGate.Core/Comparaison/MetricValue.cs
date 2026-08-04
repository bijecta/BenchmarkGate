namespace Bijecta.BenchmarkGate.Core.Comparison;

/// <summary>
/// A single raw metric value as reported by one side (reference or
/// candidate) of a comparison, with its reported unit.
/// </summary>
/// <remarks>
/// Preserves the value exactly as reported, including non-finite values
/// (NaN, Infinity) — validity and comparability are communicated
/// separately via <see cref="MetricComparisonStatus"/>, never by omitting
/// or altering the value here. No rounding is applied at this layer.
/// </remarks>
public readonly record struct MetricValue(double Value, string Unit);
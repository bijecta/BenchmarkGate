namespace Bijecta.BenchmarkGate.Core.Comparison;

/// <summary>
/// A single raw metric value as reported by one side (reference or
/// candidate) of a comparison, with its reported unit when known.
/// </summary>
/// <remarks>
/// Preserves the value exactly as reported, including non-finite values
/// (NaN, Infinity) — validity and comparability are communicated
/// separately via <see cref="MetricComparisonStatus"/>, never by omitting
/// or altering the value here. No rounding is applied at this layer.
///
/// <see cref="Unit"/> is null, not an empty string, when no unit is known —
/// an empty string would blur "unknown unit" with "explicitly unitless
/// metric" and "malformed empty unit reported by the adapter". For a known
/// metric (present in <c>MetricCatalog</c>), <see cref="Unit"/> is the
/// catalog's canonical unit; for an unknown metric, it is null.
/// </remarks>
public readonly record struct MetricValue(double Value, string? Unit);
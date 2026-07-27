namespace Bijecta.BenchmarkGate.Core.Evaluation;

/// <summary>
/// Whether a lower or higher metric value is the improving direction.
/// Time-based metrics (mean, allocation) are typically lower-is-better;
/// throughput-style metrics would be higher-is-better.
/// </summary>
public enum MetricDirection
{
    LowerIsBetter,
    HigherIsBetter
}

/// <summary>
/// Threshold policy for a single named metric (e.g. "meanNanoseconds",
/// "allocatedBytesPerOperation"). WarningPercent must be strictly less
/// than FailurePercent for the policy to be meaningful — not enforced
/// here; PolicyFile validates on load.
/// </summary>
public sealed record MetricPolicy
{
    public required MetricDirection Direction { get; init; }
    public required double WarningPercent { get; init; }
    public required double FailurePercent { get; init; }

    /// <summary>
    /// Absolute change floor in the metric's native unit (e.g. nanoseconds,
    /// bytes). A percentage regression below this absolute delta is not
    /// flagged, even if it crosses WarningPercent/FailurePercent — guards
    /// against noise on already-tiny benchmarks where a 20% swing is a
    /// handful of nanoseconds.
    /// </summary>
    public required double MinimumAbsoluteChange { get; init; }
}

/// <summary>
/// Gates a benchmark to Unstable before any metric comparison runs, if its
/// measurements don't meet these bars. Applies once per benchmark, not
/// per metric.
/// </summary>
public sealed record StabilityPolicy
{
    public required int MinimumMeasurements { get; init; }
    public required double MaximumCoefficientOfVariation { get; init; }
}

/// <summary>
/// Root policy document — deserialized from policy.json. Keyed by metric
/// name (must match the keys used in BenchmarkObservation.Metrics /
/// BaselineEntry.Metrics, e.g. BenchmarkObservation.MeanNanosecondsMetric).
/// A metric absent from this dictionary is not evaluated at all.
/// </summary>
public sealed record GatePolicy
{
    public required StabilityPolicy Stability { get; init; }
    public required IReadOnlyDictionary<string, MetricPolicy> Metrics { get; init; }
}
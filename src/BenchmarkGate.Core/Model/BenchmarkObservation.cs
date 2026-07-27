using Bijecta.BenchmarkGate.Core.Identity;

namespace Bijecta.BenchmarkGate.Core.Model;

/// <summary>
/// A single benchmark's observed results from a run. Metrics is extensible —
/// new metric names can be added (by the adapter layer) without further
/// schema changes here or in GatePolicy/BenchmarkBaseline.
/// </summary>
public sealed record BenchmarkObservation
{
    /// <summary>Key into Metrics for BenchmarkDotNet's reported mean time.</summary>
    public const string MeanNanosecondsMetric = "meanNanoseconds";

    /// <summary>Key into Metrics for BenchmarkDotNet's memory diagnoser output.</summary>
    public const string AllocatedBytesMetric = "allocatedBytesPerOperation";

    public required BenchmarkIdentity Identity { get; init; }

    /// <summary>
    /// Metric name -> value, in each metric's native unit (nanoseconds,
    /// bytes, etc). A metric absent here (e.g. no MemoryDiagnoser enabled)
    /// is simply not evaluated, not treated as zero or missing-with-error.
    /// </summary>
    public required IReadOnlyDictionary<string, double> Metrics { get; init; }

    /// <summary>
    /// Number of measurements BenchmarkDotNet took for this benchmark.
    /// Compared against StabilityPolicy.MinimumMeasurements.
    /// </summary>
    public required int MeasurementCount { get; init; }

    /// <summary>
    /// Standard deviation of the mean-time measurements, in nanoseconds.
    /// Used with the mean to compute coefficient of variation against
    /// StabilityPolicy.MaximumCoefficientOfVariation. Only meaningful for
    /// the mean-time metric — allocation figures from BenchmarkDotNet
    /// don't carry a comparable per-iteration stddev in the JSON output.
    /// </summary>
    public required double StandardDeviationNanoseconds { get; init; }
}
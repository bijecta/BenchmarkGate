using Bijecta.BenchmarkGate.Core.Identity;

namespace Bijecta.BenchmarkGate.Core.Model;

/// <summary>
/// A single benchmark's observed results from a run. Metrics is extensible —
/// new metric names can be added (by the adapter layer) without further
/// schema changes here or in GatePolicy/BenchmarkBaseline.
/// </summary>
public sealed record BenchmarkObservation(
    BenchmarkIdentity Identity,
    IReadOnlyDictionary<string, double> Metrics,
    int MeasurementCount,
    double StandardDeviationNanoseconds)
{
    /// <summary>Key into Metrics for BenchmarkDotNet's reported mean time.</summary>
    public const string MeanNanosecondsMetric = "meanNanoseconds";

    /// <summary>Key into Metrics for BenchmarkDotNet's memory diagnoser output.</summary>
    public const string AllocatedBytesMetric = "allocatedBytesPerOperation";
}
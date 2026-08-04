namespace Bijecta.BenchmarkGate.Core.Comparison;

/// <summary>
/// Describes a benchmark's structural relationship between the reference
/// baseline and the candidate run.
/// </summary>
/// <remarks>
/// This is a benchmark-level classification only. Per-metric outcomes are
/// described separately by <see cref="MetricComparisonStatus"/>, and
/// <see cref="Added"/>/<see cref="Removed"/> never appear at the metric
/// level — a metric within an added or removed benchmark is described by
/// <see cref="MetricComparisonStatus.MissingReferenceMetric"/> or
/// <see cref="MetricComparisonStatus.MissingCandidateMetric"/> instead.
/// </remarks>
public enum BenchmarkComparisonStatus
{
    /// <summary>The benchmark exists in both the reference baseline and the candidate run.</summary>
    Comparable,

    /// <summary>The benchmark exists in the candidate run but not in the reference baseline.</summary>
    Added,

    /// <summary>The benchmark exists in the reference baseline but not in the candidate run.</summary>
    Removed
}
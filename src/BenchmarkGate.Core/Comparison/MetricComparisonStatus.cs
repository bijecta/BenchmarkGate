namespace Bijecta.BenchmarkGate.Core.Comparison;

/// <summary>
/// Describes the outcome of comparing a single metric between the
/// reference baseline and the candidate run.
/// </summary>
/// <remarks>
/// <see cref="MissingReferenceMetric"/> and <see cref="MissingCandidateMetric"/>
/// are deliberately distinct statuses, not one collapsed "invalid" status —
/// which side is missing the metric matters for diagnostics, reporting, and
/// tests. No pass/fail/stability vocabulary appears here; this status
/// describes structural and numeric comparability only.
/// </remarks>
public enum MetricComparisonStatus
{
    /// <summary>Both sides report this metric with finite, unit-compatible values.</summary>
    Comparable,

    /// <summary>The metric is absent from the reference baseline's entry for this benchmark.</summary>
    MissingReferenceMetric,

    /// <summary>The metric is absent from the candidate run's observation for this benchmark.</summary>
    MissingCandidateMetric,

    /// <summary>
    /// Reserved. Both sides report finite values for this metric, but with
    /// incompatible units. Not currently producible: neither
    /// <c>BenchmarkObservation.Metrics</c> nor <c>BaselineEntry.Metrics</c>
    /// carries a source unit per value today (both are
    /// <c>IReadOnlyDictionary&lt;string, double&gt;</c>), so there is no
    /// factual per-value unit to compare — <c>MetricCatalog</c>'s <c>Unit</c>
    /// is semantic metadata for a metric name, not evidence of what unit a
    /// specific reported value used, and comparing a metric's catalog unit
    /// against itself can never disagree. This status is reserved for when
    /// baseline and candidate values carry real source-unit metadata.
    /// </summary>
    UnitMismatch,

    /// <summary>The reference value is present but not finite (NaN or Infinity).</summary>
    InvalidReferenceValue,

    /// <summary>The candidate value is present but not finite (NaN or Infinity).</summary>
    InvalidCandidateValue
}
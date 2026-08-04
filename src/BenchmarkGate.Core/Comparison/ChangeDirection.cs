namespace Bijecta.BenchmarkGate.Core.Comparison;

/// <summary>
/// Describes whether a candidate metric value represents an improvement,
/// degradation, unchanged result, or a change whose meaning cannot be
/// determined.
/// </summary>
/// <remarks>
/// This type is policy-independent. This vocabulary alone does not produce
/// a <see cref="ChangeDirection"/> value — no producer exists yet.
/// Direction is derived by <c>BenchmarkComparisonEngine</c> from the
/// numeric change and the metric's <see cref="OptimizationDirection"/>,
/// including the rule that an unmatched/unknown metric (no
/// <see cref="MetricDescriptor"/> in <see cref="MetricCatalog"/>) always
/// produces <see cref="Indeterminate"/>, never a guessed direction.
/// </remarks>
public enum ChangeDirection
{
    /// <summary>The change moved the metric in its favorable direction.</summary>
    Improvement,

    /// <summary>The reference and candidate values are identical.</summary>
    Unchanged,

    /// <summary>The change moved the metric in its unfavorable direction.</summary>
    Degradation,

    /// <summary>
    /// The change's meaning cannot be determined — either the metric's
    /// <see cref="OptimizationDirection"/> is <see cref="OptimizationDirection.Neutral"/>
    /// and the value changed, or no <see cref="MetricDescriptor"/> exists
    /// for this metric name.
    /// </summary>
    Indeterminate
}
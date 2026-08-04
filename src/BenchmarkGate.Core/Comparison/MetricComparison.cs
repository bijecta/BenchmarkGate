namespace Bijecta.BenchmarkGate.Core.Comparison;

/// <summary>
/// The comparison of a single metric between the reference baseline and
/// the candidate run.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Reference"/> and <see cref="Candidate"/> preserve the raw
/// metric values whenever the corresponding side contains the metric,
/// including non-finite values. <see cref="Status"/> determines whether
/// those values are valid and comparable — it is never inferred from
/// whether <see cref="Reference"/>/<see cref="Candidate"/> are null.
/// </para>
/// <para>
/// When <see cref="Status"/> is not <see cref="MetricComparisonStatus.Comparable"/>,
/// <see cref="AbsoluteDelta"/>, <see cref="PercentDelta"/>, and
/// <see cref="Direction"/> are all <c>null</c>.
/// </para>
/// <para>
/// When <see cref="Status"/> is <see cref="MetricComparisonStatus.Comparable"/>,
/// <see cref="AbsoluteDelta"/> and <see cref="Direction"/> are always
/// populated. <see cref="PercentDelta"/> is <c>null</c> only in the single
/// mathematically undefined case: the reference value is zero and the
/// candidate value is non-zero. When both reference and candidate are zero,
/// <see cref="PercentDelta"/> is <c>0</c> and <see cref="Direction"/> is
/// <see cref="ChangeDirection.Unchanged"/>.
/// </para>
/// <para>
/// <see cref="Descriptor"/> being <c>null</c> does not imply
/// <see cref="Direction"/> is null — an unknown metric (no
/// <see cref="MetricDescriptor"/> in <c>MetricCatalog</c>) is still
/// numerically comparable: equal values produce
/// <see cref="ChangeDirection.Unchanged"/>, changed values produce
/// <see cref="ChangeDirection.Indeterminate"/>.
/// </para>
/// </remarks>
public sealed record MetricComparison(
    string MetricName,
    MetricComparisonStatus Status,
    MetricDescriptor? Descriptor,
    MetricValue? Reference,
    MetricValue? Candidate,
    double? AbsoluteDelta,
    double? PercentDelta,
    ChangeDirection? Direction);
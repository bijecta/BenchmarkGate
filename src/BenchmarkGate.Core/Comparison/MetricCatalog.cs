using Bijecta.BenchmarkGate.Core.Model;

namespace Bijecta.BenchmarkGate.Core.Comparison;

/// <summary>
/// Provides optimization semantics and canonical units for metric names
/// explicitly understood by BenchmarkGate.
/// </summary>
/// <remarks>
/// This catalog does not define which metrics may be compared —
/// <c>BenchmarkComparisonEngine</c> compares every metric present in the
/// reference or candidate data, regardless of catalog membership.
/// Membership here controls <em>semantic classification</em> only: a metric
/// absent from this catalog remains numerically comparable (value, delta,
/// percentage change) when its values and units are compatible. The exact
/// rule for its <see cref="ChangeDirection"/> — including how an unchanged
/// value is treated when no <see cref="OptimizationDirection"/> can be
/// inferred — is owned by <c>BenchmarkComparisonEngine</c>, not restated
/// here.
///
/// This is deliberately not a mutable/registerable catalog for v0.4.0 — a
/// registration mechanism raises open product questions (who registers
/// descriptors, whether policy files can declare custom metric semantics,
/// conflict handling) that don't need answering yet. Only add a built-in
/// entry here for a metric whose semantics BenchmarkGate genuinely
/// understands — not merely because BenchmarkDotNet can emit it. Metrics
/// like error, standard deviation, ratio, operation count, or GC collection
/// counts may need different treatment entirely (some are stability facts
/// rather than gate metrics; some lack a universally meaningful direction)
/// and should be evaluated case-by-case, not bulk-added.
/// </remarks>
public static class MetricCatalog
{
    private static readonly Dictionary<string, MetricDescriptor> Descriptors =
        new Dictionary<string, MetricDescriptor>(StringComparer.Ordinal)
        {
            [BenchmarkObservation.MeanNanosecondsMetric] = new(
                BenchmarkObservation.MeanNanosecondsMetric,
                OptimizationDirection.LowerIsBetter,
                Unit: "ns"),

            [BenchmarkObservation.AllocatedBytesMetric] = new(
                BenchmarkObservation.AllocatedBytesMetric,
                OptimizationDirection.LowerIsBetter,
                Unit: "bytes"),
        };

    /// <summary>
    /// Looks up the descriptor for a metric name explicitly understood by
    /// BenchmarkGate.
    /// </summary>
    /// <param name="metricName">The exact, case-sensitive metric name.</param>
    /// <param name="descriptor">
    /// The matching descriptor when the method returns <c>true</c>;
    /// otherwise, <c>null</c>.
    /// </param>
    /// <returns>
    /// <c>true</c> when the metric has built-in semantics; otherwise,
    /// <c>false</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="metricName"/> is <c>null</c>.
    /// </exception>
    public static bool TryGet(string metricName, out MetricDescriptor? descriptor)
    {
        ArgumentNullException.ThrowIfNull(metricName);

        return Descriptors.TryGetValue(metricName, out descriptor);
    }
}
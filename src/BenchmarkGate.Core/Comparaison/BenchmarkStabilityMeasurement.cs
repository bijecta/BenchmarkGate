namespace Bijecta.BenchmarkGate.Core.Comparison;

/// <summary>
/// The candidate run's raw stability facts for a benchmark, carried through
/// comparison without classification.
/// </summary>
/// <remarks>
/// This is a benchmark-level fact, not a per-metric one — it mirrors where
/// <c>BenchmarkObservation</c> actually carries measurement count and
/// standard deviation today: once per benchmark, not once per metric.
/// Attaching separate stability facts to every <see cref="MetricComparison"/>
/// would falsely imply each metric has its own sample count and that
/// allocation has an independent standard deviation — neither is true of
/// the underlying data.
///
/// Only the candidate side is represented. The reference baseline does not
/// persist stability facts, so there is no reference-side counterpart to
/// model, and no symmetry to suggest one exists.
///
/// This type carries raw facts only — classifying them (e.g. an
/// unstable-benchmark determination) is a policy decision that stays in
/// <c>RegressionEvaluator</c>, not here.
/// </remarks>
public sealed record BenchmarkStabilityMeasurement(
    int MeasurementCount,
    double StandardDeviationNanoseconds);
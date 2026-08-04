using Bijecta.BenchmarkGate.Core.Identity;

namespace Bijecta.BenchmarkGate.Core.Comparison;

/// <summary>
/// The comparison of a single benchmark between the reference baseline and
/// the candidate run.
/// </summary>
/// <remarks>
/// <see cref="CandidateStability"/> is non-null whenever the candidate run
/// has an observation for this benchmark (<see cref="BenchmarkComparisonStatus.Comparable"/>
/// or <see cref="BenchmarkComparisonStatus.Added"/>), and null when it does
/// not (<see cref="BenchmarkComparisonStatus.Removed"/>) — there is no
/// candidate observation to source stability facts from.
/// </remarks>
public sealed record BenchmarkComparison(
    BenchmarkIdentity Identity,
    BenchmarkComparisonStatus Status,
    BenchmarkStabilityMeasurement? CandidateStability,
    IReadOnlyList<MetricComparison> Metrics);
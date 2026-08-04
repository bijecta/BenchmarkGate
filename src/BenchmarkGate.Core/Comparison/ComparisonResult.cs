namespace Bijecta.BenchmarkGate.Core.Comparison;

/// <summary>
/// The policy-free comparison of a captured suite against a candidate
/// benchmark run.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Suite"/> is inherited from the reference baseline — the
/// candidate run does not independently define a suite name, so there is
/// no suite-mismatch state to model here.
/// </para>
/// <para>
/// Benchmark ordering is determined by whatever produces this type (e.g.
/// <c>BenchmarkComparisonEngine</c>); this type preserves the order it is
/// given rather than sorting or normalizing it.
/// </para>
/// <para>
/// The <c>*Count</c> properties are always computed from
/// <see cref="Benchmarks"/>, never stored independently, so they cannot
/// diverge from it — same pattern as <c>SuiteDecision</c>'s count
/// properties.
/// </para>
/// </remarks>
public sealed record ComparisonResult(
    string Suite,
    IReadOnlyList<BenchmarkComparison> Benchmarks)
{
    public int ComparableCount => Count(BenchmarkComparisonStatus.Comparable);
    public int AddedCount => Count(BenchmarkComparisonStatus.Added);
    public int RemovedCount => Count(BenchmarkComparisonStatus.Removed);

    private int Count(BenchmarkComparisonStatus status) =>
        Benchmarks.Count(b => b.Status == status);
}
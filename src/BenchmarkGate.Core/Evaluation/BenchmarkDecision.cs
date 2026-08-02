using Bijecta.BenchmarkGate.Core.Identity;

namespace Bijecta.BenchmarkGate.Core.Evaluation;

/// <summary>
/// The evaluated outcome for a single metric (e.g. mean time, allocation)
/// within one benchmark.
/// </summary>
public sealed record MetricDecision(
    string MetricName,
    BenchmarkGateStatus Status,
    double BaselineValue,
    double CurrentValue,
    double AbsoluteDelta,
    double RelativeDeltaPercent,
    string Explanation);

/// <summary>
/// The evaluated outcome for a single benchmark across all applicable
/// metrics. Status is the worst-wins aggregate across Metrics (and is set
/// to Unstable directly, bypassing per-metric evaluation entirely, if the
/// benchmark fails the stability gate — see RegressionEvaluator).
/// Precedence: Regressed > Warning > Unstable > Missing > New > Improved > Passed.
/// </summary>
public sealed record BenchmarkDecision(
    BenchmarkIdentity Identity,
    BenchmarkGateStatus Status,
    IReadOnlyList<MetricDecision> Metrics,
    string Explanation);

/// <summary>
/// The aggregated result for an entire suite evaluation. This is what
/// drives the process exit code and the reports.
/// </summary>
public sealed record SuiteDecision(
    IReadOnlyList<BenchmarkDecision> Benchmarks)
{
    public int ImprovedCount => Count(BenchmarkGateStatus.Improved);
    public int PassedCount => Count(BenchmarkGateStatus.Passed);
    public int WarningCount => Count(BenchmarkGateStatus.Warning);
    public int RegressedCount => Count(BenchmarkGateStatus.Regressed);
    public int MissingCount => Count(BenchmarkGateStatus.Missing);
    public int NewCount => Count(BenchmarkGateStatus.New);
    public int UnstableCount => Count(BenchmarkGateStatus.Unstable);

    private int Count(BenchmarkGateStatus status) =>
        Benchmarks.Count(b => b.Status == status);

    /// <summary>
    /// Maps the suite outcome to the documented process exit codes
    /// (see docs/EXIT-CODES.md). Precedence: Regressed > Missing > Unstable >
    /// Warning-if-failOnWarning > Passed. A regression always wins over a
    /// missing or unstable benchmark, so CI surfaces the most actionable
    /// failure first.
    /// </summary>
    public int GetExitCode(bool failOnWarning)
    {
        if (RegressedCount > 0) return ExitCodes.Regressed;
        if (MissingCount > 0) return ExitCodes.IncompleteResultSet;
        if (UnstableCount > 0) return ExitCodes.UnstableResults;
        if (failOnWarning && WarningCount > 0) return ExitCodes.Warning;
        return ExitCodes.Passed;
    }
}

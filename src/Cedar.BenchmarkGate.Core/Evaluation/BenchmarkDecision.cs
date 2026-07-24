using Cedar.BenchmarkGate.Core.Identity;

namespace Cedar.BenchmarkGate.Core.Evaluation;

/// <summary>
/// The evaluated outcome for a single benchmark.
/// </summary>
public sealed record BenchmarkDecision(
    BenchmarkIdentity Identity,
    BenchmarkGateStatus Status,
    double? BaselineMeanNanoseconds,
    double? CurrentMeanNanoseconds,
    double? AbsoluteDeltaNanoseconds,
    double? RelativeDeltaPercent,
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
    public int RegressedCount => Count(BenchmarkGateStatus.Regressed);
    public int MissingCount => Count(BenchmarkGateStatus.Missing);
    public int NewCount => Count(BenchmarkGateStatus.New);

    private int Count(BenchmarkGateStatus status) =>
        Benchmarks.Count(b => b.Status == status);

    /// <summary>
    /// Maps the suite outcome to the documented process exit codes
    /// (see docs/exit-codes.md). Precedence: a regression always wins over
    /// a missing benchmark, so CI surfaces the more actionable failure
    /// first.
    /// </summary>
    public int ExitCode
    {
        get
        {
            if (RegressedCount > 0) return ExitCodes.Regressed;
            if (MissingCount > 0) return ExitCodes.IncompleteResultSet;
            return ExitCodes.Passed;
        }
    }
}

/// <summary>
/// Stable, documented process exit codes. Do not change meanings after a
/// stable release without a major version bump (master spec section 12).
/// v0.1.0-alpha.1 only produces a subset of these; the rest are reserved.
/// </summary>
public static class ExitCodes
{
    public const int Passed = 0;
    public const int Regressed = 1;
    public const int InvalidArguments = 2;
    public const int InvalidBaselineOrPolicy = 3;
    public const int IncompleteResultSet = 4;
    public const int IncompatibleEnvironment = 5;
    public const int UnstableResults = 6;
    public const int UnapprovedNewBenchmarks = 7;
    public const int UnsupportedSchema = 8;
    public const int InternalError = 10;
}

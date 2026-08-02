namespace Bijecta.BenchmarkGate.Core.Evaluation;

/// <summary>
/// Stable, documented process exit codes. Do not change meanings after a
/// stable release without a major version bump (master spec section 12).
/// See docs/EXIT-CODES.md for the authoritative reference — which
/// command(s) return each code and under what condition.
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
    public const int Warning = 9;
    public const int InternalError = 10;
    public const int OutputWriteFailure = 11;
    public const int ValidationFailed = 12;
}
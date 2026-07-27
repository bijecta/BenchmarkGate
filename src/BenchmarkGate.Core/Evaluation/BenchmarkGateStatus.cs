namespace Bijecta.BenchmarkGate.Core.Evaluation;

/// <summary>
/// Outcome for a single benchmark (or, in v0.2, a single metric within a
/// benchmark — see BenchmarkDecision for how per-metric statuses aggregate).
/// </summary>
public enum BenchmarkGateStatus
{
    Improved,
    Passed,
    Warning,
    Regressed,
    Missing,
    New,
    Unstable
}

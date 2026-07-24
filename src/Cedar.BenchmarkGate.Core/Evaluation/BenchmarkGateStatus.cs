namespace Cedar.BenchmarkGate.Core.Evaluation;

/// <summary>
/// Outcome for a single benchmark. This is the v0.1.0-alpha.1 subset of the
/// full status list in the master spec (Improved/Passed/Warning/Regressed/
/// Missing/New/Unstable/Invalid/IncompatibleEnvironment). Warning/Unstable/
/// Invalid/IncompatibleEnvironment are deferred to v0.2 along with stability
/// and environment validation.
/// </summary>
public enum BenchmarkGateStatus
{
    Improved,
    Passed,
    Regressed,
    Missing,
    New
}

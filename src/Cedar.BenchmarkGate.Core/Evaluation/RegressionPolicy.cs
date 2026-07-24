namespace Cedar.BenchmarkGate.Core.Evaluation;

/// <summary>
/// v0.1.0-alpha.1 policy: a single lower-is-better metric (mean time) with a
/// percentage failure threshold and a minimum absolute change guard. This is
/// deliberately a small subset of the full policy schema in the master spec
/// (section 8) — missing/new-benchmark handling, environment policy, and
/// stability policy are deferred to v0.2.
/// </summary>
/// <param name="FailurePercent">
/// A benchmark fails when its relative regression is greater than or equal
/// to this percentage. 15 means "a regression of exactly 15% fails" (boundary
/// is inclusive — see docs/adr for the reasoning).
/// </param>
/// <param name="MinimumAbsoluteChangeNanoseconds">
/// A benchmark only fails if the absolute delta also meets or exceeds this
/// value. Guards against tiny, meaningless benchmarks (e.g. 1ns -> 1.2ns is
/// 20% but is noise) tripping the percentage threshold. Default 0 disables
/// this guard.
/// </param>
public sealed record RegressionPolicy(
    double FailurePercent,
    double MinimumAbsoluteChangeNanoseconds = 0);

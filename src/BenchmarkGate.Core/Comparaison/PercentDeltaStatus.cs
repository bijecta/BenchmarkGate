namespace Bijecta.BenchmarkGate.Core.Comparison;

/// <summary>
/// Describes the outcome of a <see cref="PercentDeltaCalculator"/> calculation.
/// Always check this before reading <see cref="PercentDeltaResult.Value"/> —
/// a null value is expected and correct for every status except
/// <see cref="Calculated"/>.
/// </summary>
public enum PercentDeltaStatus
{
    /// <summary>A percentage delta was computed. <c>Value</c> is non-null.</summary>
    Calculated,

    /// <summary>Both reference and candidate are zero — no change occurred, and no percentage applies.</summary>
    ReferenceZeroAndCandidateZero,

    /// <summary>
    /// The reference value is zero and the candidate is a valid non-zero
    /// number. A percentage change from zero is undefined; this is
    /// deliberately not approximated with an epsilon substitute.
    /// </summary>
    ReferenceZero,

    /// <summary>The reference value is NaN or Infinity.</summary>
    InvalidReference,

    /// <summary>The candidate value is NaN or Infinity. Only reported when the reference is finite.</summary>
    InvalidCandidate
}
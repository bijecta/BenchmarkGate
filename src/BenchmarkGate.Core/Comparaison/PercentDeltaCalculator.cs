namespace Bijecta.BenchmarkGate.Core.Comparison;

/// <summary>
/// Computes the percentage change from a reference value to a candidate
/// value. Pure: no I/O, no dependency on baseline/observation/policy types,
/// no knowledge of metric semantics — see ADR-0001's dependency direction.
/// </summary>
public static class PercentDeltaCalculator
{
    /// <summary>
    /// Computes <c>((candidate - reference) / reference) * 100</c>, with
    /// explicit outcomes for invalid input and for a zero reference. Never
    /// substitutes an epsilon for a zero reference and never guesses a
    /// value for NaN/Infinity input.
    /// </summary>
    /// <remarks>
    /// Validation order is deliberate and tested explicitly:
    /// <see cref="PercentDeltaStatus.InvalidReference"/> is checked before
    /// <see cref="PercentDeltaStatus.InvalidCandidate"/>, and both are
    /// checked before any zero-reference handling — invalid numeric input
    /// is a data-integrity problem, and zero-reference handling only
    /// applies once both operands are valid finite numbers.
    /// </remarks>
    public static PercentDeltaResult Calculate(double reference, double candidate)
    {
        if (!double.IsFinite(reference))
        {
            return PercentDeltaResult.InvalidReference;
        }

        if (!double.IsFinite(candidate))
        {
            return PercentDeltaResult.InvalidCandidate;
        }

        if (reference == 0d)
        {
            return candidate == 0d
                ? PercentDeltaResult.ReferenceZeroAndCandidateZero
                : PercentDeltaResult.ReferenceZero;
        }

        var percent = (candidate - reference) / reference * 100d;
        return PercentDeltaResult.Calculated(percent);
    }
}
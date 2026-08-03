namespace Bijecta.BenchmarkGate.Core.Comparison;

/// <summary>
/// The outcome of a <see cref="PercentDeltaCalculator.Calculate"/> call.
/// </summary>
/// <remarks>
/// Only ever construct this via <see cref="Calculated"/> or one of the
/// named static results below — never via <c>default(PercentDeltaResult)</c>.
/// Because this is a <c>record struct</c>, the runtime provides an implicit
/// parameterless constructor that this type cannot suppress; that default
/// instance reports <c>Status = Calculated</c> with a null <c>Value</c>,
/// which is not a state <see cref="PercentDeltaCalculator"/> ever produces
/// and violates this type's own contract (a <see cref="Calculated"/> status
/// must carry a non-null value). <see cref="PercentDeltaCalculator.Calculate"/>
/// is the only supported source of instances.
/// </remarks>
public readonly record struct PercentDeltaResult
{
    /// <summary>What kind of outcome this is. Always check this before reading <see cref="Value"/>.</summary>
    public PercentDeltaStatus Status { get; }

    /// <summary>
    /// The computed percentage delta, non-null if and only if
    /// <see cref="Status"/> is <see cref="PercentDeltaStatus.Calculated"/>.
    /// </summary>
    public double? Value { get; }

    private PercentDeltaResult(PercentDeltaStatus status, double? value)
    {
        Status = status;
        Value = value;
    }

    /// <summary>A successfully computed percentage delta.</summary>
    public static PercentDeltaResult Calculated(double value) =>
        new(PercentDeltaStatus.Calculated, value);

    /// <summary>Both reference and candidate are zero.</summary>
    public static PercentDeltaResult ReferenceZeroAndCandidateZero { get; } =
        new(PercentDeltaStatus.ReferenceZeroAndCandidateZero, null);

    /// <summary>The reference is zero and the candidate is a valid non-zero number.</summary>
    public static PercentDeltaResult ReferenceZero { get; } =
        new(PercentDeltaStatus.ReferenceZero, null);

    /// <summary>The reference value is NaN or Infinity.</summary>
    public static PercentDeltaResult InvalidReference { get; } =
        new(PercentDeltaStatus.InvalidReference, null);

    /// <summary>The candidate value is NaN or Infinity (reference was finite).</summary>
    public static PercentDeltaResult InvalidCandidate { get; } =
        new(PercentDeltaStatus.InvalidCandidate, null);
}
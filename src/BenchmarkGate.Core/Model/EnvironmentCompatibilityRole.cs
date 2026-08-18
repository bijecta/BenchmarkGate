namespace Bijecta.BenchmarkGate.Core.Model;

/// <summary>
/// The role a single <see cref="EnvironmentDimension"/> plays in environment compatibility
/// comparison (ADR-0006 Decision 2). This issue defines the role as data only; what a role value
/// does during comparison (e.g. a Filter mismatch forcing an Incompatible verdict) is comparison
/// logic added in a later issue.
/// </summary>
public enum EnvironmentCompatibilityRole
{
    /// <summary>A mismatch on this dimension can drive an environment compatibility verdict to Incompatible.</summary>
    Filter,

    /// <summary>A mismatch on this dimension is surfaced for visibility but never by itself drives an Incompatible verdict.</summary>
    Advisory,

    /// <summary>This dimension does not participate in compatibility comparison at all.</summary>
    None
}
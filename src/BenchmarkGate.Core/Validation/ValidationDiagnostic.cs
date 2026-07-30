namespace Bijecta.BenchmarkGate.Core.Validation;

/// <summary>
/// One reported validation finding: a DiagnosticDescriptor (what kind of
/// problem) plus the instance-specific detail (where, and with what
/// values). Severity always equals the descriptor's DefaultSeverity — no
/// per-instance override exists yet; see ADR-0003 for the extension point
/// if that's ever needed. Carries no source-file identity — see ADR-0003.
/// </summary>
/// <param name="Descriptor">The stable kind of finding this is.</param>
/// <param name="Path">
/// Where in the document this finding applies, e.g. a JSON pointer like
/// "/metrics/meanNanoseconds/warningPercent". Empty string if the finding
/// applies to the document as a whole. Deliberately unstructured — each
/// validator's addressing convention differs; see ADR-0003.
/// </param>
/// <param name="Message">Instance-specific human-readable detail.</param>
public sealed record ValidationDiagnostic(
    DiagnosticDescriptor Descriptor,
    string Path,
    string Message)
{
    public DiagnosticSeverity Severity => Descriptor.DefaultSeverity;
}
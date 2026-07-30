namespace Bijecta.BenchmarkGate.Core.Validation;

/// <summary>
/// How serious a single validation finding is. Every diagnostic is meant
/// to be something the user may need to fix or review — not a processing
/// note or status message. There is deliberately no Info tier; see
/// ADR-0003.
/// </summary>
public enum DiagnosticSeverity
{
    Warning,
    Error
}
namespace Bijecta.BenchmarkGate.Core.Validation;

/// <summary>
/// The complete outcome of validating one document (a policy file, a
/// baseline file, or a BenchmarkDotNet input file): every finding collected
/// in a single pass, rather than failing fast on the first problem — see
/// docs/ROADMAP.md's v0.3.0 rationale. This is what drives `validate`'s
/// exit code and report output, the same way SuiteDecision drives `check`.
/// </summary>
public sealed record ValidationResult(IReadOnlyList<ValidationDiagnostic> Diagnostics)
{
    /// <summary>
    /// No Error-severity diagnostics were found. A result can be IsValid
    /// and still have Warning-severity diagnostics to report.
    /// </summary>
    public bool IsValid => !Diagnostics.Any(static d => d.Severity == DiagnosticSeverity.Error);

    public int ErrorCount => Count(DiagnosticSeverity.Error);
    public int WarningCount => Count(DiagnosticSeverity.Warning);

    private int Count(DiagnosticSeverity severity) =>
        Diagnostics.Count(d => d.Severity == severity);
}
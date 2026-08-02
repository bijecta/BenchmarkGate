using Bijecta.BenchmarkGate.Core.Validation;

namespace Bijecta.BenchmarkGate.BenchmarkDotNet.Validation;

/// <summary>
/// Adapter-level diagnostics for failures that occur before a
/// BenchmarkDotNet document exists to hand to ObservationValidator — file
/// access, JSON syntax, and deserialization-shape failures. Reserved
/// range BGV390-BGV399, distinct from BGV300-BGV306's document/observation
/// findings, so `validate`'s aggregated output never conflates "this file
/// couldn't be read" with "this file's Benchmarks array was empty".
/// </summary>
internal static class BenchmarkDotNetInputDiagnostics
{
    internal static readonly DiagnosticDescriptor FileNotFound =
        new("BGV390", "Benchmark result file was not found", DiagnosticSeverity.Error);
    internal static readonly DiagnosticDescriptor FileReadFailed =
        new("BGV391", "Benchmark result file could not be read", DiagnosticSeverity.Error);
    internal static readonly DiagnosticDescriptor InvalidJson =
        new("BGV392", "Benchmark result file contains invalid JSON", DiagnosticSeverity.Error);
    internal static readonly DiagnosticDescriptor NullDocument =
        new("BGV393", "Benchmark result file produced no document", DiagnosticSeverity.Error);
    internal static readonly DiagnosticDescriptor PathNotFound =
        new("BGV394", "Results path does not exist", DiagnosticSeverity.Error);

    internal static IReadOnlyList<DiagnosticDescriptor> All { get; } =
    [
        FileNotFound, FileReadFailed, InvalidJson, NullDocument, PathNotFound,
    ];
}
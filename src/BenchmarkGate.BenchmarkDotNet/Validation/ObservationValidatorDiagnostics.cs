namespace Bijecta.BenchmarkGate.BenchmarkDotNet.Validation;

internal static class ObservationValidatorDiagnostics
{
    internal static readonly Core.Validation.DiagnosticDescriptor MissingBenchmarks =
        new("BGV300", "Benchmarks is missing or empty", Core.Validation.DiagnosticSeverity.Error);
    internal static readonly Core.Validation.DiagnosticDescriptor MissingType =
        new("BGV301", "Benchmark entry is missing 'Type'", Core.Validation.DiagnosticSeverity.Error);
    internal static readonly Core.Validation.DiagnosticDescriptor MissingMethod =
        new("BGV302", "Benchmark entry is missing 'Method'", Core.Validation.DiagnosticSeverity.Error);
    internal static readonly Core.Validation.DiagnosticDescriptor MissingMean =
        new("BGV303", "Benchmark entry is missing 'Statistics.Mean'", Core.Validation.DiagnosticSeverity.Error);
    internal static readonly Core.Validation.DiagnosticDescriptor DuplicateIdentityWithinFile =
        new("BGV304", "Duplicate benchmark identity within one file", Core.Validation.DiagnosticSeverity.Error);
    internal static readonly Core.Validation.DiagnosticDescriptor DuplicateIdentityAcrossFiles =
        new("BGV305", "Duplicate benchmark identity across result files", Core.Validation.DiagnosticSeverity.Error);
    internal static readonly Core.Validation.DiagnosticDescriptor MalformedParameterFragment =
        new("BGV306", "Malformed BenchmarkDotNet parameter fragment", Core.Validation.DiagnosticSeverity.Error);

    internal static IReadOnlyList<Core.Validation.DiagnosticDescriptor> All { get; } =
    [
        MissingBenchmarks, MissingType, MissingMethod, MissingMean,
        DuplicateIdentityWithinFile, DuplicateIdentityAcrossFiles,
        MalformedParameterFragment,
    ];
}
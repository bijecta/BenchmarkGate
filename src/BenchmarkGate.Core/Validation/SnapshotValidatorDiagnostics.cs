namespace Bijecta.BenchmarkGate.Core.Validation;

internal static class SnapshotValidatorDiagnostics
{
    internal static readonly DiagnosticDescriptor MissingSchemaVersion =
        new("BGV200", "Missing schema version", DiagnosticSeverity.Error);
    internal static readonly DiagnosticDescriptor UnsupportedSchemaVersion =
        new("BGV201", "Unsupported schema version", DiagnosticSeverity.Error);
    internal static readonly DiagnosticDescriptor MissingSuite =
        new("BGV202", "Missing 'suite'", DiagnosticSeverity.Error);
    internal static readonly DiagnosticDescriptor DuplicateBenchmarkIdentity =
        new("BGV203", "Duplicate benchmark identity", DiagnosticSeverity.Error);
    internal static readonly DiagnosticDescriptor MissingIdentityFields =
        new("BGV204", "Benchmark entry is missing required identity fields", DiagnosticSeverity.Error);
    internal static readonly DiagnosticDescriptor MissingMetrics =
        new("BGV205", "Benchmark entry has no metrics", DiagnosticSeverity.Error);

    internal static IReadOnlyList<DiagnosticDescriptor> All { get; } =
    [
        MissingSchemaVersion, UnsupportedSchemaVersion, MissingSuite,
        DuplicateBenchmarkIdentity, MissingIdentityFields, MissingMetrics,
    ];
}
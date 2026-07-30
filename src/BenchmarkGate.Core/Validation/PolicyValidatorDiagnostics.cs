namespace Bijecta.BenchmarkGate.Core.Validation;

internal static class PolicyValidatorDiagnostics
{
    internal static readonly DiagnosticDescriptor MissingSchemaVersion =
        new("BGV100", "Missing schema version", DiagnosticSeverity.Error);
    internal static readonly DiagnosticDescriptor UnsupportedSchemaVersion =
        new("BGV101", "Unsupported schema version", DiagnosticSeverity.Error);
    internal static readonly DiagnosticDescriptor MissingStability =
        new("BGV102", "Missing 'stability' section", DiagnosticSeverity.Error);
    internal static readonly DiagnosticDescriptor MissingMinimumMeasurements =
        new("BGV103", "Missing 'stability.minimumMeasurements'", DiagnosticSeverity.Error);
    internal static readonly DiagnosticDescriptor MissingMaximumCoefficientOfVariation =
        new("BGV104", "Missing 'stability.maximumCoefficientOfVariation'", DiagnosticSeverity.Error);
    internal static readonly DiagnosticDescriptor InvalidMinimumMeasurements =
        new("BGV105", "minimumMeasurements must be greater than zero", DiagnosticSeverity.Error);
    internal static readonly DiagnosticDescriptor InvalidMaximumCoefficientOfVariation =
        new("BGV106", "maximumCoefficientOfVariation must be finite and non-negative", DiagnosticSeverity.Error);
    internal static readonly DiagnosticDescriptor MissingMetrics =
        new("BGV107", "Policy defines no metrics", DiagnosticSeverity.Error);
    internal static readonly DiagnosticDescriptor EmptyMetricName =
        new("BGV108", "Metric name is empty", DiagnosticSeverity.Error);
    internal static readonly DiagnosticDescriptor MissingMetricDefinition =
        new("BGV109", "Metric definition is missing", DiagnosticSeverity.Error);
    internal static readonly DiagnosticDescriptor MissingDirection =
        new("BGV110", "Metric is missing 'direction'", DiagnosticSeverity.Error);
    internal static readonly DiagnosticDescriptor MissingWarningPercent =
        new("BGV111", "Metric is missing 'warningPercent'", DiagnosticSeverity.Error);
    internal static readonly DiagnosticDescriptor MissingFailurePercent =
        new("BGV112", "Metric is missing 'failurePercent'", DiagnosticSeverity.Error);
    internal static readonly DiagnosticDescriptor UnrecognizedDirection =
        new("BGV113", "Metric has an unrecognized 'direction' value", DiagnosticSeverity.Error);
    internal static readonly DiagnosticDescriptor InvalidWarningPercent =
        new("BGV114", "warningPercent must be finite and non-negative", DiagnosticSeverity.Error);
    internal static readonly DiagnosticDescriptor InvalidFailurePercent =
        new("BGV115", "failurePercent must be finite and non-negative", DiagnosticSeverity.Error);
    internal static readonly DiagnosticDescriptor InvalidMinimumAbsoluteChange =
        new("BGV116", "minimumAbsoluteChange must be finite and non-negative", DiagnosticSeverity.Error);
    internal static readonly DiagnosticDescriptor WarningNotLessThanFailure =
        new("BGV117", "warningPercent must be strictly less than failurePercent", DiagnosticSeverity.Error);

    internal static IReadOnlyList<DiagnosticDescriptor> All { get; } =
    [
        MissingSchemaVersion, UnsupportedSchemaVersion, MissingStability, MissingMinimumMeasurements,
        MissingMaximumCoefficientOfVariation, InvalidMinimumMeasurements, InvalidMaximumCoefficientOfVariation,
        MissingMetrics, EmptyMetricName, MissingMetricDefinition, MissingDirection, MissingWarningPercent,
        MissingFailurePercent, UnrecognizedDirection, InvalidWarningPercent, InvalidFailurePercent,
        InvalidMinimumAbsoluteChange, WarningNotLessThanFailure,
    ];
}
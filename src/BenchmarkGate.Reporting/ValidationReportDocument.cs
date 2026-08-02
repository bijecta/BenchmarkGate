using System.Text.Json.Serialization;

namespace Bijecta.BenchmarkGate.Reporting;

internal sealed record ValidationReportDocument(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("isValid")] bool IsValid,
    [property: JsonPropertyName("errorCount")] int ErrorCount,
    [property: JsonPropertyName("warningCount")] int WarningCount,
    [property: JsonPropertyName("artifacts")] IReadOnlyList<ValidationArtifactReport> Artifacts);

internal sealed record ValidationArtifactReport(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("isValid")] bool IsValid,
    [property: JsonPropertyName("errorCount")] int ErrorCount,
    [property: JsonPropertyName("warningCount")] int WarningCount,
    [property: JsonPropertyName("diagnostics")] IReadOnlyList<ValidationDiagnosticReport> Diagnostics);

internal sealed record ValidationDiagnosticReport(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("severity")] string Severity,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("message")] string Message);
using System.Text.Json;
using System.Text.Json.Serialization;
using Bijecta.BenchmarkGate.Core.Validation;
using Bijecta.BenchmarkGate.Storage.FileSystem;

namespace Bijecta.BenchmarkGate.Reporting;

public static class JsonValidationReporter
{
    private const int SchemaVersion = 1;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Write(
        string path,
        IReadOnlyList<(string Kind, string Source, ValidationResult? Validation, string? FailureMessage)> artifacts)
    {
        var artifactReports = artifacts.Select(a =>
        {
            var diagnostics = a.Validation?.Diagnostics
                .Select(d => new ValidationDiagnosticReport(
                    d.Descriptor.Id, d.Severity.ToString(), d.Descriptor.Title, d.Path, d.Message))
                .ToList() ?? [];

            return new ValidationArtifactReport(
                a.Kind, a.Source,
                a.FailureMessage is null && (a.Validation?.IsValid ?? false),
                a.Validation?.ErrorCount ?? 1,
                a.Validation?.WarningCount ?? 0,
                diagnostics);
        }).ToList();

        var document = new ValidationReportDocument(
            SchemaVersion,
            artifactReports.All(a => a.IsValid),
            artifactReports.Sum(a => a.ErrorCount),
            artifactReports.Sum(a => a.WarningCount),
            artifactReports);

        try
        {
            AtomicFileWriter.WriteJson(path, document, SerializerOptions);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ReportWriteException(path, "Failed to write JSON validation report.", ex);
        }
    }
}
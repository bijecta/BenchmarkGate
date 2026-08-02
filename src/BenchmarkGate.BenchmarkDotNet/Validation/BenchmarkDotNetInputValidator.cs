using System.Text.Json;
using Bijecta.BenchmarkGate.BenchmarkDotNet.Parsing;
using Bijecta.BenchmarkGate.Core.Validation;

namespace Bijecta.BenchmarkGate.BenchmarkDotNet.Validation;

public sealed record SourceValidationResult(string SourceFile, ValidationResult Validation);

/// <summary>
/// Non-compiling validation entry point for BenchmarkDotNet result input —
/// deserialize and validate only, no compilation to BenchmarkObservation.
/// Named separately from BenchmarkDotNetResultParser so the parser doesn't
/// conceptually own validation. Used by `benchmark-gate validate`. See
/// ADR-0003.
/// </summary>
public static class BenchmarkDotNetInputValidator
{
    public static IReadOnlyList<SourceValidationResult> ValidatePath(string path)
    {
        if (File.Exists(path))
            return [ValidateFile(path)];

        if (!Directory.Exists(path))
        {
            return string.IsNullOrEmpty(Path.GetExtension(path))
                ? [Invalid(path, BenchmarkDotNetInputDiagnostics.PathNotFound, $"Results path '{path}' does not exist.")]
                : [Invalid(path, BenchmarkDotNetInputDiagnostics.FileNotFound, $"Result file '{path}' does not exist.")];
        }

        var files = Directory.GetFiles(path, "*.json", SearchOption.AllDirectories)
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

        var perFile = new Dictionary<string, ValidationResult>();
        var parsedDocuments = new List<ParsedBenchmarkDotNetDocument>();

        foreach (var file in files)
        {
            var result = ValidateFile(file);
            perFile[file] = result.Validation;

            if (TryDeserialize(file, out var document))
                parsedDocuments.Add(new ParsedBenchmarkDotNetDocument(file, document!));
        }

        foreach (var sourced in ObservationSetValidator.ValidateWithSources(parsedDocuments))
        {
            if (perFile.TryGetValue(sourced.SourceFile, out var existing))
            {
                perFile[sourced.SourceFile] = new ValidationResult([.. existing.Diagnostics, sourced.Diagnostic]);
            }
        }

        return files.Select(f => new SourceValidationResult(f, perFile[f])).ToList();
    }

    private static SourceValidationResult ValidateFile(string path)
    {
        if (!File.Exists(path))
        {
            return Invalid(path, BenchmarkDotNetInputDiagnostics.FileNotFound,
                $"Result file '{path}' does not exist.");
        }

        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Invalid(path, BenchmarkDotNetInputDiagnostics.FileReadFailed,
                $"Result file '{path}' could not be read: {ex.Message}");
        }

        BdnReportRootDto? document;
        try
        {
            document = JsonSerializer.Deserialize<BdnReportRootDto>(json);
        }
        catch (JsonException ex)
        {
            return Invalid(path, BenchmarkDotNetInputDiagnostics.InvalidJson,
                $"Result file '{path}' contains invalid JSON: {ex.Message}");
        }

        if (document is null)
        {
            return Invalid(path, BenchmarkDotNetInputDiagnostics.NullDocument,
                $"Result file '{path}' deserialized to null.");
        }

        return new SourceValidationResult(path, ObservationValidator.Validate(document));
    }

    private static bool TryDeserialize(string path, out BdnReportRootDto? document)
    {
        try
        {
            document = File.Exists(path)
                ? JsonSerializer.Deserialize<BdnReportRootDto>(File.ReadAllText(path))
                : null;
            return document is not null;
        }
        catch (JsonException) { document = null; return false; }
        catch (IOException) { document = null; return false; }
        catch (UnauthorizedAccessException) { document = null; return false; }
    }

    private static SourceValidationResult Invalid(string source, DiagnosticDescriptor descriptor, string message) =>
        new(source, new ValidationResult([new ValidationDiagnostic(descriptor, "", message)]));
}
using System.Text.Json;
using System.Text.Json.Serialization;
using Bijecta.BenchmarkGate.Core.Baseline;
using Bijecta.BenchmarkGate.Core.Model;
using Bijecta.BenchmarkGate.Core.Validation;
using Bijecta.BenchmarkGate.Storage.FileSystem;

namespace Bijecta.BenchmarkGate.Tool.Baseline;

public sealed class BaselineFileException : Exception
{
    public string SourceFile { get; }
    public ValidationResult? ValidationResult { get; }

    public BaselineFileException(string sourceFile, string message)
        : base($"{message} (source file: '{sourceFile}')")
    {
        SourceFile = sourceFile;
    }

    public BaselineFileException(string sourceFile, string message, Exception innerException)
        : base($"{message} (source file: '{sourceFile}')", innerException)
    {
        SourceFile = sourceFile;
    }

    private BaselineFileException(string sourceFile, string message, ValidationResult validationResult)
        : base(message)
    {
        SourceFile = sourceFile;
        ValidationResult = validationResult;
    }

    internal static BaselineFileException FromValidationResult(string sourceFile, ValidationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.IsValid)
        {
            throw new ArgumentException(
                "A valid result cannot be converted into a baseline validation exception.", nameof(result));
        }

        return new BaselineFileException(sourceFile, BuildMessage(sourceFile, result), result);
    }

    private static string BuildMessage(string sourceFile, ValidationResult result)
    {
        var errors = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        var lines = errors.Select(d => $"  {d.Descriptor.Id} {d.Path}: {d.Message}");

        return $"Baseline '{sourceFile}' contains {errors.Count} validation error(s):" +
               Environment.NewLine + string.Join(Environment.NewLine, lines);
    }
}

public sealed class BaselineWriteException : Exception
{
    public string OutputPath { get; }

    public BaselineWriteException(string outputPath, string message, Exception innerException)
        : base($"{message} (output file: '{outputPath}')", innerException)
    {
        OutputPath = outputPath;
    }
}

public static class BaselineFile
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static BenchmarkBaseline Load(string path)
    {
        var document = Deserialize(path);

        var validation = SnapshotValidator.Validate(document);
        if (!validation.IsValid)
            throw BaselineFileException.FromValidationResult(path, validation);

        return BaselineCompiler.CompileValidated(document);
    }

    public static ValidationResult Validate(string path)
    {
        var document = Deserialize(path);
        return SnapshotValidator.Validate(document);
    }

    private static BaselineDocument Deserialize(string path)
    {
        if (!File.Exists(path))
            throw new BaselineFileException(path, "Baseline file does not exist.");

        BaselineDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<BaselineDocument>(File.ReadAllText(path));
        }
        catch (JsonException ex)
        {
            throw new BaselineFileException(path, "Baseline file has invalid JSON syntax or structure.", ex);
        }

        if (document is null)
            throw new BaselineFileException(path, "Baseline file deserialized to null.");

        return document;
    }

    public static void WriteCandidate(
        string path, string suite, IReadOnlyList<BenchmarkObservation> observations, bool overwrite = true)
    {
        var document = new BaselineDocument(
            SchemaVersion: BaselineFormat.CurrentSchemaVersion,
            Suite: suite,
            Benchmarks: observations
                .OrderBy(o => o.Identity.CanonicalString, StringComparer.Ordinal)
                .Select(o => new BaselineEntryDefinition(
                    new BaselineIdentityDefinition(
                        o.Identity.TypeName,
                        o.Identity.MethodName,
                        o.Identity.Job,
                        o.Identity.Parameters.Count > 0
                            ? new Dictionary<string, string>(o.Identity.Parameters)
                            : null),
                    new Dictionary<string, double>(o.Metrics)))
                .ToList());

        try
        {
            AtomicFileWriter.WriteJson(path, document, SerializerOptions, overwrite);
        }
        catch (IOException ex) when (!overwrite && File.Exists(path))
        {
            throw new BaselineWriteException(
                path, "Destination already exists. Re-run with --overwrite if you intend to replace it.", ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new BaselineWriteException(path, "Failed to write baseline candidate.", ex);
        }
    }
}
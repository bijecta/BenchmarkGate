using System.Text.Json;
using System.Text.Json.Serialization;
using Bijecta.BenchmarkGate.Core.Baseline;
using Bijecta.BenchmarkGate.Core.Model;
using Bijecta.BenchmarkGate.Core.Validation;
using Bijecta.BenchmarkGate.Storage.FileSystem;

namespace Bijecta.BenchmarkGate.Tool.Baseline;

/// <summary>
/// Thrown when a baseline file is malformed or unreadable, or fails
/// SnapshotValidator's semantic validation. Kept separate from
/// <c>Bijecta.BenchmarkGate.BenchmarkDotNet.Parsing.BenchmarkResultParseException</c>
/// since baseline files are not BenchmarkDotNet output.
/// </summary>
public sealed class BaselineFileException : Exception
{
    public string SourceFile { get; }

    /// <summary>
    /// The structured validation result, if this exception represents a
    /// SnapshotValidator failure. Null for file-access, JSON-syntax, or
    /// deserialization-shape failures, which never reach the validator.
    /// </summary>
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

/// <summary>
/// Thrown when a baseline file cannot be written (invalid path, access
/// denied, missing directory, disk full, atomic-write failure, or the
/// destination already exists and overwrite was not requested). Kept
/// separate from <see cref="BaselineFileException"/>, which represents
/// load/parse failures, not write failures.
/// </summary>
public sealed class BaselineWriteException : Exception
{
    public string OutputPath { get; }

    public BaselineWriteException(string outputPath, string message, Exception innerException)
        : base($"{message} (output file: '{outputPath}')", innerException)
    {
        OutputPath = outputPath;
    }
}

/// <summary>
/// Reads and writes the baseline JSON file format. File access, JSON
/// syntax, and deserialization-shape failures are fail-fast here; semantic
/// validation is delegated to
/// <see cref="Bijecta.BenchmarkGate.Core.Validation.SnapshotValidator"/>,
/// which collects every finding in one pass — the same validator
/// `benchmark-gate validate` uses. See ADR-0003.
/// </summary>
/// <remarks>
/// v0.2 bumped schemaVersion 1 -> 2: benchmarks[].meanNanoseconds (single
/// double) was replaced by benchmarks[].metrics (an object keyed by metric
/// name). This is a deliberate breaking change with no migration path —
/// this is a pre-1.0 internal tool with no external consumers, so
/// schemaVersion 1 files are rejected outright rather than carrying a
/// compatibility shim. Re-run `capture` to produce a schemaVersion 2
/// baseline. See SnapshotValidator's schemaVersion-1-specific diagnostic
/// message for the user-facing guidance.
/// </remarks>
public static class BaselineFile
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static BenchmarkBaseline Load(string path)
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

        var validation = SnapshotValidator.Validate(document);
        if (!validation.IsValid)
            throw BaselineFileException.FromValidationResult(path, validation);

        return BaselineCompiler.CompileValidated(document);
    }

    /// <summary>
    /// Writes a baseline candidate from a set of observations (used by the
    /// `capture` command). Output is deterministically ordered by canonical
    /// identity so the file diffs cleanly in source control.
    /// </summary>
    /// <param name="path">Destination file path.</param>
    /// <param name="suite">Suite name recorded in the baseline document.</param>
    /// <param name="observations">Observations to capture as baseline entries.</param>
    /// <param name="overwrite">
    /// When false, fails if <paramref name="path"/> already exists. This is
    /// enforced atomically inside AtomicFileWriter's commit (File.Move with
    /// overwrite: false) — not by a preceding File.Exists check — so there
    /// is no time-of-check/time-of-use race with another process.
    /// </param>
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
                path,
                "Destination already exists. Re-run with --overwrite if you intend to replace it.",
                ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new BaselineWriteException(path, "Failed to write baseline candidate.", ex);
        }
    }
}
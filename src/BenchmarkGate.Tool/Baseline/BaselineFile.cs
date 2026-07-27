using System.Text.Json;
using System.Text.Json.Serialization;
using Bijecta.BenchmarkGate.Core.Baseline;
using Bijecta.BenchmarkGate.Core.Identity;
using Bijecta.BenchmarkGate.Core.Model;

namespace Bijecta.BenchmarkGate.Tool.Baseline;

/// <summary>
/// Thrown when a baseline file is malformed or unreadable. Kept separate
/// from <c>Bijecta.BenchmarkGate.BenchmarkDotNet.Parsing.BenchmarkResultParseException</c>
/// since baseline files are not BenchmarkDotNet output.
/// </summary>
public sealed class BaselineFileException : Exception
{
    public string SourceFile { get; }

    public BaselineFileException(string sourceFile, string message) : base($"{message} (source file: '{sourceFile}')")
    {
        SourceFile = sourceFile;
    }

    public BaselineFileException(string sourceFile, string message, Exception innerException)
        : base($"{message} (source file: '{sourceFile}')", innerException)
    {
        SourceFile = sourceFile;
    }
}

/// <summary>
/// Reads and writes the baseline JSON file format.
/// </summary>
/// <remarks>
/// v0.2 bumps schemaVersion 1 -> 2: benchmarks[].meanNanoseconds (single
/// double) is replaced by benchmarks[].metrics (an object keyed by metric
/// name). This is a deliberate breaking change with no migration path —
/// this is a pre-1.0 internal tool with no external consumers, so
/// schemaVersion 1 files are rejected outright rather than carrying a
/// compatibility shim. Re-run `capture` to produce a schemaVersion 2
/// baseline.
/// </remarks>
public static class BaselineFile
{
    private const int SupportedSchemaVersion = 2;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static BenchmarkBaseline Load(string path)
    {
        if (!File.Exists(path))
            throw new BaselineFileException(path, "Baseline file does not exist.");

        BaselineDocumentDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<BaselineDocumentDto>(File.ReadAllText(path));
        }
        catch (JsonException ex)
        {
            throw new BaselineFileException(path, "Baseline file is not valid JSON.", ex);
        }

        if (dto is null)
            throw new BaselineFileException(path, "Baseline file deserialized to null.");

        if (dto.SchemaVersion != SupportedSchemaVersion)
        {
            var message = string.Create(System.Globalization.CultureInfo.InvariantCulture,
                $"Unsupported baseline schemaVersion {dto.SchemaVersion}. " +
                $"This build of Bijecta.BenchmarkGate supports schemaVersion {SupportedSchemaVersion}.");

            message += dto.SchemaVersion == 1
                ? " schemaVersion 1 baselines (single meanNanoseconds field) are no longer " +
                  "supported — re-run 'capture' to produce a schemaVersion 2 baseline."
                : " Re-run 'capture' with this version of the tool to produce a supported baseline.";

            throw new BaselineFileException(path, message);
        }

        if (string.IsNullOrWhiteSpace(dto.Suite))
            throw new BaselineFileException(path, "Baseline file is missing 'suite'.");

        var entries = new List<BaselineEntry>();
        foreach (var b in dto.Benchmarks ?? [])
        {
            if (b.Identity is null)
                throw new BaselineFileException(path, "Baseline entry is missing 'identity'.");
            if (string.IsNullOrWhiteSpace(b.Identity.TypeName))
                throw new BaselineFileException(path, "Baseline entry identity is missing 'typeName'.");
            if (string.IsNullOrWhiteSpace(b.Identity.MethodName))
                throw new BaselineFileException(path, "Baseline entry identity is missing 'methodName'.");
            if (b.Metrics is null || b.Metrics.Count == 0)
                throw new BaselineFileException(
                    path, $"Baseline entry '{b.Identity.TypeName}.{b.Identity.MethodName}' is missing 'metrics'.");

            var identity = new BenchmarkIdentity(
                b.Identity.TypeName,
                b.Identity.MethodName,
                b.Identity.Job ?? "Default",
                b.Identity.Parameters ?? new Dictionary<string, string>());

            entries.Add(new BaselineEntry(identity, b.Metrics));
        }

        // BenchmarkBaseline's constructor throws on duplicate identities.
        return new BenchmarkBaseline(dto.Suite, entries);
    }

    /// <summary>
    /// Writes a baseline candidate from a set of observations (used by the
    /// `capture` command). Output is deterministically ordered by canonical
    /// identity so the file diffs cleanly in source control.
    /// </summary>
    public static void WriteCandidate(string path, string suite, IReadOnlyList<BenchmarkObservation> observations)
    {
        var dto = new BaselineDocumentDto
        {
            SchemaVersion = SupportedSchemaVersion,
            Suite = suite,
            Benchmarks = observations
                .OrderBy(o => o.Identity.CanonicalString, StringComparer.Ordinal)
                .Select(o => new BaselineEntryDto
                {
                    Identity = new BaselineIdentityDto
                    {
                        TypeName = o.Identity.TypeName,
                        MethodName = o.Identity.MethodName,
                        Job = o.Identity.Job,
                        Parameters = o.Identity.Parameters.Count > 0
                            ? new Dictionary<string, string>(o.Identity.Parameters)
                            : null,
                    },
                    Metrics = new Dictionary<string, double>(o.Metrics),
                })
                .ToList(),
        };

        AtomicFileWriter.WriteJson(path, dto, SerializerOptions);
    }

    private sealed class BaselineDocumentDto
    {
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; set; }

        [JsonPropertyName("suite")]
        public string? Suite { get; set; }

        [JsonPropertyName("benchmarks")]
        public List<BaselineEntryDto>? Benchmarks { get; set; }
    }

    private sealed class BaselineEntryDto
    {
        [JsonPropertyName("identity")]
        public BaselineIdentityDto? Identity { get; set; }

        [JsonPropertyName("metrics")]
        public Dictionary<string, double>? Metrics { get; set; }
    }

    private sealed class BaselineIdentityDto
    {
        [JsonPropertyName("typeName")]
        public string? TypeName { get; set; }

        [JsonPropertyName("methodName")]
        public string? MethodName { get; set; }

        [JsonPropertyName("job")]
        public string? Job { get; set; }

        [JsonPropertyName("parameters")]
        public Dictionary<string, string>? Parameters { get; set; }
    }
}
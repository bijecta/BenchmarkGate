using System.Text.Json;
using System.Text.Json.Serialization;
using Bijecta.BenchmarkGate.Core.Baseline;
using Bijecta.BenchmarkGate.Core.Identity;
using Bijecta.BenchmarkGate.Core.Model;

namespace Bijecta.BenchmarkGate.Tool.Baseline;

/// <summary>
/// Thrown when a baseline file is malformed or unreadable. Kept separate
/// from <c>Nijecta.BenchmarkGate.BenchmarkDotNet.Parsing.BenchmarkResultParseException</c>
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
/// Reads and writes the baseline JSON file format. v0.1.0-alpha.1 uses a
/// deliberately reduced schema (schemaVersion, suite, benchmarks[].identity,
/// benchmarks[].meanNanoseconds) compared to the full master-spec schema
/// (section 7), which also carries provenance and environment blocks.
/// Those are deferred to v0.2 — see docs/baseline-schema.md.
/// </summary>
public static class BaselineFile
{
    private const int SupportedSchemaVersion = 1;

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
            throw new BaselineFileException(
                path,
                string.Create(System.Globalization.CultureInfo.InvariantCulture,
                    $"Unsupported baseline schemaVersion {dto.SchemaVersion}. " +
                    $"This build of Bijecta.BenchmarkGate supports schemaVersion {SupportedSchemaVersion}."));

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
            if (b.MeanNanoseconds is null)
                throw new BaselineFileException(
                    path, $"Baseline entry '{b.Identity.TypeName}.{b.Identity.MethodName}' is missing 'meanNanoseconds'.");

            var identity = new BenchmarkIdentity(
                b.Identity.TypeName,
                b.Identity.MethodName,
                b.Identity.Job ?? "Default",
                b.Identity.Parameters ?? new Dictionary<string, string>());

            entries.Add(new BaselineEntry(identity, b.MeanNanoseconds.Value));
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
                    MeanNanoseconds = o.MeanNanoseconds,
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

        [JsonPropertyName("meanNanoseconds")]
        public double? MeanNanoseconds { get; set; }
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

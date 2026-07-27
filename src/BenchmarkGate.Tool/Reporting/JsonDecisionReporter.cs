using System.Text.Json;
using System.Text.Json.Serialization;
using Bijecta.BenchmarkGate.Core.Evaluation;

namespace Bijecta.BenchmarkGate.Tool.Reporting;

/// <summary>
/// Writes the machine-readable decision document. v0.1.0-alpha.1 keeps this
/// minimal (schemaVersion, status, counts, benchmarks[]) — confirmation
/// filters and richer diagnostics are deferred to v0.2 per the roadmap.
/// </summary>
public static class JsonDecisionReporter
{
    private const int SchemaVersion = 1;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Write(string path, SuiteDecision decision)
    {
        var dto = new DecisionDocumentDto
        {
            SchemaVersion = SchemaVersion,
            ExitCode = decision.ExitCode,
            Improved = decision.ImprovedCount,
            Passed = decision.PassedCount,
            Regressed = decision.RegressedCount,
            Missing = decision.MissingCount,
            New = decision.NewCount,
            Benchmarks = decision.Benchmarks
                .OrderBy(b => b.Identity.CanonicalString, StringComparer.Ordinal)
                .Select(b => new DecisionBenchmarkDto
                {
                    Identity = b.Identity.CanonicalString,
                    Status = b.Status.ToString(),
                    BaselineMeanNanoseconds = b.BaselineMeanNanoseconds,
                    CurrentMeanNanoseconds = b.CurrentMeanNanoseconds,
                    AbsoluteDeltaNanoseconds = b.AbsoluteDeltaNanoseconds,
                    RelativeDeltaPercent = b.RelativeDeltaPercent,
                    Explanation = b.Explanation,
                })
                .ToList(),
        };

        AtomicFileWriter.WriteJson(path, dto, SerializerOptions);
    }

    private sealed class DecisionDocumentDto
    {
        [JsonPropertyName("schemaVersion")] public int SchemaVersion { get; set; }
        [JsonPropertyName("exitCode")] public int ExitCode { get; set; }
        [JsonPropertyName("improved")] public int Improved { get; set; }
        [JsonPropertyName("passed")] public int Passed { get; set; }
        [JsonPropertyName("regressed")] public int Regressed { get; set; }
        [JsonPropertyName("missing")] public int Missing { get; set; }
        [JsonPropertyName("new")] public int New { get; set; }
        [JsonPropertyName("benchmarks")] public List<DecisionBenchmarkDto>? Benchmarks { get; set; }
    }

    private sealed class DecisionBenchmarkDto
    {
        [JsonPropertyName("identity")] public string? Identity { get; set; }
        [JsonPropertyName("status")] public string? Status { get; set; }
        [JsonPropertyName("baselineMeanNanoseconds")] public double? BaselineMeanNanoseconds { get; set; }
        [JsonPropertyName("currentMeanNanoseconds")] public double? CurrentMeanNanoseconds { get; set; }
        [JsonPropertyName("absoluteDeltaNanoseconds")] public double? AbsoluteDeltaNanoseconds { get; set; }
        [JsonPropertyName("relativeDeltaPercent")] public double? RelativeDeltaPercent { get; set; }
        [JsonPropertyName("explanation")] public string? Explanation { get; set; }
    }
}

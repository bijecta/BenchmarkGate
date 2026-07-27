using System.Text.Json;
using System.Text.Json.Serialization;
using Bijecta.BenchmarkGate.Core.Evaluation;

namespace Bijecta.BenchmarkGate.Tool.Reporting;

/// <summary>
/// Writes the machine-readable decision document. Each benchmark carries a
/// nested metrics array (one entry per evaluated MetricDecision) instead of
/// flat baseline/current/delta fields, since a benchmark can now have more
/// than one metric (mean time, allocation, ...).
/// </summary>
public static class JsonDecisionReporter
{
    private const int SchemaVersion = 2;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Write(string path, SuiteDecision decision, bool failOnWarning)
    {
        var dto = new DecisionDocumentDto
        {
            SchemaVersion = SchemaVersion,
            ExitCode = decision.GetExitCode(failOnWarning),
            Improved = decision.ImprovedCount,
            Passed = decision.PassedCount,
            Warning = decision.WarningCount,
            Regressed = decision.RegressedCount,
            Missing = decision.MissingCount,
            New = decision.NewCount,
            Unstable = decision.UnstableCount,
            Benchmarks = decision.Benchmarks
                .OrderBy(b => b.Identity.CanonicalString, StringComparer.Ordinal)
                .Select(b => new DecisionBenchmarkDto
                {
                    Identity = b.Identity.CanonicalString,
                    Status = b.Status.ToString(),
                    Explanation = b.Explanation,
                    Metrics = b.Metrics
                        .Select(m => new DecisionMetricDto
                        {
                            MetricName = m.MetricName,
                            Status = m.Status.ToString(),
                            BaselineValue = m.BaselineValue,
                            CurrentValue = m.CurrentValue,
                            AbsoluteDelta = m.AbsoluteDelta,
                            RelativeDeltaPercent = m.RelativeDeltaPercent,
                            Explanation = m.Explanation,
                        })
                        .ToList(),
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
        [JsonPropertyName("warning")] public int Warning { get; set; }
        [JsonPropertyName("regressed")] public int Regressed { get; set; }
        [JsonPropertyName("missing")] public int Missing { get; set; }
        [JsonPropertyName("new")] public int New { get; set; }
        [JsonPropertyName("unstable")] public int Unstable { get; set; }
        [JsonPropertyName("benchmarks")] public List<DecisionBenchmarkDto>? Benchmarks { get; set; }
    }

    private sealed class DecisionBenchmarkDto
    {
        [JsonPropertyName("identity")] public string? Identity { get; set; }
        [JsonPropertyName("status")] public string? Status { get; set; }
        [JsonPropertyName("explanation")] public string? Explanation { get; set; }
        [JsonPropertyName("metrics")] public List<DecisionMetricDto>? Metrics { get; set; }
    }

    private sealed class DecisionMetricDto
    {
        [JsonPropertyName("metricName")] public string? MetricName { get; set; }
        [JsonPropertyName("status")] public string? Status { get; set; }
        [JsonPropertyName("baselineValue")] public double BaselineValue { get; set; }
        [JsonPropertyName("currentValue")] public double CurrentValue { get; set; }
        [JsonPropertyName("absoluteDelta")] public double AbsoluteDelta { get; set; }
        [JsonPropertyName("relativeDeltaPercent")] public double RelativeDeltaPercent { get; set; }
        [JsonPropertyName("explanation")] public string? Explanation { get; set; }
    }
}
using System.Text.Json;
using System.Text.Json.Serialization;
using Bijecta.BenchmarkGate.Core.Evaluation;

namespace Bijecta.BenchmarkGate.Tool.Policy;

/// <summary>
/// Thrown when a policy file is malformed or unreadable.
/// </summary>
public sealed class PolicyFileException : Exception
{
    public string SourceFile { get; }

    public PolicyFileException(string sourceFile, string message) : base($"{message} (source file: '{sourceFile}')")
    {
        SourceFile = sourceFile;
    }

    public PolicyFileException(string sourceFile, string message, Exception innerException)
        : base($"{message} (source file: '{sourceFile}')", innerException)
    {
        SourceFile = sourceFile;
    }
}

/// <summary>
/// Reads the policy.json file format into a <see cref="GatePolicy"/>.
/// Replaces v0.1's --threshold-percent/--minimum-absolute-change-ns CLI
/// flags with a committed, reviewable file (per-metric direction/warning/
/// failure thresholds plus a stability gate).
/// </summary>
public static class PolicyFile
{
    private const int SupportedSchemaVersion = 1;

    // Disallow unmapped members so a typo'd property name (e.g.
    // "warningPrecent") fails loudly at load time instead of being
    // silently ignored and the real property reported as merely "missing".
    // Requires .NET 8+.
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static GatePolicy Load(string path)
    {
        if (!File.Exists(path))
            throw new PolicyFileException(path, "Policy file does not exist.");

        PolicyDocumentDto? dto;
        try
        {
            using var stream = File.OpenRead(path);
            dto = JsonSerializer.Deserialize<PolicyDocumentDto>(stream, SerializerOptions);
        }
        catch (JsonException ex)
        {
            throw new PolicyFileException(path, "Policy file is not valid JSON.", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new PolicyFileException(path, "Access to the policy file was denied.", ex);
        }
        catch (IOException ex)
        {
            throw new PolicyFileException(path, "Could not read policy file.", ex);
        }

        if (dto is null)
            throw new PolicyFileException(path, "Policy file deserialized to null.");

        if (dto.SchemaVersion != SupportedSchemaVersion)
            throw new PolicyFileException(
                path,
                FormattableString.Invariant(
                    $"Unsupported policy schemaVersion {dto.SchemaVersion}. This build of Bijecta.BenchmarkGate supports schemaVersion {SupportedSchemaVersion}."));

        if (dto.Stability is null)
            throw new PolicyFileException(path, "Policy file is missing 'stability'.");
        if (dto.Stability.MinimumMeasurements is null)
            throw new PolicyFileException(path, "Policy file's 'stability' is missing 'minimumMeasurements'.");
        if (dto.Stability.MaximumCoefficientOfVariation is null)
            throw new PolicyFileException(path, "Policy file's 'stability' is missing 'maximumCoefficientOfVariation'.");

        var minimumMeasurements = dto.Stability.MinimumMeasurements.Value;
        var maximumCoefficientOfVariation = dto.Stability.MaximumCoefficientOfVariation.Value;

        if (minimumMeasurements <= 0)
            throw new PolicyFileException(
                path,
                FormattableString.Invariant(
                    $"stability.minimumMeasurements must be greater than zero, but was {minimumMeasurements}."));

        if (!double.IsFinite(maximumCoefficientOfVariation) || maximumCoefficientOfVariation < 0)
            throw new PolicyFileException(
                path,
                FormattableString.Invariant(
                    $"stability.maximumCoefficientOfVariation must be a finite, non-negative number, but was {maximumCoefficientOfVariation}."));

        if (dto.Metrics is null || dto.Metrics.Count == 0)
            throw new PolicyFileException(path, "Policy file must define at least one entry under 'metrics'.");

        var metrics = new Dictionary<string, MetricPolicy>(StringComparer.Ordinal);
        foreach (var (metricName, metricDto) in dto.Metrics)
        {
            if (string.IsNullOrWhiteSpace(metricName))
                throw new PolicyFileException(path, "Policy file contains an empty metric name.");

            if (metricDto.Direction is null)
                throw new PolicyFileException(path, $"Metric '{metricName}' is missing 'direction'.");
            if (metricDto.WarningPercent is null)
                throw new PolicyFileException(path, $"Metric '{metricName}' is missing 'warningPercent'.");
            if (metricDto.FailurePercent is null)
                throw new PolicyFileException(path, $"Metric '{metricName}' is missing 'failurePercent'.");

            // Strict, case-sensitive match — versioned machine-readable
            // format, not user-facing free text. Documented accepted
            // values rather than normalized casing.
            var direction = metricDto.Direction switch
            {
                "lower-is-better" => MetricDirection.LowerIsBetter,
                "higher-is-better" => MetricDirection.HigherIsBetter,
                _ => throw new PolicyFileException(
                    path,
                    $"Metric '{metricName}' has an unrecognized 'direction' value " +
                    $"'{metricDto.Direction}'. Expected 'lower-is-better' or 'higher-is-better'."),
            };

            var warningPercent = metricDto.WarningPercent.Value;
            var failurePercent = metricDto.FailurePercent.Value;
            var minimumAbsoluteChange = metricDto.MinimumAbsoluteChange ?? 0;

            if (!double.IsFinite(warningPercent) || warningPercent < 0)
                throw new PolicyFileException(
                    path,
                    FormattableString.Invariant(
                        $"Metric '{metricName}' has invalid warningPercent ({warningPercent}). It must be a finite, non-negative number."));

            if (!double.IsFinite(failurePercent) || failurePercent < 0)
                throw new PolicyFileException(
                    path,
                    FormattableString.Invariant(
                        $"Metric '{metricName}' has invalid failurePercent ({failurePercent}). It must be a finite, non-negative number."));

            if (!double.IsFinite(minimumAbsoluteChange) || minimumAbsoluteChange < 0)
                throw new PolicyFileException(
                    path,
                    FormattableString.Invariant(
                        $"Metric '{metricName}' has invalid minimumAbsoluteChange ({minimumAbsoluteChange}). It must be a finite, non-negative number."));

            if (warningPercent >= failurePercent)
                throw new PolicyFileException(
                    path,
                    FormattableString.Invariant(
                        $"Metric '{metricName}' has warningPercent ({warningPercent}) >= failurePercent ({failurePercent}). warningPercent must be strictly less than failurePercent for the policy to be meaningful."));

            metrics[metricName] = new MetricPolicy
            {
                Direction = direction,
                WarningPercent = warningPercent,
                FailurePercent = failurePercent,
                MinimumAbsoluteChange = minimumAbsoluteChange,
            };
        }

        return new GatePolicy
        {
            Stability = new StabilityPolicy
            {
                MinimumMeasurements = minimumMeasurements,
                MaximumCoefficientOfVariation = maximumCoefficientOfVariation,
            },
            Metrics = metrics,
        };
    }

    private sealed class PolicyDocumentDto
    {
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; set; }

        [JsonPropertyName("stability")]
        public StabilityDto? Stability { get; set; }

        [JsonPropertyName("metrics")]
        public Dictionary<string, MetricDto>? Metrics { get; set; }
    }

    private sealed class StabilityDto
    {
        [JsonPropertyName("minimumMeasurements")]
        public int? MinimumMeasurements { get; set; }

        [JsonPropertyName("maximumCoefficientOfVariation")]
        public double? MaximumCoefficientOfVariation { get; set; }
    }

    private sealed class MetricDto
    {
        [JsonPropertyName("direction")]
        public string? Direction { get; set; }

        [JsonPropertyName("warningPercent")]
        public double? WarningPercent { get; set; }

        [JsonPropertyName("failurePercent")]
        public double? FailurePercent { get; set; }

        [JsonPropertyName("minimumAbsoluteChange")]
        public double? MinimumAbsoluteChange { get; set; }
    }
}
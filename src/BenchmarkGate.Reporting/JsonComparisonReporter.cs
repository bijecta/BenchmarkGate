using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bijecta.BenchmarkGate.Core.Comparison;
using Bijecta.BenchmarkGate.Storage.FileSystem;

namespace Bijecta.BenchmarkGate.Reporting;

/// <summary>
/// Writes the machine-readable comparison document. Independently
/// versioned from <see cref="JsonDecisionReporter"/>, <c>JsonValidationReporter</c>,
/// and the baseline/policy document schemas — this schema tracks
/// <see cref="ComparisonResult"/>'s shape only, and changing it does not
/// imply anything about those other documents' compatibility.
/// </summary>
/// <remarks>
/// <para>
/// Full numeric precision is preserved: no value here is rounded before
/// serialization. Display rounding is a console/Markdown-only concern —
/// see <see cref="ConsoleComparisonReporter"/>/<see cref="MarkdownComparisonReporter"/>.
/// </para>
/// <para>
/// Non-finite IEEE-754 values (NaN, Infinity, -Infinity — preserved raw by
/// <see cref="ComparisonResult"/> for <see cref="MetricComparisonStatus.InvalidReferenceValue"/>/
/// <see cref="MetricComparisonStatus.InvalidCandidateValue"/> metrics) are
/// serialized using System.Text.Json's named floating-point representation
/// and therefore appear as JSON <em>strings</em> — <c>"NaN"</c>,
/// <c>"Infinity"</c>, <c>"-Infinity"</c> — not JSON numbers, at a given
/// value's position. Consumers must handle either a JSON number or a named
/// floating-point string for floating-point fields that preserve raw
/// benchmark values (<c>reference.value</c>, <c>candidate.value</c>,
/// <c>absoluteDelta</c>, <c>percentDelta</c>, <c>standardDeviationNanoseconds</c>);
/// integer/count fields (<c>schemaVersion</c>, <c>comparable</c>, <c>added</c>,
/// <c>removed</c>, <c>measurementCount</c>) cannot contain named
/// floating-point strings.
/// </para>
/// <para>
/// <c>status</c> and <c>direction</c> strings use BenchmarkGate's
/// established PascalCase JSON convention (matching
/// <see cref="JsonDecisionReporter"/>), via an explicit switch mapper
/// rather than raw enum <c>ToString()</c> — a future enum member rename
/// fails this build instead of silently changing the shipped contract.
/// </para>
/// <para>
/// <see cref="MetricComparison.Reference"/>/<c>.Candidate</c> are nested
/// value+unit objects rather than flattened fields, since (unlike
/// <see cref="JsonDecisionReporter"/>'s baseline/current fields) a unit is
/// meaningful metadata here, not something to drop or duplicate into a
/// second flat field.
/// </para>
/// </remarks>
public static class JsonComparisonReporter
{
    private const int SchemaVersion = 1;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // ComparisonResult deliberately preserves raw NaN/Infinity values
        // (InvalidReferenceValue/InvalidCandidateValue metrics) rather than
        // omitting them — System.Text.Json throws on those by default, so
        // this is required, not optional, for real compare output.
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    public static void Write(string path, ComparisonResult comparison)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(comparison);

        var dto = new ComparisonDocumentDto
        {
            SchemaVersion = SchemaVersion,
            Suite = comparison.Suite,
            Comparable = comparison.ComparableCount,
            Added = comparison.AddedCount,
            Removed = comparison.RemovedCount,
            // Preserves comparison.Benchmarks' given order rather than
            // re-sorting — see ConsoleComparisonReporter's remarks (ADR-0004).
            Benchmarks = comparison.Benchmarks
                .Select(b => new ComparisonBenchmarkDto
                {
                    Identity = b.Identity.CanonicalString,
                    Status = FormatBenchmarkStatus(b.Status),
                    CandidateStability = b.CandidateStability is { } stability
                        ? new ComparisonStabilityDto
                        {
                            MeasurementCount = stability.MeasurementCount,
                            StandardDeviationNanoseconds = stability.StandardDeviationNanoseconds,
                        }
                        : null,
                    Metrics = b.Metrics
                        .Select(m => new ComparisonMetricDto
                        {
                            MetricName = m.MetricName,
                            Status = FormatMetricStatus(m.Status),
                            Reference = ToValueDto(m.Reference),
                            Candidate = ToValueDto(m.Candidate),
                            AbsoluteDelta = m.AbsoluteDelta,
                            PercentDelta = m.PercentDelta,
                            Direction = m.Direction is { } direction ? FormatDirection(direction) : null,
                        })
                        .ToList(),
                })
                .ToList(),
        };

        try
        {
            AtomicFileWriter.WriteJson(path, dto, SerializerOptions);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ReportWriteException(path, "Failed to write JSON comparison report.", ex);
        }
    }

    private static ComparisonValueDto? ToValueDto(MetricValue? value) =>
        value is { } metricValue
            ? new ComparisonValueDto { Value = metricValue.Value, Unit = metricValue.Unit }
            : null;

    /// <summary>
    /// Explicit switch rather than <c>status.ToString()</c>: a future enum
    /// member rename fails this build instead of silently changing the
    /// shipped JSON contract. PascalCase, matching <see cref="JsonDecisionReporter"/>'s
    /// existing enum-string convention — kept consistent across the two
    /// JSON schemas deliberately, since casing is a project-wide JSON
    /// style question, not something schema-version independence should
    /// decide per-schema.
    /// </summary>
    private static string FormatBenchmarkStatus(BenchmarkComparisonStatus status) => status switch
    {
        BenchmarkComparisonStatus.Comparable => "Comparable",
        BenchmarkComparisonStatus.Added => "Added",
        BenchmarkComparisonStatus.Removed => "Removed",
        _ => throw new UnreachableException($"Unhandled {nameof(BenchmarkComparisonStatus)}: {status}"),
    };

    private static string FormatMetricStatus(MetricComparisonStatus status) => status switch
    {
        MetricComparisonStatus.Comparable => "Comparable",
        MetricComparisonStatus.MissingReferenceMetric => "MissingReferenceMetric",
        MetricComparisonStatus.MissingCandidateMetric => "MissingCandidateMetric",
        MetricComparisonStatus.UnitMismatch => "UnitMismatch",
        MetricComparisonStatus.InvalidReferenceValue => "InvalidReferenceValue",
        MetricComparisonStatus.InvalidCandidateValue => "InvalidCandidateValue",
        _ => throw new UnreachableException($"Unhandled {nameof(MetricComparisonStatus)}: {status}"),
    };

    private static string FormatDirection(ChangeDirection direction) => direction switch
    {
        ChangeDirection.Improvement => "Improvement",
        ChangeDirection.Unchanged => "Unchanged",
        ChangeDirection.Degradation => "Degradation",
        ChangeDirection.Indeterminate => "Indeterminate",
        _ => throw new UnreachableException($"Unhandled {nameof(ChangeDirection)}: {direction}"),
    };

    private sealed class ComparisonDocumentDto
    {
        [JsonPropertyName("schemaVersion")] public required int SchemaVersion { get; init; }
        [JsonPropertyName("suite")] public required string Suite { get; init; }
        [JsonPropertyName("comparable")] public required int Comparable { get; init; }
        [JsonPropertyName("added")] public required int Added { get; init; }
        [JsonPropertyName("removed")] public required int Removed { get; init; }
        [JsonPropertyName("benchmarks")] public required List<ComparisonBenchmarkDto> Benchmarks { get; init; }
    }

    private sealed class ComparisonBenchmarkDto
    {
        [JsonPropertyName("identity")] public required string Identity { get; init; }
        [JsonPropertyName("status")] public required string Status { get; init; }
        [JsonPropertyName("candidateStability")] public ComparisonStabilityDto? CandidateStability { get; init; }
        [JsonPropertyName("metrics")] public required List<ComparisonMetricDto> Metrics { get; init; }
    }

    private sealed class ComparisonStabilityDto
    {
        [JsonPropertyName("measurementCount")] public required int MeasurementCount { get; init; }
        [JsonPropertyName("standardDeviationNanoseconds")] public required double StandardDeviationNanoseconds { get; init; }
    }

    private sealed class ComparisonMetricDto
    {
        [JsonPropertyName("metricName")] public required string MetricName { get; init; }
        [JsonPropertyName("status")] public required string Status { get; init; }
        [JsonPropertyName("reference")] public ComparisonValueDto? Reference { get; init; }
        [JsonPropertyName("candidate")] public ComparisonValueDto? Candidate { get; init; }
        [JsonPropertyName("absoluteDelta")] public double? AbsoluteDelta { get; init; }
        [JsonPropertyName("percentDelta")] public double? PercentDelta { get; init; }
        [JsonPropertyName("direction")] public string? Direction { get; init; }
    }

    private sealed class ComparisonValueDto
    {
        [JsonPropertyName("value")] public required double Value { get; init; }
        [JsonPropertyName("unit")] public string? Unit { get; init; }
    }
}
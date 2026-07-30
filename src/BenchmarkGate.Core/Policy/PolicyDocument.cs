using System.Text.Json.Serialization;

namespace Bijecta.BenchmarkGate.Core.Policy;

public sealed record PolicyDocument(
    [property: JsonPropertyName("schemaVersion")] int? SchemaVersion,
    [property: JsonPropertyName("stability")] StabilityDefinition? Stability,
    [property: JsonPropertyName("metrics")] IReadOnlyDictionary<string, MetricDefinition?>? Metrics);

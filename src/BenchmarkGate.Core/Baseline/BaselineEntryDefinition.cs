using System.Text.Json.Serialization;

namespace Bijecta.BenchmarkGate.Core.Baseline;

public sealed record BaselineEntryDefinition(
    [property: JsonPropertyName("identity")] BaselineIdentityDefinition? Identity,
    [property: JsonPropertyName("metrics")] IReadOnlyDictionary<string, double>? Metrics);

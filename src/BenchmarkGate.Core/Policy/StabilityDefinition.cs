using System.Text.Json.Serialization;

namespace Bijecta.BenchmarkGate.Core.Policy;

public sealed record StabilityDefinition(
    [property: JsonPropertyName("minimumMeasurements")] int? MinimumMeasurements,
    [property: JsonPropertyName("maximumCoefficientOfVariation")] double? MaximumCoefficientOfVariation);

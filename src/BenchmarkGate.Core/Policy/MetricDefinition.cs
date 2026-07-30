using System.Text.Json.Serialization;

namespace Bijecta.BenchmarkGate.Core.Policy;

public sealed record MetricDefinition(
    [property: JsonPropertyName("direction")] string? Direction,
    [property: JsonPropertyName("warningPercent")] double? WarningPercent,
    [property: JsonPropertyName("failurePercent")] double? FailurePercent,
    [property: JsonPropertyName("minimumAbsoluteChange")] double? MinimumAbsoluteChange);
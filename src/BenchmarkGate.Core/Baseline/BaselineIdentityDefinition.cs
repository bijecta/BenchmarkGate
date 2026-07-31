using System.Text.Json.Serialization;

namespace Bijecta.BenchmarkGate.Core.Baseline;

public sealed record BaselineIdentityDefinition(
    [property: JsonPropertyName("typeName")] string? TypeName,
    [property: JsonPropertyName("methodName")] string? MethodName,
    [property: JsonPropertyName("job")] string? Job,
    [property: JsonPropertyName("parameters")] IReadOnlyDictionary<string, string>? Parameters);
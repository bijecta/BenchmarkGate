using System.Text.Json.Serialization;

namespace Bijecta.BenchmarkGate.BenchmarkDotNet.Parsing;

// These DTOs are intentionally permissive (nullable, tolerant of missing
// fields) because BenchmarkDotNet's exact JSON shape varies across
// versions. They exist only inside this project and must never be exposed
// to Core or Tool — see master spec section 4 ("Do not expose BenchmarkDotNet
// exporter DTOs to Core").

internal sealed class BdnReportRootDto
{
    [JsonPropertyName("Title")]
    public string? Title { get; set; }

    [JsonPropertyName("Benchmarks")]
    public List<BdnBenchmarkDto>? Benchmarks { get; set; }
}

internal sealed class BdnBenchmarkDto
{
    [JsonPropertyName("Type")]
    public string? Type { get; set; }

    [JsonPropertyName("Method")]
    public string? Method { get; set; }

    // Full JSON exporter represents parameters as a single display string,
    // e.g. "N=1000000" or "N=1000000,Distribution=Canonical". Empty string
    // (or absent) means the benchmark has no parameters.
    [JsonPropertyName("Parameters")]
    public string? Parameters { get; set; }

    [JsonPropertyName("Statistics")]
    public BdnStatisticsDto? Statistics { get; set; }
}

internal sealed class BdnStatisticsDto
{
    [JsonPropertyName("Mean")]
    public double? Mean { get; set; }
}

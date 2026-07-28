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

    // NEW — v0.2 multi-job identity source. There is no separate structured
    // Job field in BenchmarkDotNet's full-JSON output; the job id is
    // embedded inside this free-text string, e.g.
    // "ExceptionClassifierBenchmark.IndexedClassifier: Job-SNYTAA(IterationCount=10, ...) [N=1000000]".
    // Confirmed against a real CedarRecon report-full.json fragment. The
    // parser (step 8) must extract the "Job-XXXXXX" token via regex/split,
    // falling back to "Default" if the pattern isn't found (e.g. single-job
    // runs may omit the "Job-..." segment entirely — unconfirmed, handle
    // defensively).
    [JsonPropertyName("DisplayInfo")]
    public string? DisplayInfo { get; set; }

    [JsonPropertyName("Statistics")]
    public BdnStatisticsDto? Statistics { get; set; }

    // NEW — v0.2 allocation tracking. Confirmed against real output: absent
    // entirely if MemoryDiagnoser wasn't enabled for the run; the parser
    // must treat that as "no allocation metric available", not an error.
    [JsonPropertyName("Memory")]
    public BdnMemoryDto? Memory { get; set; }
}

internal sealed class BdnStatisticsDto
{
    [JsonPropertyName("Mean")]
    public double? Mean { get; set; }

    // NEW — v0.2 stability fields. Confirmed against real output.
    [JsonPropertyName("N")]
    public int? N { get; set; }

    [JsonPropertyName("StandardDeviation")]
    public double? StandardDeviation { get; set; }
}

// NEW — v0.2 allocation tracking (MemoryDiagnoser output).
// Confirmed against real output: BytesAllocatedPerOperation is the correct
// field name, present directly under Memory.
internal sealed class BdnMemoryDto
{
    [JsonPropertyName("BytesAllocatedPerOperation")]
    public long? BytesAllocatedPerOperation { get; set; }
}
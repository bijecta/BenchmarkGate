using System.Text.Json;
using Cedar.BenchmarkGate.Core.Identity;
using Cedar.BenchmarkGate.Core.Model;

namespace Cedar.BenchmarkGate.BenchmarkDotNet.Parsing;

/// <summary>
/// Parses BenchmarkDotNet full-JSON exporter output into normalized
/// <see cref="BenchmarkObservation"/> values.
/// </summary>
/// <remarks>
/// v0.1.0-alpha.1 simplification: BenchmarkDotNet's full JSON export does
/// not carry a simple top-level "job id" string the way the master spec's
/// canonical identity example assumes. Until job-aware parsing is added
/// (tracked for v0.2, once we have real multi-job fixtures from CedarRecon),
/// every parsed benchmark is assigned the fixed job name "Default". This
/// means v0.1 cannot yet distinguish the same benchmark run under two
/// different BenchmarkDotNet jobs in a single result set — acceptable for
/// CedarRecon's current single-job CI usage, not acceptable for v1.
/// </remarks>
public static class BenchmarkDotNetResultParser
{
    private const string DefaultJob = "Default";

    /// <summary>
    /// Parses a single BenchmarkDotNet full-JSON report file.
    /// </summary>
    public static IReadOnlyList<BenchmarkObservation> ParseFile(string path)
    {
        if (!File.Exists(path))
            throw new BenchmarkResultParseException(path, "Result file does not exist.");

        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (IOException ex)
        {
            throw new BenchmarkResultParseException(path, "Could not read result file.", ex);
        }

        BdnReportRootDto? root;
        try
        {
            root = JsonSerializer.Deserialize<BdnReportRootDto>(json);
        }
        catch (JsonException ex)
        {
            throw new BenchmarkResultParseException(path, "Result file is not valid JSON.", ex);
        }

        if (root is null)
            throw new BenchmarkResultParseException(path, "Result file deserialized to null.");

        if (root.Benchmarks is null || root.Benchmarks.Count == 0)
            throw new BenchmarkResultParseException(
                path, "Result file contains no 'Benchmarks' array, or it is empty.");

        var observations = new List<BenchmarkObservation>(root.Benchmarks.Count);
        var seenIdentities = new HashSet<string>(StringComparer.Ordinal);

        foreach (var benchmark in root.Benchmarks)
        {
            var observation = ParseBenchmark(benchmark, path);

            if (!seenIdentities.Add(observation.Identity.CanonicalString))
            {
                throw new BenchmarkResultParseException(
                    path,
                    $"Duplicate benchmark identity in results: '{observation.Identity.CanonicalString}'.");
            }

            observations.Add(observation);
        }

        return observations;
    }

    /// <summary>
    /// Parses every *.json file found under <paramref name="path"/> (if it's
    /// a directory) or the single file at <paramref name="path"/>. Duplicate
    /// identities across multiple files are rejected the same as within one
    /// file, since they refer to the same logical benchmark.
    /// </summary>
    public static IReadOnlyList<BenchmarkObservation> ParsePath(string path)
    {
        if (File.Exists(path))
            return ParseFile(path);

        if (!Directory.Exists(path))
            throw new BenchmarkResultParseException(path, "Results path does not exist.");

        var files = Directory.GetFiles(path, "*.json", SearchOption.AllDirectories);
        if (files.Length == 0)
            throw new BenchmarkResultParseException(path, "No *.json result files found under directory.");

        var allObservations = new List<BenchmarkObservation>();
        var seenIdentities = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in files.OrderBy(f => f, StringComparer.Ordinal))
        {
            foreach (var observation in ParseFile(file))
            {
                if (!seenIdentities.Add(observation.Identity.CanonicalString))
                {
                    throw new BenchmarkResultParseException(
                        path,
                        $"Duplicate benchmark identity across result files: " +
                        $"'{observation.Identity.CanonicalString}' (encountered while reading '{file}').");
                }

                allObservations.Add(observation);
            }
        }

        return allObservations;
    }

    private static BenchmarkObservation ParseBenchmark(BdnBenchmarkDto dto, string sourceFile)
    {
        if (string.IsNullOrWhiteSpace(dto.Type))
            throw new BenchmarkResultParseException(sourceFile, "Benchmark entry is missing 'Type'.");
        if (string.IsNullOrWhiteSpace(dto.Method))
            throw new BenchmarkResultParseException(sourceFile, "Benchmark entry is missing 'Method'.");
        if (dto.Statistics?.Mean is not { } mean)
            throw new BenchmarkResultParseException(
                sourceFile,
                $"Benchmark '{dto.Type}.{dto.Method}' is missing 'Statistics.Mean'.");

        var parameters = BdnParameterStringParser.Parse(dto.Parameters);
        var identity = new BenchmarkIdentity(dto.Type, dto.Method, DefaultJob, parameters);

        return new BenchmarkObservation(identity, mean);
    }
}

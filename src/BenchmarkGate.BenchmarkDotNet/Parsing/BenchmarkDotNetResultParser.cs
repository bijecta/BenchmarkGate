using System.Text.Json;
using System.Text.RegularExpressions;
using Bijecta.BenchmarkGate.Core.Identity;
using Bijecta.BenchmarkGate.Core.Model;

namespace Bijecta.BenchmarkGate.BenchmarkDotNet.Parsing;

/// <summary>
/// Parses BenchmarkDotNet full-JSON exporter output into normalized
/// <see cref="BenchmarkObservation"/> values.
/// </summary>
/// <remarks>
/// v0.2: job identity is extracted from the free-text 'DisplayInfo' field
/// (there is no structured Job field in BenchmarkDotNet's JSON export). The
/// observed shape is "&lt;prefix&gt;: &lt;job-token&gt;[(params)] [Parameters]",
/// e.g. "MismatchScan: DefaultJob [N=1000000]" or
/// "Type.Method: Job-SNYTAA(IterationCount=10, ...) [N=1000000]". If the
/// pattern isn't found (DisplayInfo missing or unrecognized shape), falls
/// back to "Default" — matching v0.1 behavior for result sets with no
/// job information at all.
/// </remarks>
public static class BenchmarkDotNetResultParser
{
    private const string DefaultJob = "Default";

    // Matches ": " followed by a run of non-whitespace, non-'(' characters
    // — the job token, whether or not it's followed by a parenthesized
    // parameter list (Job-SNYTAA(...) vs DefaultJob with no parens).
    private static readonly Regex JobTokenPattern = new(@": (?<job>[^\s(]+)", RegexOptions.Compiled);

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

        var metrics = new Dictionary<string, double>
        {
            [BenchmarkObservation.MeanNanosecondsMetric] = mean
        };

        // Allocation is optional: absent Memory block (no MemoryDiagnoser
        // enabled) means this metric simply isn't in the dictionary, not an
        // error and not a zero.
        if (dto.Memory?.BytesAllocatedPerOperation is { } bytesAllocated)
        {
            metrics[BenchmarkObservation.AllocatedBytesMetric] = bytesAllocated;
        }

        var measurementCount = dto.Statistics?.N ?? 0;
        var standardDeviation = dto.Statistics?.StandardDeviation ?? 0;

        var job = ExtractJob(dto.DisplayInfo);
        var parameters = BdnParameterStringParser.Parse(dto.Parameters);
        var identity = new BenchmarkIdentity(dto.Type, dto.Method, job, parameters);

        return new BenchmarkObservation(identity, metrics, measurementCount, standardDeviation);
    }

    private static string ExtractJob(string? displayInfo)
    {
        if (string.IsNullOrWhiteSpace(displayInfo))
            return DefaultJob;

        var match = JobTokenPattern.Match(displayInfo);
        return match.Success ? match.Groups["job"].Value : DefaultJob;
    }
}
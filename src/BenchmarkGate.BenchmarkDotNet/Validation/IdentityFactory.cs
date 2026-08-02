using Bijecta.BenchmarkGate.BenchmarkDotNet.Parsing;
using Bijecta.BenchmarkGate.Core.Identity;

namespace Bijecta.BenchmarkGate.BenchmarkDotNet.Validation;

/// <summary>
/// Builds a BenchmarkIdentity from a raw BdnBenchmarkDto, shared by
/// ObservationValidator (duplicate detection) and
/// BenchmarkDotNetResultParser (final BenchmarkObservation compilation)
/// so identity composition exists in exactly one place. Returns null only
/// when Type or Method is missing — the caller is responsible for having
/// already reported that via BGV301/BGV302.
/// </summary>
internal static class IdentityFactory
{
    private const string DefaultJob = "Default";

    private static readonly System.Text.RegularExpressions.Regex JobTokenPattern =
        new(@": (?<job>[^\s(]+)", System.Text.RegularExpressions.RegexOptions.Compiled);

    internal static BenchmarkIdentity? TryCreate(BdnBenchmarkDto benchmark)
    {
        if (string.IsNullOrWhiteSpace(benchmark.Type) || string.IsNullOrWhiteSpace(benchmark.Method))
            return null;

        var job = ExtractJob(benchmark.DisplayInfo);
        var parameters = BdnParameterStringParser.Parse(benchmark.Parameters);
        return new BenchmarkIdentity(benchmark.Type, benchmark.Method, job, parameters);
    }

    private static string ExtractJob(string? displayInfo)
    {
        if (string.IsNullOrWhiteSpace(displayInfo))
            return DefaultJob;

        var match = JobTokenPattern.Match(displayInfo);
        return match.Success ? match.Groups["job"].Value : DefaultJob;
    }
}
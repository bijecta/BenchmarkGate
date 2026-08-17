using Bijecta.BenchmarkGate.BenchmarkDotNet.Parsing;
using Bijecta.BenchmarkGate.Core.Identity;

namespace Bijecta.BenchmarkGate.BenchmarkDotNet.Validation;

/// <summary>
/// The outcome of interpreting one BdnBenchmarkDto as a BenchmarkIdentity:
/// the identity itself, plus any malformed parameter fragments
/// encountered while parsing. Identity is absent only when Type or Method
/// is missing — the caller is responsible for having already reported
/// that via BGV301/BGV302.
/// </summary>
/// <param name="Identity">The constructed identity, or null if Type or Method was missing.</param>
/// <param name="ParameterIssues">
/// Malformed parameter fragments encountered while parsing, in the order
/// they were found. Empty when parsing succeeded cleanly or was not
/// attempted (Identity is null).
/// </param>
internal sealed record IdentityCreationResult(
    BenchmarkIdentity? Identity,
    IReadOnlyList<ParameterParseIssue> ParameterIssues);

/// <summary>
/// Builds a BenchmarkIdentity from a raw BdnBenchmarkDto, shared by
/// ObservationValidator (duplicate detection) and
/// BenchmarkDotNetResultParser (final BenchmarkObservation compilation) so
/// identity composition — and parameter-string parsing — exists in
/// exactly one place. ObservationValidator must never call
/// BdnParameterStringParser directly; all interpretation of a benchmark's
/// raw parameter string goes through this class (see Issue #16).
/// </summary>
internal static class IdentityFactory
{
    private const string DefaultJob = "Default";

    private static readonly System.Text.RegularExpressions.Regex JobTokenPattern =
        new(@": (?<job>[^\s(]+)", System.Text.RegularExpressions.RegexOptions.Compiled);

    internal static IdentityCreationResult Create(BdnBenchmarkDto benchmark)
    {
        if (string.IsNullOrWhiteSpace(benchmark.Type) || string.IsNullOrWhiteSpace(benchmark.Method))
            return new IdentityCreationResult(Identity: null, ParameterIssues: []);

        var job = ExtractJob(benchmark.DisplayInfo);
        var parameterResult = BdnParameterStringParser.Parse(benchmark.Parameters);
        var identity = new BenchmarkIdentity(benchmark.Type, benchmark.Method, job, parameterResult.Parameters);

        return new IdentityCreationResult(identity, parameterResult.Issues);
    }

    private static string ExtractJob(string? displayInfo)
    {
        if (string.IsNullOrWhiteSpace(displayInfo))
            return DefaultJob;

        var match = JobTokenPattern.Match(displayInfo);
        return match.Success ? match.Groups["job"].Value : DefaultJob;
    }
}
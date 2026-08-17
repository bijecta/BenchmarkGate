using Bijecta.BenchmarkGate.BenchmarkDotNet.Parsing;
using Bijecta.BenchmarkGate.Core.Identity;
using Bijecta.BenchmarkGate.Core.Validation;

namespace Bijecta.BenchmarkGate.BenchmarkDotNet.Validation;

/// <summary>
/// Validates one deserialized BenchmarkDotNet report document, collecting
/// every finding in one pass rather than failing fast. Adapter-owned per
/// ADR-0003 — Core has no knowledge of BenchmarkDotNet's document shape or
/// these BGV3xx codes. Parameter-string parsing is never performed here
/// directly — all identity/parameter interpretation goes through
/// IdentityFactory (see Issue #16).
/// </summary>
internal static class ObservationValidator
{
    internal static ValidationResult Validate(BdnReportRootDto document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var diagnostics = new List<ValidationDiagnostic>();

        if (document.Benchmarks is null || document.Benchmarks.Count == 0)
        {
            diagnostics.Add(new ValidationDiagnostic(
                ObservationValidatorDiagnostics.MissingBenchmarks, "/Benchmarks",
                "Result file contains no 'Benchmarks' array, or it is empty."));
            return new ValidationResult(diagnostics);
        }

        var seenIdentities = new HashSet<BenchmarkIdentity>();

        for (var index = 0; index < document.Benchmarks.Count; index++)
        {
            var benchmark = document.Benchmarks[index];
            var path = $"/Benchmarks/{index}";

            if (string.IsNullOrWhiteSpace(benchmark.Type))
            {
                diagnostics.Add(new ValidationDiagnostic(
                    ObservationValidatorDiagnostics.MissingType, $"{path}/Type",
                    $"Benchmark entry at index {index} is missing 'Type'."));
            }

            if (string.IsNullOrWhiteSpace(benchmark.Method))
            {
                diagnostics.Add(new ValidationDiagnostic(
                    ObservationValidatorDiagnostics.MissingMethod, $"{path}/Method",
                    $"Benchmark entry at index {index} is missing 'Method'."));
            }

            if (benchmark.Statistics?.Mean is null)
            {
                var label = string.IsNullOrWhiteSpace(benchmark.Type) || string.IsNullOrWhiteSpace(benchmark.Method)
                    ? $"index {index}"
                    : $"{benchmark.Type}.{benchmark.Method}";
                diagnostics.Add(new ValidationDiagnostic(
                    ObservationValidatorDiagnostics.MissingMean, $"{path}/Statistics/Mean",
                    $"Benchmark '{label}' is missing 'Statistics.Mean'."));
            }

            // Duplicate detection only runs for entries with a fully
            // constructible identity — an entry already missing Type/Method
            // has no trustworthy identity to compare, and would otherwise
            // produce a spurious duplicate alongside its real BGV301/302.
            var identityResult = IdentityFactory.Create(benchmark);

            foreach (var issue in identityResult.ParameterIssues)
            {
                diagnostics.Add(new ValidationDiagnostic(
                    ObservationValidatorDiagnostics.MalformedParameterFragment,
                    $"{path}/Parameters",
                    DescribeParameterIssue(issue)));
            }

            if (identityResult.Identity is { } identity && !seenIdentities.Add(identity))
            {
                diagnostics.Add(new ValidationDiagnostic(
                    ObservationValidatorDiagnostics.DuplicateIdentityWithinFile, path,
                    $"Duplicate benchmark identity '{identity}'."));
            }
        }

        return new ValidationResult(diagnostics);
    }

    private static string DescribeParameterIssue(ParameterParseIssue issue) => issue.Kind switch
    {
        ParameterParseIssueKind.MissingSeparator =>
            $"Malformed BenchmarkDotNet parameter fragment at position {issue.FragmentIndex}: " +
            $"'{issue.Fragment}' does not contain a '=' separator.",
        ParameterParseIssueKind.EmptyKey =>
            $"Malformed BenchmarkDotNet parameter fragment at position {issue.FragmentIndex}: " +
            $"'{issue.Fragment}' has an empty parameter name.",
        _ => $"Malformed BenchmarkDotNet parameter fragment at position {issue.FragmentIndex}: '{issue.Fragment}'.",
    };
}
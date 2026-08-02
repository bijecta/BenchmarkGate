using Bijecta.BenchmarkGate.BenchmarkDotNet.Parsing;
using Bijecta.BenchmarkGate.Core.Identity;
using Bijecta.BenchmarkGate.Core.Validation;

namespace Bijecta.BenchmarkGate.BenchmarkDotNet.Validation;

internal sealed record ParsedBenchmarkDotNetDocument(string SourceFile, BdnReportRootDto Document);

internal sealed record SourceValidationDiagnostic(string SourceFile, ValidationDiagnostic Diagnostic);

internal static class ObservationSetValidator
{
    internal static IReadOnlyList<SourceValidationDiagnostic> ValidateWithSources(
        IReadOnlyList<ParsedBenchmarkDotNetDocument> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);

        var results = new List<SourceValidationDiagnostic>();
        var firstSeenIn = new Dictionary<BenchmarkIdentity, string>();

        foreach (var parsed in documents)
        {
            var identitiesInThisFile = new HashSet<BenchmarkIdentity>();

            foreach (var benchmark in parsed.Document.Benchmarks ?? [])
            {
                var identity = IdentityFactory.TryCreate(benchmark);
                if (identity is null || !identitiesInThisFile.Add(identity))
                    continue;

                if (firstSeenIn.TryGetValue(identity, out var firstFile))
                {
                    var diagnostic = new ValidationDiagnostic(
                        ObservationValidatorDiagnostics.DuplicateIdentityAcrossFiles, "/Benchmarks",
                        $"Duplicate benchmark identity '{identity}' occurs in both " +
                        $"'{firstFile}' and '{parsed.SourceFile}'.");

                    results.Add(new SourceValidationDiagnostic(parsed.SourceFile, diagnostic));
                }
                else
                {
                    firstSeenIn[identity] = parsed.SourceFile;
                }
            }
        }

        return results;
    }

    internal static ValidationResult Validate(IReadOnlyList<ParsedBenchmarkDotNetDocument> documents) =>
        new(ValidateWithSources(documents).Select(x => x.Diagnostic).ToList());
}
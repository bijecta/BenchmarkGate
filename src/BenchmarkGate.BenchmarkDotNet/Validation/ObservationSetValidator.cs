using Bijecta.BenchmarkGate.BenchmarkDotNet.Parsing;
using Bijecta.BenchmarkGate.Core.Identity;
using Bijecta.BenchmarkGate.Core.Validation;

namespace Bijecta.BenchmarkGate.BenchmarkDotNet.Validation;

/// <summary>
/// A parsed document paired with the file it came from — needed for
/// cross-file duplicate detection, which must be able to name the
/// colliding files in its diagnostic message.
/// </summary>
internal sealed record ParsedBenchmarkDotNetDocument(string SourceFile, BdnReportRootDto Document);

/// <summary>
/// Validates identity uniqueness across multiple already-individually-valid
/// documents (a directory parse). Separate from ObservationValidator
/// because this operates on a collection, not one document — BGV305 is
/// fundamentally a different scope than BGV300-304.
/// </summary>
internal static class ObservationSetValidator
{
    internal static ValidationResult Validate(IReadOnlyList<ParsedBenchmarkDotNetDocument> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);

        var diagnostics = new List<ValidationDiagnostic>();
        var firstSeenIn = new Dictionary<BenchmarkIdentity, string>();

        foreach (var parsed in documents)
        {
            // BGV304 (within-file duplicates) already excludes invalid
            // identities from its own set; here we only need identities
            // that were fully constructible, same rule, applied per file.
            var identitiesInThisFile = new HashSet<BenchmarkIdentity>();

            foreach (var benchmark in parsed.Document.Benchmarks ?? [])
            {
                var identity = IdentityFactory.TryCreate(benchmark);
                if (identity is null || !identitiesInThisFile.Add(identity))
                    continue; // already reported by ObservationValidator, or invalid

                if (firstSeenIn.TryGetValue(identity, out var firstFile))
                {
                    diagnostics.Add(new ValidationDiagnostic(
                        ObservationValidatorDiagnostics.DuplicateIdentityAcrossFiles, "/Benchmarks",
                        $"Duplicate benchmark identity '{identity}' occurs in both " +
                        $"'{firstFile}' and '{parsed.SourceFile}'."));
                }
                else
                {
                    firstSeenIn[identity] = parsed.SourceFile;
                }
            }
        }

        return new ValidationResult(diagnostics);
    }
}
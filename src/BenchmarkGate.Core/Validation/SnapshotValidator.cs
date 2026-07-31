using Bijecta.BenchmarkGate.Core.Baseline;
using Bijecta.BenchmarkGate.Core.Identity;

namespace Bijecta.BenchmarkGate.Core.Validation;

public static class SnapshotValidator
{
    public static ValidationResult Validate(BaselineDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var diagnostics = new List<ValidationDiagnostic>();

        ValidateSchemaVersion(document.SchemaVersion, diagnostics);
        ValidateSuite(document.Suite, diagnostics);
        ValidateEntries(document.Benchmarks, diagnostics);

        return new ValidationResult(diagnostics);
    }

    private static void ValidateSchemaVersion(int? schemaVersion, List<ValidationDiagnostic> diagnostics)
    {
        if (schemaVersion is null)
        {
            diagnostics.Add(new ValidationDiagnostic(
                SnapshotValidatorDiagnostics.MissingSchemaVersion, "/schemaVersion",
                "Baseline is missing 'schemaVersion'."));
            return;
        }

        if (schemaVersion.Value == BaselineFormat.CurrentSchemaVersion)
            return;

        var message = schemaVersion.Value == 1
            ? "Baseline schemaVersion 1 is no longer supported. " +
              "Re-run 'benchmark-gate capture' to generate a schemaVersion 2 baseline."
            : FormattableString.Invariant(
                $"Unsupported baseline schemaVersion {schemaVersion.Value}. This build supports schemaVersion {BaselineFormat.CurrentSchemaVersion}.");

        diagnostics.Add(new ValidationDiagnostic(
            SnapshotValidatorDiagnostics.UnsupportedSchemaVersion, "/schemaVersion", message));
    }

    private static void ValidateSuite(string? suite, List<ValidationDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(suite))
        {
            diagnostics.Add(new ValidationDiagnostic(
                SnapshotValidatorDiagnostics.MissingSuite, "/suite", "Baseline is missing 'suite'."));
        }
    }

    private static void ValidateEntries(
        IReadOnlyList<BaselineEntryDefinition?>? benchmarks, List<ValidationDiagnostic> diagnostics)
    {
        // Null/missing 'benchmarks' is treated as an empty collection, not
        // a validation failure — see BaselineCompiler's matching treatment.
        // No BGV code covers the collection itself, only individual entries.
        if (benchmarks is null)
            return;

        var seenIdentities = new HashSet<BenchmarkIdentity>();

        for (var index = 0; index < benchmarks.Count; index++)
        {
            var entry = benchmarks[index];
            var path = $"/benchmarks/{index}";

            if (entry is null)
            {
                diagnostics.Add(new ValidationDiagnostic(
                    SnapshotValidatorDiagnostics.MissingIdentityFields, $"{path}/identity",
                    $"Benchmark entry at index {index} is missing required identity fields: identity."));
                diagnostics.Add(new ValidationDiagnostic(
                    SnapshotValidatorDiagnostics.MissingMetrics, $"{path}/metrics",
                    $"Benchmark entry at index {index} does not contain any metrics."));
                continue;
            }

            var identity = ValidateAndCreateIdentity(entry.Identity, index, path, diagnostics);
            ValidateMetrics(entry.Metrics, index, path, identity, diagnostics);

            if (identity is not null && !seenIdentities.Add(identity))
            {
                diagnostics.Add(new ValidationDiagnostic(
                    SnapshotValidatorDiagnostics.DuplicateBenchmarkIdentity, path,
                    $"Duplicate benchmark identity '{identity}'."));
            }
        }
    }

    private static BenchmarkIdentity? ValidateAndCreateIdentity(
        BaselineIdentityDefinition? identityDefinition, int index, string path, List<ValidationDiagnostic> diagnostics)
    {
        var missingFields = new List<string>();

        if (identityDefinition is null)
        {
            missingFields.Add("identity");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(identityDefinition.TypeName))
                missingFields.Add("typeName");
            if (string.IsNullOrWhiteSpace(identityDefinition.MethodName))
                missingFields.Add("methodName");
        }

        if (missingFields.Count > 0)
        {
            diagnostics.Add(new ValidationDiagnostic(
                SnapshotValidatorDiagnostics.MissingIdentityFields, $"{path}/identity",
                $"Benchmark entry at index {index} is missing required identity fields: {string.Join(", ", missingFields)}."));
            return null;
        }

        return new BenchmarkIdentity(
            identityDefinition!.TypeName!,
            identityDefinition.MethodName!,
            identityDefinition.Job ?? "Default",
            identityDefinition.Parameters ?? new Dictionary<string, string>());
    }

    private static void ValidateMetrics(
        IReadOnlyDictionary<string, double>? metrics, int index, string path, BenchmarkIdentity? identity,
        List<ValidationDiagnostic> diagnostics)
    {
        if (metrics is not null && metrics.Count != 0)
            return;

        var label = identity is not null ? identity.ToString() : $"index {index}";
        diagnostics.Add(new ValidationDiagnostic(
            SnapshotValidatorDiagnostics.MissingMetrics, $"{path}/metrics",
            $"Benchmark '{label}' does not contain any metrics."));
    }
}
using Bijecta.BenchmarkGate.Core.Identity;

namespace Bijecta.BenchmarkGate.Core.Baseline;

public static class BaselineCompiler
{
    /// <exception cref="ArgumentException">
    /// The document does not satisfy the structural preconditions
    /// SnapshotValidator.Validate would have required.
    /// </exception>
    public static BenchmarkBaseline CompileValidated(BaselineDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.SchemaVersion != BaselineFormat.CurrentSchemaVersion ||
            string.IsNullOrWhiteSpace(document.Suite))
        {
            throw new ArgumentException(
                "The baseline document must pass SnapshotValidator.Validate before compilation.",
                nameof(document));
        }

        // Missing/null 'benchmarks' is equivalent to an empty collection —
        // matches SnapshotValidator, which reports no diagnostic for it.
        var definitions = document.Benchmarks ?? [];
        var entries = definitions.Select(CompileEntry).ToList();

        return new BenchmarkBaseline(document.Suite, entries);
    }

    private static BaselineEntry CompileEntry(BaselineEntryDefinition? definition)
    {
        if (definition?.Identity is null ||
            string.IsNullOrWhiteSpace(definition.Identity.TypeName) ||
            string.IsNullOrWhiteSpace(definition.Identity.MethodName) ||
            definition.Metrics is null || definition.Metrics.Count == 0)
        {
            throw new ArgumentException(
                "A benchmark entry does not satisfy the structural preconditions. " +
                "The document must pass SnapshotValidator.Validate before compilation.",
                nameof(definition));
        }

        var identity = new BenchmarkIdentity(
            definition.Identity.TypeName,
            definition.Identity.MethodName,
            definition.Identity.Job ?? "Default",
            definition.Identity.Parameters ?? new Dictionary<string, string>());

        return new BaselineEntry(identity, definition.Metrics);
    }
}
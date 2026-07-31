using System.Text.Json.Serialization;

namespace Bijecta.BenchmarkGate.Core.Baseline;

/// <summary>
/// The unvalidated shape of a baseline JSON document. Nullable throughout
/// — a missing property deserializes to null rather than throwing, so
/// SnapshotValidator can report it as an ordinary finding rather than a
/// deserialization failure. See ADR-0003's fail-fast/collect boundary.
/// </summary>
public sealed record BaselineDocument(
    [property: JsonPropertyName("schemaVersion")] int? SchemaVersion,
    [property: JsonPropertyName("suite")] string? Suite,
    [property: JsonPropertyName("benchmarks")] IReadOnlyList<BaselineEntryDefinition?>? Benchmarks);

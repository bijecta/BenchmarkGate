namespace Bijecta.BenchmarkGate.Core.Baseline;

/// <summary>
/// The baseline JSON document's current schema version, shared by
/// SnapshotValidator, BaselineCompiler, and BaselineFile.WriteCandidate
/// so the version number lives in one place rather than as repeated
/// unexplained literals.
/// </summary>
public static class BaselineFormat
{
    public const int CurrentSchemaVersion = 2;
}
namespace Bijecta.BenchmarkGate.Core.Model;

/// <summary>
/// A single parsed BenchmarkDotNet report: its execution-environment metadata, if any, and its
/// observations (ADR-0006 Decision 1). <see cref="Environment"/> is <see langword="null"/> when
/// no environment document was supplied at all (e.g. a non-full BDN JSON export, or a
/// pre-v0.5.0 baseline) — distinct from a present-but-partial <see cref="BenchmarkEnvironment"/>
/// where a document existed but some dimensions were unavailable. These two states are never
/// merged.
/// </summary>
/// <param name="Environment">The execution-environment metadata captured for this report, or <see langword="null"/> if no environment document was supplied at all.</param>
/// <param name="Observations">The benchmark observations parsed from this report.</param>
public sealed record BenchmarkRun(
    BenchmarkEnvironment? Environment,
    IReadOnlyList<BenchmarkObservation> Observations);
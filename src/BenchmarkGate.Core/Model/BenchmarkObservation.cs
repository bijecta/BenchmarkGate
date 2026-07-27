using Bijecta.BenchmarkGate.Core.Identity;

namespace Bijecta.BenchmarkGate.Core.Model;

/// <summary>
/// A single benchmark's measured result, normalized away from whatever
/// tool produced it. For v0.1.0-alpha.1 this only carries the mean timing
/// in nanoseconds — allocation, stability, and environment fields are
/// deferred to v0.2 per the roadmap.
/// </summary>
public sealed record BenchmarkObservation(
    BenchmarkIdentity Identity,
    double MeanNanoseconds);

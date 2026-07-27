using Bijecta.BenchmarkGate.Core.Identity;

namespace Bijecta.BenchmarkGate.Core.Baseline;

/// <summary>
/// One approved benchmark result inside a baseline document.
/// </summary>
/// <param name="Identity">The benchmark this entry approves a baseline for.</param>
/// <param name="Metrics">
/// Metric name -> approved baseline value, in the same units and using the
/// same metric-name keys as BenchmarkObservation.Metrics
/// (BenchmarkObservation.MeanNanosecondsMetric / AllocatedBytesMetric).
/// A metric present in a current observation but absent here (e.g. an old
/// baseline captured before allocation tracking existed) is simply not
/// evaluated for that benchmark, not treated as a failure.
/// </param>
public sealed record BaselineEntry(
    BenchmarkIdentity Identity,
    IReadOnlyDictionary<string, double> Metrics);

/// <summary>
/// A committed, reviewable performance baseline: the set of benchmark
/// results a repository has explicitly approved as "acceptable performance".
/// </summary>
public sealed class BenchmarkBaseline
{
    public string Suite { get; }
    public IReadOnlyList<BaselineEntry> Benchmarks { get; }

    private readonly Dictionary<string, BaselineEntry> _byCanonicalIdentity;

    public BenchmarkBaseline(string suite, IReadOnlyList<BaselineEntry> benchmarks)
    {
        if (string.IsNullOrWhiteSpace(suite))
            throw new ArgumentException("Suite name must not be empty.", nameof(suite));

        Suite = suite;
        Benchmarks = benchmarks;

        _byCanonicalIdentity = new Dictionary<string, BaselineEntry>(StringComparer.Ordinal);
        foreach (var entry in benchmarks)
        {
            if (!_byCanonicalIdentity.TryAdd(entry.Identity.CanonicalString, entry))
            {
                throw new InvalidOperationException(
                    $"Duplicate benchmark identity in baseline: '{entry.Identity.CanonicalString}'. " +
                    "Baselines must contain at most one entry per benchmark identity.");
            }
        }
    }

    public BaselineEntry? TryFind(BenchmarkIdentity identity) =>
        _byCanonicalIdentity.GetValueOrDefault(identity.CanonicalString);
}
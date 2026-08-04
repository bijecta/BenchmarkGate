using System.Diagnostics;
using Bijecta.BenchmarkGate.Core.Baseline;
using Bijecta.BenchmarkGate.Core.Identity;
using Bijecta.BenchmarkGate.Core.Model;

namespace Bijecta.BenchmarkGate.Core.Comparison;

/// <summary>
/// Produces a <see cref="ComparisonResult"/> from a reference baseline and
/// a set of candidate observations: benchmark matching, metric matching,
/// and delta calculation happen here, and only here — see the dependency
/// direction in <c>RegressionEvaluator</c>'s remarks, which must not
/// duplicate any of this.
/// </summary>
/// <remarks>
/// This type is policy-free: no threshold, no pass/fail/stability
/// vocabulary, no <c>GatePolicy</c> dependency. It answers "what changed
/// and by how much", not "is that acceptable".
/// </remarks>
public static class BenchmarkComparisonEngine
{
    public static ComparisonResult Compare(
        BenchmarkBaseline reference,
        IReadOnlyCollection<BenchmarkObservation> candidate)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(candidate);

        var candidateByIdentity = BuildCandidateLookup(candidate);
        var matchedCandidateIdentities = new HashSet<BenchmarkIdentity>();
        var comparisons = new List<BenchmarkComparison>(reference.Benchmarks.Count);

        foreach (var entry in reference.Benchmarks)
        {
            if (candidateByIdentity.TryGetValue(entry.Identity, out var observation))
            {
                matchedCandidateIdentities.Add(entry.Identity);
                comparisons.Add(BuildComparable(entry, observation));
            }
            else
            {
                comparisons.Add(BuildRemoved(entry));
            }
        }

        foreach (var observation in candidate)
        {
            if (!matchedCandidateIdentities.Contains(observation.Identity))
            {
                comparisons.Add(BuildAdded(observation));
            }
        }

        comparisons.Sort(static (left, right) =>
            BenchmarkIdentityComparer.Instance.Compare(left.Identity, right.Identity));

        return new ComparisonResult(reference.Suite, comparisons);
    }

    /// <summary>
    /// Keys by <see cref="BenchmarkIdentity"/> itself, not its
    /// <see cref="BenchmarkIdentity.CanonicalString"/> — identity equality
    /// and hashing are <see cref="BenchmarkIdentity"/>'s own contract
    /// (<see cref="IEquatable{T}"/>), so the engine never needs to know
    /// that a canonical string exists underneath it.
    /// </summary>
    private static Dictionary<BenchmarkIdentity, BenchmarkObservation> BuildCandidateLookup(
        IReadOnlyCollection<BenchmarkObservation> candidate)
    {
        var byIdentity = new Dictionary<BenchmarkIdentity, BenchmarkObservation>();

        foreach (var observation in candidate)
        {
            if (!byIdentity.TryAdd(observation.Identity, observation))
            {
                throw new ArgumentException(
                    $"Duplicate benchmark identity in candidate observations: " +
                    $"'{observation.Identity.CanonicalString}'. Each benchmark identity must " +
                    "appear at most once in a single comparison run.",
                    nameof(candidate));
            }
        }

        return byIdentity;
    }

    private static BenchmarkComparison BuildComparable(BaselineEntry entry, BenchmarkObservation observation)
    {
        var metricNames = entry.Metrics.Keys
            .Concat(observation.Metrics.Keys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal);

        var metrics = metricNames
            .Select(metricName => CompareMetric(metricName, entry, observation))
            .ToList();

        return new BenchmarkComparison(
            entry.Identity, BenchmarkComparisonStatus.Comparable, CreateCandidateStability(observation), metrics);
    }

    /// <summary>
    /// Invariant: whenever this returns a <see cref="MetricComparisonStatus.Comparable"/>
    /// result, <see cref="MetricComparison.Direction"/> is guaranteed non-null —
    /// <see cref="DeriveDirection"/> is exhaustive over <see cref="OptimizationDirection"/>
    /// and a missing/null descriptor. A future edit that returns
    /// <see cref="MetricComparisonStatus.Comparable"/> without going through
    /// <see cref="CreateComparable"/> would break this silently, so route all
    /// Comparable construction through it.
    /// </summary>
    private static MetricComparison CompareMetric(string metricName, BaselineEntry entry, BenchmarkObservation observation)
    {
        var descriptor = MetricCatalog.TryGet(metricName, out var known) ? known : null;
        var hasReference = entry.Metrics.TryGetValue(metricName, out var referenceRawValue);
        var hasCandidate = observation.Metrics.TryGetValue(metricName, out var candidateRawValue);

        if (!hasReference)
        {
            return CreateMissingReference(metricName, descriptor, new MetricValue(candidateRawValue, descriptor?.Unit));
        }

        if (!hasCandidate)
        {
            return CreateMissingCandidate(metricName, descriptor, new MetricValue(referenceRawValue, descriptor?.Unit));
        }

        var reference = new MetricValue(referenceRawValue, descriptor?.Unit);
        var candidate = new MetricValue(candidateRawValue, descriptor?.Unit);

        if (!double.IsFinite(referenceRawValue))
        {
            return CreateInvalidReference(metricName, descriptor, reference, candidate);
        }

        if (!double.IsFinite(candidateRawValue))
        {
            return CreateInvalidCandidate(metricName, descriptor, reference, candidate);
        }

        var absoluteDelta = candidateRawValue - referenceRawValue;
        var percentDelta = ResolvePercentDelta(referenceRawValue, candidateRawValue);
        var direction = DeriveDirection(absoluteDelta, descriptor?.Direction);

        return CreateComparable(metricName, descriptor, reference, candidate, absoluteDelta, percentDelta, direction);
    }

    private static MetricComparison CreateMissingReference(string metricName, MetricDescriptor? descriptor, MetricValue candidate) =>
        new(metricName, MetricComparisonStatus.MissingReferenceMetric, descriptor,
            Reference: null, Candidate: candidate,
            AbsoluteDelta: null, PercentDelta: null, Direction: null);

    private static MetricComparison CreateMissingCandidate(string metricName, MetricDescriptor? descriptor, MetricValue reference) =>
        new(metricName, MetricComparisonStatus.MissingCandidateMetric, descriptor,
            Reference: reference, Candidate: null,
            AbsoluteDelta: null, PercentDelta: null, Direction: null);

    private static MetricComparison CreateInvalidReference(
        string metricName, MetricDescriptor? descriptor, MetricValue reference, MetricValue candidate) =>
        new(metricName, MetricComparisonStatus.InvalidReferenceValue, descriptor,
            reference, candidate,
            AbsoluteDelta: null, PercentDelta: null, Direction: null);

    private static MetricComparison CreateInvalidCandidate(
        string metricName, MetricDescriptor? descriptor, MetricValue reference, MetricValue candidate) =>
        new(metricName, MetricComparisonStatus.InvalidCandidateValue, descriptor,
            reference, candidate,
            AbsoluteDelta: null, PercentDelta: null, Direction: null);

    private static MetricComparison CreateComparable(
        string metricName, MetricDescriptor? descriptor, MetricValue reference, MetricValue candidate,
        double absoluteDelta, double? percentDelta, ChangeDirection direction) =>
        new(metricName, MetricComparisonStatus.Comparable, descriptor,
            reference, candidate, absoluteDelta, percentDelta, direction);

    /// <summary>
    /// Adapts <see cref="PercentDeltaCalculator.Calculate"/>'s result onto
    /// <see cref="MetricComparison.PercentDelta"/>'s simpler nullable-double
    /// contract, normalizing the zero-to-zero case to <c>0</c> rather than
    /// surfacing the calculator's distinct zero-status. Both inputs are
    /// already known-finite by the only caller, so
    /// <see cref="PercentDeltaStatus.InvalidReference"/>/
    /// <see cref="PercentDeltaStatus.InvalidCandidate"/> cannot occur here.
    /// </summary>
    private static double? ResolvePercentDelta(double referenceValue, double candidateValue)
    {
        var result = PercentDeltaCalculator.Calculate(referenceValue, candidateValue);

        return result.Status switch
        {
            PercentDeltaStatus.Calculated => result.Value,
            PercentDeltaStatus.ReferenceZeroAndCandidateZero => 0d,
            // ReferenceZero: relative percentage is undefined for a
            // non-zero change from a zero reference.
            _ => null,
        };
    }

    /// <summary>
    /// An unchanged value is always <see cref="ChangeDirection.Unchanged"/>,
    /// regardless of direction semantics. A changed value is classified by
    /// <paramref name="direction"/> when known; a <c>null</c> direction
    /// (unknown metric) and <see cref="OptimizationDirection.Neutral"/>
    /// both produce <see cref="ChangeDirection.Indeterminate"/>.
    /// </summary>
    private static ChangeDirection DeriveDirection(double absoluteDelta, OptimizationDirection? direction)
    {
        if (absoluteDelta == 0d)
        {
            return ChangeDirection.Unchanged;
        }

        if (direction is null)
        {
            return ChangeDirection.Indeterminate;
        }

        return direction switch
        {
            OptimizationDirection.LowerIsBetter => absoluteDelta < 0 ? ChangeDirection.Improvement : ChangeDirection.Degradation,
            OptimizationDirection.HigherIsBetter => absoluteDelta > 0 ? ChangeDirection.Improvement : ChangeDirection.Degradation,
            OptimizationDirection.Neutral => ChangeDirection.Indeterminate,
            _ => throw new UnreachableException($"Unhandled {nameof(OptimizationDirection)}: {direction}"),
        };
    }

    private static BenchmarkComparison BuildAdded(BenchmarkObservation observation)
    {
        var metrics = observation.Metrics.Keys
            .OrderBy(name => name, StringComparer.Ordinal)
            .Select(metricName =>
            {
                var descriptor = MetricCatalog.TryGet(metricName, out var known) ? known : null;
                var candidateValue = new MetricValue(observation.Metrics[metricName], descriptor?.Unit);
                return CreateMissingReference(metricName, descriptor, candidateValue);
            })
            .ToList();

        return new BenchmarkComparison(
            observation.Identity, BenchmarkComparisonStatus.Added, CreateCandidateStability(observation), metrics);
    }

    private static BenchmarkComparison BuildRemoved(BaselineEntry entry)
    {
        var metrics = entry.Metrics.Keys
            .OrderBy(name => name, StringComparer.Ordinal)
            .Select(metricName =>
            {
                var descriptor = MetricCatalog.TryGet(metricName, out var known) ? known : null;
                var referenceValue = new MetricValue(entry.Metrics[metricName], descriptor?.Unit);
                return CreateMissingCandidate(metricName, descriptor, referenceValue);
            })
            .ToList();

        return new BenchmarkComparison(entry.Identity, BenchmarkComparisonStatus.Removed, CandidateStability: null, metrics);
    }

    private static BenchmarkStabilityMeasurement CreateCandidateStability(BenchmarkObservation observation) =>
        new(observation.MeasurementCount, observation.StandardDeviationNanoseconds);
}
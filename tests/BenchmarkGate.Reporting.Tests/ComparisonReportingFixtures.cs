using Bijecta.BenchmarkGate.Core.Comparison;
using Bijecta.BenchmarkGate.Core.Identity;

namespace Bijecta.BenchmarkGate.Reporting.Tests;

/// <summary>
/// Hand-built <see cref="ComparisonResult"/> fixtures for reporter tests.
/// Built directly rather than via BenchmarkComparisonEngine, since these
/// tests exercise reporting in isolation from the engine that produces
/// real comparisons.
/// </summary>
internal static class ComparisonReportingFixtures
{
    private static BenchmarkIdentity Identity(string method) => new("Ns.Type", method, "Ci");

    /// <summary>
    /// A representative comparison: one Comparable benchmark with a known
    /// metric (normal delta) and an unknown metric (Indeterminate
    /// direction), one Comparable benchmark with a zero-reference metric
    /// (null PercentDelta), one Added benchmark, one Removed benchmark.
    /// </summary>
    public static ComparisonResult Sample() => new(
        "nightly",
        [
            new BenchmarkComparison(
                Identity("Sort"),
                BenchmarkComparisonStatus.Comparable,
                new BenchmarkStabilityMeasurement(MeasurementCount: 20, StandardDeviationNanoseconds: 5.0),
                [
                    new MetricComparison(
                        "meanNanoseconds", MetricComparisonStatus.Comparable,
                        new MetricDescriptor("meanNanoseconds", OptimizationDirection.LowerIsBetter, "ns"),
                        Reference: new MetricValue(1000d, "ns"), Candidate: new MetricValue(1100d, "ns"),
                        AbsoluteDelta: 100d, PercentDelta: 10d, Direction: ChangeDirection.Degradation),
                    new MetricComparison(
                        "gen0Collections", MetricComparisonStatus.Comparable,
                        Descriptor: null,
                        Reference: new MetricValue(4d, null), Candidate: new MetricValue(4d, null),
                        AbsoluteDelta: 0d, PercentDelta: 0d, Direction: ChangeDirection.Unchanged),
                ]),
            new BenchmarkComparison(
                Identity("Zeroed"),
                BenchmarkComparisonStatus.Comparable,
                new BenchmarkStabilityMeasurement(MeasurementCount: 20, StandardDeviationNanoseconds: 1.0),
                [
                    new MetricComparison(
                        "allocatedBytesPerOperation", MetricComparisonStatus.Comparable,
                        new MetricDescriptor("allocatedBytesPerOperation", OptimizationDirection.LowerIsBetter, "bytes"),
                        Reference: new MetricValue(0d, "bytes"), Candidate: new MetricValue(64d, "bytes"),
                        AbsoluteDelta: 64d, PercentDelta: null, Direction: ChangeDirection.Degradation),
                ]),
            new BenchmarkComparison(
                Identity("New"),
                BenchmarkComparisonStatus.Added,
                new BenchmarkStabilityMeasurement(MeasurementCount: 20, StandardDeviationNanoseconds: 2.0),
                [
                    new MetricComparison(
                        "meanNanoseconds", MetricComparisonStatus.MissingReferenceMetric,
                        new MetricDescriptor("meanNanoseconds", OptimizationDirection.LowerIsBetter, "ns"),
                        Reference: null, Candidate: new MetricValue(500d, "ns"),
                        AbsoluteDelta: null, PercentDelta: null, Direction: null),
                ]),
            new BenchmarkComparison(
                Identity("Old"),
                BenchmarkComparisonStatus.Removed,
                CandidateStability: null,
                [
                    new MetricComparison(
                        "meanNanoseconds", MetricComparisonStatus.MissingCandidateMetric,
                        new MetricDescriptor("meanNanoseconds", OptimizationDirection.LowerIsBetter, "ns"),
                        Reference: new MetricValue(800d, "ns"), Candidate: null,
                        AbsoluteDelta: null, PercentDelta: null, Direction: null),
                ]),
        ]);

    /// <summary>
    /// A single Comparable benchmark with an InvalidCandidateValue metric
    /// carrying a raw NaN value — for JSON serialization tests, since
    /// System.Text.Json throws on NaN/Infinity by default.
    /// </summary>
    public static ComparisonResult WithNonFiniteValue() => new(
        "nightly",
        [
            new BenchmarkComparison(
                Identity("Flaky"),
                BenchmarkComparisonStatus.Comparable,
                new BenchmarkStabilityMeasurement(MeasurementCount: 20, StandardDeviationNanoseconds: 1.0),
                [
                    new MetricComparison(
                        "meanNanoseconds", MetricComparisonStatus.InvalidCandidateValue,
                        new MetricDescriptor("meanNanoseconds", OptimizationDirection.LowerIsBetter, "ns"),
                        Reference: new MetricValue(1000d, "ns"), Candidate: new MetricValue(double.NaN, "ns"),
                        AbsoluteDelta: null, PercentDelta: null, Direction: null),
                ]),
        ]);

    /// <summary>A comparison with no benchmarks at all.</summary>
    public static ComparisonResult Empty() => new("nightly", []);
}
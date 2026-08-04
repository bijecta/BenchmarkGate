using Bijecta.BenchmarkGate.Core.Comparison;
using Bijecta.BenchmarkGate.Core.Identity;
using FluentAssertions;
using Xunit;

namespace Bijecta.BenchmarkGate.BenchmarkDotNet.Tests.Comparison;

public class BenchmarkComparisonModelTests
{
    private static BenchmarkIdentity Identity(string method = "Sort") =>
        new("MyBenchmarks", method, job: "Ci");

    [Fact]
    public void comparable_metric_with_known_descriptor_and_nonzero_reference_populates_all_derived_fields()
    {
        var metric = new MetricComparison(
            MetricName: "meanNanoseconds",
            Status: MetricComparisonStatus.Comparable,
            Descriptor: new MetricDescriptor("meanNanoseconds", OptimizationDirection.LowerIsBetter, "ns"),
            Reference: new MetricValue(100d, "ns"),
            Candidate: new MetricValue(110d, "ns"),
            AbsoluteDelta: 10d,
            PercentDelta: 10d,
            Direction: ChangeDirection.Degradation);

        metric.AbsoluteDelta.Should().Be(10d);
        metric.PercentDelta.Should().Be(10d);
        metric.Direction.Should().Be(ChangeDirection.Degradation);
    }

    [Fact]
    public void comparable_metric_with_zero_reference_and_nonzero_candidate_leaves_percent_delta_null()
    {
        var metric = new MetricComparison(
            MetricName: "allocatedBytesPerOperation",
            Status: MetricComparisonStatus.Comparable,
            Descriptor: new MetricDescriptor("allocatedBytesPerOperation", OptimizationDirection.LowerIsBetter, "bytes"),
            Reference: new MetricValue(0d, "bytes"),
            Candidate: new MetricValue(10d, "bytes"),
            AbsoluteDelta: 10d,
            PercentDelta: null,
            Direction: ChangeDirection.Degradation);

        metric.AbsoluteDelta.Should().Be(10d);
        metric.PercentDelta.Should().BeNull();
        metric.Direction.Should().Be(ChangeDirection.Degradation);
    }

    [Fact]
    public void comparable_metric_with_zero_reference_and_zero_candidate_reports_unchanged_and_zero_percent()
    {
        var metric = new MetricComparison(
            MetricName: "allocatedBytesPerOperation",
            Status: MetricComparisonStatus.Comparable,
            Descriptor: new MetricDescriptor("allocatedBytesPerOperation", OptimizationDirection.LowerIsBetter, "bytes"),
            Reference: new MetricValue(0d, "bytes"),
            Candidate: new MetricValue(0d, "bytes"),
            AbsoluteDelta: 0d,
            PercentDelta: 0d,
            Direction: ChangeDirection.Unchanged);

        metric.PercentDelta.Should().Be(0d);
        metric.Direction.Should().Be(ChangeDirection.Unchanged);
    }

    [Fact]
    public void comparable_metric_with_unknown_descriptor_and_equal_values_reports_unchanged()
    {
        var metric = new MetricComparison(
            MetricName: "gen0Collections",
            Status: MetricComparisonStatus.Comparable,
            Descriptor: null,
            Reference: new MetricValue(4d, "count"),
            Candidate: new MetricValue(4d, "count"),
            AbsoluteDelta: 0d,
            PercentDelta: 0d,
            Direction: ChangeDirection.Unchanged);

        metric.Descriptor.Should().BeNull();
        metric.Direction.Should().Be(ChangeDirection.Unchanged);
    }

    [Fact]
    public void comparable_metric_with_unknown_descriptor_and_changed_values_reports_indeterminate()
    {
        var metric = new MetricComparison(
            MetricName: "gen0Collections",
            Status: MetricComparisonStatus.Comparable,
            Descriptor: null,
            Reference: new MetricValue(4d, "count"),
            Candidate: new MetricValue(6d, "count"),
            AbsoluteDelta: 2d,
            PercentDelta: 50d,
            Direction: ChangeDirection.Indeterminate);

        metric.Descriptor.Should().BeNull();
        metric.Direction.Should().Be(ChangeDirection.Indeterminate);
    }

    [Theory]
    [InlineData(MetricComparisonStatus.MissingReferenceMetric)]
    [InlineData(MetricComparisonStatus.MissingCandidateMetric)]
    [InlineData(MetricComparisonStatus.UnitMismatch)]
    [InlineData(MetricComparisonStatus.InvalidReferenceValue)]
    [InlineData(MetricComparisonStatus.InvalidCandidateValue)]
    public void non_comparable_metric_status_leaves_all_derived_fields_null(MetricComparisonStatus status)
    {
        var metric = new MetricComparison(
            MetricName: "meanNanoseconds",
            Status: status,
            Descriptor: new MetricDescriptor("meanNanoseconds", OptimizationDirection.LowerIsBetter, "ns"),
            Reference: null,
            Candidate: null,
            AbsoluteDelta: null,
            PercentDelta: null,
            Direction: null);

        metric.AbsoluteDelta.Should().BeNull();
        metric.PercentDelta.Should().BeNull();
        metric.Direction.Should().BeNull();
    }

    [Fact]
    public void added_benchmark_metrics_have_null_reference_values_but_populated_candidate_values()
    {
        var metric = new MetricComparison(
            MetricName: "meanNanoseconds",
            Status: MetricComparisonStatus.MissingReferenceMetric,
            Descriptor: new MetricDescriptor("meanNanoseconds", OptimizationDirection.LowerIsBetter, "ns"),
            Reference: null,
            Candidate: new MetricValue(120d, "ns"),
            AbsoluteDelta: null,
            PercentDelta: null,
            Direction: null);

        var benchmark = new BenchmarkComparison(
            Identity(),
            BenchmarkComparisonStatus.Added,
            CandidateStability: new BenchmarkStabilityMeasurement(MeasurementCount: 15, StandardDeviationNanoseconds: 3.2),
            Metrics: [metric]);

        benchmark.Status.Should().Be(BenchmarkComparisonStatus.Added);
        benchmark.CandidateStability.Should().NotBeNull();
        benchmark.Metrics.Should().ContainSingle();
        benchmark.Metrics[0].Reference.Should().BeNull();
        benchmark.Metrics[0].Candidate.Should().NotBeNull();
    }

    [Fact]
    public void removed_benchmark_has_null_candidate_stability_and_metrics_have_null_candidate_values()
    {
        var metric = new MetricComparison(
            MetricName: "meanNanoseconds",
            Status: MetricComparisonStatus.MissingCandidateMetric,
            Descriptor: new MetricDescriptor("meanNanoseconds", OptimizationDirection.LowerIsBetter, "ns"),
            Reference: new MetricValue(95d, "ns"),
            Candidate: null,
            AbsoluteDelta: null,
            PercentDelta: null,
            Direction: null);

        var benchmark = new BenchmarkComparison(
            Identity(),
            BenchmarkComparisonStatus.Removed,
            CandidateStability: null,
            Metrics: [metric]);

        benchmark.Status.Should().Be(BenchmarkComparisonStatus.Removed);
        benchmark.CandidateStability.Should().BeNull();
        benchmark.Metrics[0].Candidate.Should().BeNull();
        benchmark.Metrics[0].Reference.Should().NotBeNull();
    }
}
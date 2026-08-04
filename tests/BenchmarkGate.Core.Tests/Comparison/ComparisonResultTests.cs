using Bijecta.BenchmarkGate.Core.Comparison;
using Bijecta.BenchmarkGate.Core.Identity;
using FluentAssertions;
using Xunit;

namespace Bijecta.BenchmarkGate.Core.Tests.Comparison;

public class ComparisonResultTests
{
    private static BenchmarkComparison Comparable(string method) =>
        new(new BenchmarkIdentity("MyBenchmarks", method, "Ci"),
            BenchmarkComparisonStatus.Comparable,
            new BenchmarkStabilityMeasurement(20, 1.5),
            Metrics: []);

    private static BenchmarkComparison Added(string method) =>
        new(new BenchmarkIdentity("MyBenchmarks", method, "Ci"),
            BenchmarkComparisonStatus.Added,
            new BenchmarkStabilityMeasurement(20, 1.5),
            Metrics: []);

    private static BenchmarkComparison Removed(string method) =>
        new(new BenchmarkIdentity("MyBenchmarks", method, "Ci"),
            BenchmarkComparisonStatus.Removed,
            null,
            Metrics: []);

    [Fact]
    public void suite_property_returns_the_constructor_value()
    {
        var result = new ComparisonResult("nightly", Benchmarks: []);

        result.Suite.Should().Be("nightly");
    }

    [Fact]
    public void counts_reflect_the_statuses_of_the_supplied_benchmarks()
    {
        var result = new ComparisonResult(
            "nightly",
            [Comparable("Sort"), Comparable("Search"), Added("NewOne"), Removed("OldOne")]);

        result.ComparableCount.Should().Be(2);
        result.AddedCount.Should().Be(1);
        result.RemovedCount.Should().Be(1);
    }

    [Fact]
    public void counts_are_zero_when_no_benchmarks_have_a_matching_status()
    {
        var result = new ComparisonResult("nightly", [Comparable("Sort")]);

        result.AddedCount.Should().Be(0);
        result.RemovedCount.Should().Be(0);
    }

    [Fact]
    public void counts_are_all_zero_for_an_empty_benchmark_list()
    {
        var result = new ComparisonResult("nightly", Benchmarks: []);

        result.ComparableCount.Should().Be(0);
        result.AddedCount.Should().Be(0);
        result.RemovedCount.Should().Be(0);
    }
}
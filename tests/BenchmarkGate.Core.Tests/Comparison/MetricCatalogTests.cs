using Bijecta.BenchmarkGate.Core.Comparison;
using Bijecta.BenchmarkGate.Core.Model;
using FluentAssertions;
using Xunit;

namespace Bijecta.BenchmarkGate.Core.Tests.Comparison;

public class MetricCatalogTests
{
    [Fact]
    public void try_get_for_mean_nanoseconds_returns_lower_is_better_descriptor()
    {
        var found = MetricCatalog.TryGet(BenchmarkObservation.MeanNanosecondsMetric, out var descriptor);

        found.Should().BeTrue();
        descriptor.Should().NotBeNull();
        descriptor!.Name.Should().Be(BenchmarkObservation.MeanNanosecondsMetric);
        descriptor.Direction.Should().Be(OptimizationDirection.LowerIsBetter);
        descriptor.Unit.Should().Be("ns");
    }

    [Fact]
    public void try_get_for_allocated_bytes_returns_lower_is_better_descriptor()
    {
        var found = MetricCatalog.TryGet(BenchmarkObservation.AllocatedBytesMetric, out var descriptor);

        found.Should().BeTrue();
        descriptor.Should().NotBeNull();
        descriptor!.Name.Should().Be(BenchmarkObservation.AllocatedBytesMetric);
        descriptor.Direction.Should().Be(OptimizationDirection.LowerIsBetter);
        descriptor.Unit.Should().Be("bytes");
    }

    [Theory]
    [InlineData("gen0Collections")]
    [InlineData("customThroughputMetric")]
    [InlineData("")]
    public void try_get_for_unknown_metric_name_returns_false_with_null_descriptor(string metricName)
    {
        var found = MetricCatalog.TryGet(metricName, out var descriptor);

        found.Should().BeFalse();
        descriptor.Should().BeNull();
    }

    [Fact]
    public void try_get_is_case_sensitive_and_does_not_match_on_different_casing()
    {
        // Ordinal comparison: metric name keys are exact adapter-reported
        // strings, not user input, so no case-insensitive matching.
        var found = MetricCatalog.TryGet("MeanNanoseconds", out var descriptor);

        found.Should().BeFalse();
        descriptor.Should().BeNull();
    }

    [Fact]
    public void try_get_when_metric_name_is_null_throws_argument_null_exception()
    {
        var act = () => MetricCatalog.TryGet(null!, out _);

        act.Should().Throw<ArgumentNullException>();
    }
}
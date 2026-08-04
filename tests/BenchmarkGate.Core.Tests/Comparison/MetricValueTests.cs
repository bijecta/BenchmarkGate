using Bijecta.BenchmarkGate.Core.Comparison;
using FluentAssertions;
using Xunit;

namespace Bijecta.BenchmarkGate.Core.Tests.Comparison;

public class MetricValueTests
{
    [Fact]
    public void construction_with_a_known_unit_populates_value_and_unit()
    {
        var value = new MetricValue(100d, "ns");

        value.Value.Should().Be(100d);
        value.Unit.Should().Be("ns");
    }

    [Fact]
    public void construction_with_a_null_unit_represents_an_unknown_unit()
    {
        var value = new MetricValue(4d, null);

        value.Value.Should().Be(4d);
        value.Unit.Should().BeNull();
    }

    [Fact]
    public void two_metric_values_with_the_same_value_and_unit_are_equal()
    {
        var first = new MetricValue(100d, "ns");
        var second = new MetricValue(100d, "ns");

        first.Should().Be(second);
    }

    [Fact]
    public void two_metric_values_with_different_units_are_not_equal()
    {
        var nanoseconds = new MetricValue(100d, "ns");
        var bytes = new MetricValue(100d, "bytes");

        nanoseconds.Should().NotBe(bytes);
    }

    [Fact]
    public void a_metric_value_with_a_null_unit_is_not_equal_to_one_with_a_known_unit()
    {
        var unknownUnit = new MetricValue(4d, null);
        var knownUnit = new MetricValue(4d, "count");

        unknownUnit.Should().NotBe(knownUnit);
    }

    [Fact]
    public void two_metric_values_with_null_units_and_equal_values_are_equal()
    {
        var first = new MetricValue(4d, null);
        var second = new MetricValue(4d, null);

        first.Should().Be(second);
    }

    [Fact]
    public void preserves_a_non_finite_value_without_alteration()
    {
        var value = new MetricValue(double.NaN, "ns");

        double.IsNaN(value.Value).Should().BeTrue();
    }
}
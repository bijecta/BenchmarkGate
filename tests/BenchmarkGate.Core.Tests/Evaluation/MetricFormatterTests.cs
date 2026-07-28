using Bijecta.BenchmarkGate.Core.Evaluation;
using FluentAssertions;
using Xunit;

namespace Bijecta.BenchmarkGate.Core.Tests.Evaluation;

public class MetricFormatterTests
{
    [Theory]
    [InlineData(999, "999.000 ns")]
    [InlineData(1000, "1.000 \u00b5s")]
    [InlineData(999_999, "999.999 \u00b5s")]
    [InlineData(1_000_000, "1.000 ms")]
    public void NanosecondsFormatter_switches_unit_at_thousand_boundaries(double value, string expected)
    {
        new NanosecondsFormatter().Format(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(1023, "1023 B")]
    [InlineData(1024, "1.000 KB")]
    [InlineData(1_048_575, "1023.999 KB")]
    [InlineData(1_048_576, "1.000 MB")]
    public void BytesFormatter_uses_binary_1024_scaling(double value, string expected)
    {
        new BytesFormatter().Format(value).Should().Be(expected);
    }

    [Fact]
    public void CountFormatter_prints_a_bare_unitless_number()
    {
        new CountFormatter().Format(42).Should().Be("42");
    }

    [Fact]
    public void MetricFormatters_resolves_mean_nanoseconds_to_NanosecondsFormatter()
    {
        var formatter = MetricFormatters.For(Bijecta.BenchmarkGate.Core.Model.BenchmarkObservation.MeanNanosecondsMetric);

        formatter.Should().BeOfType<NanosecondsFormatter>();
    }

    [Fact]
    public void MetricFormatters_resolves_allocated_bytes_to_BytesFormatter()
    {
        var formatter = MetricFormatters.For(Bijecta.BenchmarkGate.Core.Model.BenchmarkObservation.AllocatedBytesMetric);

        formatter.Should().BeOfType<BytesFormatter>();
    }

    [Fact]
    public void MetricFormatters_falls_back_to_CountFormatter_for_unregistered_metric_names()
    {
        var formatter = MetricFormatters.For("someFutureMetric");

        formatter.Should().BeOfType<CountFormatter>();
    }
}
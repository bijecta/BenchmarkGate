using System.Text.Json;
using Bijecta.BenchmarkGate.Reporting;
using FluentAssertions;
using Xunit;

namespace Bijecta.BenchmarkGate.Reporting.Tests;

public class JsonComparisonReporterTests
{
    private static JsonDocument RenderAsDocument(Core.Comparison.ComparisonResult comparison)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        try
        {
            JsonComparisonReporter.Write(path, comparison);
            return JsonDocument.Parse(File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void schema_version_is_one_and_independent_of_the_decision_reports_schema_version()
    {
        using var document = RenderAsDocument(ComparisonReportingFixtures.Sample());

        document.RootElement.GetProperty("schemaVersion").GetInt32().Should().Be(1);
    }

    [Fact]
    public void full_numeric_precision_is_preserved_without_rounding()
    {
        var comparison = new Core.Comparison.ComparisonResult(
            "nightly",
            [
                new Core.Comparison.BenchmarkComparison(
                    new Core.Identity.BenchmarkIdentity("Ns.Type", "Precise", "Ci"),
                    Core.Comparison.BenchmarkComparisonStatus.Comparable,
                    new Core.Comparison.BenchmarkStabilityMeasurement(20, 1.0),
                    [
                        new Core.Comparison.MetricComparison(
                            "meanNanoseconds", Core.Comparison.MetricComparisonStatus.Comparable,
                            new Core.Comparison.MetricDescriptor(
                                "meanNanoseconds", Core.Comparison.OptimizationDirection.LowerIsBetter, "ns"),
                            Reference: new Core.Comparison.MetricValue(1000.123456789012, "ns"),
                            Candidate: new Core.Comparison.MetricValue(1000.987654321098, "ns"),
                            AbsoluteDelta: 0.864197532086, PercentDelta: 0.0864066d,
                            Direction: Core.Comparison.ChangeDirection.Degradation),
                    ]),
            ]);

        using var document = RenderAsDocument(comparison);

        var reference = document.RootElement
            .GetProperty("benchmarks")[0].GetProperty("metrics")[0].GetProperty("reference").GetProperty("value");
        reference.GetDouble().Should().Be(1000.123456789012);
    }

    [Fact]
    public void non_finite_values_serialize_without_throwing()
    {
        var act = () => RenderAsDocument(ComparisonReportingFixtures.WithNonFiniteValue());

        act.Should().NotThrow();
    }

    [Fact]
    public void non_finite_candidate_value_serializes_as_the_exact_named_literal_string()
    {
        using var document = RenderAsDocument(ComparisonReportingFixtures.WithNonFiniteValue());

        var metric = document.RootElement.GetProperty("benchmarks")[0].GetProperty("metrics")[0];
        var candidateValueElement = metric.GetProperty("candidate").GetProperty("value");

        candidateValueElement.ValueKind.Should().Be(JsonValueKind.String);
        candidateValueElement.GetString().Should().Be("NaN");
    }

    [Fact]
    public void non_finite_metric_still_identifies_which_side_is_invalid_via_status()
    {
        using var document = RenderAsDocument(ComparisonReportingFixtures.WithNonFiniteValue());

        var metric = document.RootElement.GetProperty("benchmarks")[0].GetProperty("metrics")[0];

        metric.GetProperty("status").GetString().Should().Be("InvalidCandidateValue");
    }

    [Fact]
    public void positive_infinity_serializes_as_the_exact_named_literal_string()
    {
        var comparison = new Core.Comparison.ComparisonResult(
            "nightly",
            [
                new Core.Comparison.BenchmarkComparison(
                    new Core.Identity.BenchmarkIdentity("Ns.Type", "Infinite", "Ci"),
                    Core.Comparison.BenchmarkComparisonStatus.Comparable,
                    new Core.Comparison.BenchmarkStabilityMeasurement(20, 1.0),
                    [
                        new Core.Comparison.MetricComparison(
                            "meanNanoseconds", Core.Comparison.MetricComparisonStatus.InvalidCandidateValue,
                            new Core.Comparison.MetricDescriptor(
                                "meanNanoseconds", Core.Comparison.OptimizationDirection.LowerIsBetter, "ns"),
                            Reference: new Core.Comparison.MetricValue(1000d, "ns"),
                            Candidate: new Core.Comparison.MetricValue(double.PositiveInfinity, "ns"),
                            AbsoluteDelta: null, PercentDelta: null, Direction: null),
                    ]),
            ]);

        using var document = RenderAsDocument(comparison);

        var candidateValueElement = document.RootElement
            .GetProperty("benchmarks")[0].GetProperty("metrics")[0].GetProperty("candidate").GetProperty("value");
        candidateValueElement.GetString().Should().Be("Infinity");
    }

    [Fact]
    public void reference_and_candidate_are_nested_objects_with_value_and_unit()
    {
        using var document = RenderAsDocument(ComparisonReportingFixtures.Sample());

        var reference = document.RootElement
            .GetProperty("benchmarks")[0].GetProperty("metrics")[0].GetProperty("reference");
        reference.GetProperty("value").GetDouble().Should().Be(1000d);
        reference.GetProperty("unit").GetString().Should().Be("ns");
    }

    [Fact]
    public void unknown_metric_has_null_unit_not_an_empty_string()
    {
        using var document = RenderAsDocument(ComparisonReportingFixtures.Sample());

        var metrics = document.RootElement.GetProperty("benchmarks")[0].GetProperty("metrics");
        var unknownMetric = metrics.EnumerateArray().Single(m => m.GetProperty("metricName").GetString() == "gen0Collections");

        unknownMetric.GetProperty("reference").TryGetProperty("unit", out _).Should().BeFalse();
    }

    [Fact]
    public void removed_benchmark_omits_null_candidate_stability_rather_than_writing_null()
    {
        using var document = RenderAsDocument(ComparisonReportingFixtures.Sample());

        var removedBenchmark = document.RootElement.GetProperty("benchmarks")
            .EnumerateArray().Single(b => b.GetProperty("status").GetString() == "Removed");

        removedBenchmark.TryGetProperty("candidateStability", out _).Should().BeFalse();
    }

    [Fact]
    public void write_throws_when_path_is_null_or_whitespace()
    {
        var act = () => JsonComparisonReporter.Write("  ", ComparisonReportingFixtures.Sample());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void write_throws_when_comparison_is_null()
    {
        var act = () => JsonComparisonReporter.Write(Path.GetTempFileName(), null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void non_finite_standard_deviation_serializes_as_the_exact_named_literal_string()
    {
        var comparison = new Core.Comparison.ComparisonResult(
            "nightly",
            [
                new Core.Comparison.BenchmarkComparison(
                    new Core.Identity.BenchmarkIdentity("Ns.Type", "NoisyStddev", "Ci"),
                    Core.Comparison.BenchmarkComparisonStatus.Comparable,
                    new Core.Comparison.BenchmarkStabilityMeasurement(20, double.NaN),
                    []),
            ]);

        using var document = RenderAsDocument(comparison);

        var stddevElement = document.RootElement
            .GetProperty("benchmarks")[0].GetProperty("candidateStability").GetProperty("standardDeviationNanoseconds");
        stddevElement.ValueKind.Should().Be(JsonValueKind.String);
        stddevElement.GetString().Should().Be("NaN");
    }

    [Fact]
    public void benchmarks_are_serialized_in_the_order_supplied_not_re_sorted()
    {
        // Deliberately non-alphabetical order (Zoo before Alpha) — proves
        // the reporter preserves ComparisonResult.Benchmarks' given order
        // rather than re-sorting by CanonicalString.
        var comparison = new Core.Comparison.ComparisonResult(
            "nightly",
            [
                new Core.Comparison.BenchmarkComparison(
                    new Core.Identity.BenchmarkIdentity("Ns.Type", "Zoo", "Ci"),
                    Core.Comparison.BenchmarkComparisonStatus.Comparable,
                    new Core.Comparison.BenchmarkStabilityMeasurement(20, 1.0), []),
                new Core.Comparison.BenchmarkComparison(
                    new Core.Identity.BenchmarkIdentity("Ns.Type", "Alpha", "Ci"),
                    Core.Comparison.BenchmarkComparisonStatus.Comparable,
                    new Core.Comparison.BenchmarkStabilityMeasurement(20, 1.0), []),
            ]);

        using var document = RenderAsDocument(comparison);

        var identities = document.RootElement.GetProperty("benchmarks")
            .EnumerateArray().Select(b => b.GetProperty("identity").GetString()).ToArray();
        identities.Should().Equal("Ns.Type.Zoo|job=Ci", "Ns.Type.Alpha|job=Ci");
    }

    [Fact]
    public void a_metric_with_no_computed_delta_omits_those_fields_rather_than_writing_null()
    {
        using var document = RenderAsDocument(ComparisonReportingFixtures.Sample());

        var addedBenchmarkMetric = document.RootElement.GetProperty("benchmarks")
            .EnumerateArray().Single(b => b.GetProperty("status").GetString() == "Added")
            .GetProperty("metrics")[0];

        addedBenchmarkMetric.TryGetProperty("absoluteDelta", out _).Should().BeFalse();
        addedBenchmarkMetric.TryGetProperty("percentDelta", out _).Should().BeFalse();
        addedBenchmarkMetric.TryGetProperty("direction", out _).Should().BeFalse();
    }
}
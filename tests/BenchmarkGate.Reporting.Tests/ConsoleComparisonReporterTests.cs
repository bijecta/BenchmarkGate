using Bijecta.BenchmarkGate.Reporting;
using FluentAssertions;
using Xunit;

namespace Bijecta.BenchmarkGate.Reporting.Tests;

public class ConsoleComparisonReporterTests
{
    private static string Render(Core.Comparison.ComparisonResult comparison)
    {
        using var writer = new StringWriter();
        ConsoleComparisonReporter.Write(writer, comparison);
        return writer.ToString();
    }

    [Fact]
    public void output_includes_suite_name_and_comparable_added_removed_counts()
    {
        var output = Render(ComparisonReportingFixtures.Sample());

        output.Should().Contain("Suite: nightly");
        output.Should().Contain("Comparable: 2");
        output.Should().Contain("Added: 1");
        output.Should().Contain("Removed: 1");
    }

    [Fact]
    public void output_lists_a_comparable_metric_row_with_its_direction()
    {
        var output = Render(ComparisonReportingFixtures.Sample());

        output.Should().Contain("meanNanoseconds");
        output.Should().Contain(nameof(Core.Comparison.ChangeDirection.Degradation));
    }

    [Fact]
    public void output_lists_an_unknown_metric_with_indeterminate_or_unchanged_direction_not_dropped()
    {
        var output = Render(ComparisonReportingFixtures.Sample());

        output.Should().Contain("gen0Collections");
        output.Should().Contain(nameof(Core.Comparison.ChangeDirection.Unchanged));
    }

    [Fact]
    public void output_shows_na_for_a_zero_reference_metrics_percent_delta()
    {
        var output = Render(ComparisonReportingFixtures.Sample());

        output.Should().Contain("n/a");
    }

    [Fact]
    public void output_lists_added_benchmarks_by_name_in_their_own_section()
    {
        var output = Render(ComparisonReportingFixtures.Sample());

        output.Should().Contain("Added benchmarks:");
        output.Should().Contain("Ns.Type.New");
    }

    [Fact]
    public void output_lists_removed_benchmarks_by_name_in_their_own_section()
    {
        var output = Render(ComparisonReportingFixtures.Sample());

        output.Should().Contain("Removed benchmarks:");
        output.Should().Contain("Ns.Type.Old");
    }

    [Fact]
    public void empty_comparison_reports_no_benchmarks_compared()
    {
        var output = Render(ComparisonReportingFixtures.Empty());

        output.Should().Contain("No benchmarks compared.");
    }

    [Fact]
    public void absolute_delta_is_shown_even_when_percent_delta_is_unavailable()
    {
        // Zero-reference metric: PercentDelta is null ("n/a") but
        // AbsoluteDelta (64) is still meaningful and must not be hidden.
        var output = Render(ComparisonReportingFixtures.Sample());

        output.Should().Contain("+64");
    }

    [Fact]
    public void a_comparable_benchmark_with_no_metrics_shows_no_metrics_not_a_dash()
    {
        var comparison = new Core.Comparison.ComparisonResult(
            "nightly",
            [
                new Core.Comparison.BenchmarkComparison(
                    new Core.Identity.BenchmarkIdentity("Ns.Type", "Empty", "Ci"),
                    Core.Comparison.BenchmarkComparisonStatus.Comparable,
                    new Core.Comparison.BenchmarkStabilityMeasurement(20, 1.0),
                    []),
            ]);

        var output = Render(comparison);

        output.Should().Contain("No metrics");
    }

    [Fact]
    public void write_throws_when_output_is_null()
    {
        var act = () => ConsoleComparisonReporter.Write(null!, ComparisonReportingFixtures.Sample());

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void write_throws_when_comparison_is_null()
    {
        using var writer = new StringWriter();
        var act = () => ConsoleComparisonReporter.Write(writer, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void benchmarks_are_rendered_in_the_order_supplied_not_re_sorted()
    {
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

        var output = Render(comparison);

        output.IndexOf("Ns.Type.Zoo", StringComparison.Ordinal)
            .Should().BeLessThan(output.IndexOf("Ns.Type.Alpha", StringComparison.Ordinal));
    }

    [Fact]
    public void console_output_matches_the_verified_golden_capture()
    {
        // Captured from a real local run against ComparisonReportingFixtures.Sample()
        // and verified by hand. Line endings normalized on both sides
        // (TextWriter.WriteLine emits the OS's Environment.NewLine, which
        // differs between the machine this was captured on and CI) —
        // spacing/alignment is NOT normalized, since alignment is the
        // behavior this test exists to verify.
        const string golden = "Suite: nightly  Comparable: 2  Added: 1  Removed: 1\n\nBenchmark                                Metric               Reference    Candidate    Abs Delta      % Delta    Direction      Status\nNs.Type.Sort|job=Ci                      meanNanoseconds      1.000 µs     1.100 µs     +100.000 ns    +10.00%    Degradation    Comparable\n                                         gen0Collections      4            4            0              +0.00%     Unchanged      Comparable\nNs.Type.Zeroed|job=Ci                    allocatedBytesPerOp… 0 B          64 B         +64 B          n/a        Degradation    Comparable\n\nAdded benchmarks:\n  Ns.Type.New|job=Ci\n\nRemoved benchmarks:\n  Ns.Type.Old|job=Ci\n\n";

        var output = Render(ComparisonReportingFixtures.Sample());

        output.ReplaceLineEndings("\n").Should().Be(golden.ReplaceLineEndings("\n"));
    }
}
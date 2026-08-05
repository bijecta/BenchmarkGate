using Bijecta.BenchmarkGate.Reporting;
using FluentAssertions;
using Xunit;

namespace Bijecta.BenchmarkGate.Reporting.Tests;

public class MarkdownComparisonReporterTests
{
    private static string Render(Core.Comparison.ComparisonResult comparison)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.md");
        try
        {
            MarkdownComparisonReporter.Write(path, comparison);
            return File.ReadAllText(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void output_has_a_heading_with_the_suite_name()
    {
        var output = Render(ComparisonReportingFixtures.Sample());

        output.Should().Contain("# Benchmark Compare — nightly");
    }

    [Fact]
    public void output_includes_a_comparable_added_removed_counts_table()
    {
        var output = Render(ComparisonReportingFixtures.Sample());

        output.Should().Contain("Comparable");
        output.Should().Contain("Added");
        output.Should().Contain("Removed");
    }

    [Fact]
    public void output_lists_metric_rows_with_direction_and_status_columns()
    {
        var output = Render(ComparisonReportingFixtures.Sample());

        output.Should().Contain("meanNanoseconds");
        output.Should().Contain(nameof(Core.Comparison.ChangeDirection.Degradation));
        output.Should().Contain(nameof(Core.Comparison.MetricComparisonStatus.Comparable));
    }

    [Fact]
    public void output_has_an_added_benchmarks_section()
    {
        var output = Render(ComparisonReportingFixtures.Sample());

        output.Should().Contain("Added benchmarks");
        output.Should().Contain("Ns.Type.New");
    }

    [Fact]
    public void output_has_a_removed_benchmarks_section()
    {
        var output = Render(ComparisonReportingFixtures.Sample());

        output.Should().Contain("Removed benchmarks");
        output.Should().Contain("Ns.Type.Old");
    }

    [Fact]
    public void zero_reference_metric_row_shows_na_delta()
    {
        var output = Render(ComparisonReportingFixtures.Sample());

        output.Should().Contain("n/a");
    }

    [Fact]
    public void absolute_delta_is_shown_even_when_percent_delta_is_unavailable()
    {
        var output = Render(ComparisonReportingFixtures.Sample());

        output.Should().Contain("+64");
    }

    [Fact]
    public void write_throws_when_path_is_null_or_whitespace()
    {
        var act = () => MarkdownComparisonReporter.Write(" ", ComparisonReportingFixtures.Sample());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void write_throws_when_comparison_is_null()
    {
        var act = () => MarkdownComparisonReporter.Write(Path.GetTempFileName(), null!);

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
    public void a_pipe_character_in_the_identity_is_escaped_in_the_table_cell()
    {
        // Every canonical identity legitimately contains '|' as its own
        // field separator (e.g. "Ns.Type.Sort|job=Ci") — this is not a
        // contrived case, every row in the Sample fixture already
        // exercises it. MarkdownBuilder.Table escapes '|' to '\|'.
        var output = Render(ComparisonReportingFixtures.Sample());

        output.Should().Contain("Ns.Type.Sort\\|job=Ci");
    }

    [Fact]
    public void a_backtick_in_an_identity_produces_a_safely_delimited_code_span()
    {
        // .NET reflection names generic types with a literal backtick
        // (e.g. "List`1"), so BenchmarkIdentity.TypeName can contain one —
        // a naive `{identity}` wrap would prematurely close the span.
        var comparison = new Core.Comparison.ComparisonResult(
            "nightly",
            [
                new Core.Comparison.BenchmarkComparison(
                    new Core.Identity.BenchmarkIdentity("Ns.List`1", "Sort", "Ci"),
                    Core.Comparison.BenchmarkComparisonStatus.Added,
                    new Core.Comparison.BenchmarkStabilityMeasurement(20, 1.0), []),
            ]);

        var output = Render(comparison);

        output.Should().Contain("``Ns.List`1.Sort|job=Ci``");
    }

    [Fact]
    public void markdown_output_matches_the_verified_golden_capture()
    {
        // Captured from a real local run against ComparisonReportingFixtures.Sample()
        // and verified by hand. Line endings normalized on both sides for
        // the same reason as the console reporter's equivalent test.
        const string golden = "# Benchmark Compare — nightly\n\n| Comparable | Added | Removed |\n|---|---|---|\n| 2 | 1 | 1 |\n\n| Benchmark | Metric | Reference | Candidate | Absolute delta | Percent delta | Direction | Status |\n|---|---|---|---|---|---|---|---|\n| Ns.Type.Sort\\|job=Ci | meanNanoseconds | 1.000 µs | 1.100 µs | +100.000 ns | +10.00% | Degradation | Comparable |\n| Ns.Type.Sort\\|job=Ci | gen0Collections | 4 | 4 | 0 | +0.00% | Unchanged | Comparable |\n| Ns.Type.Zeroed\\|job=Ci | allocatedBytesPerOperation | 0 B | 64 B | +64 B | n/a | Degradation | Comparable |\n\n## Added benchmarks\n\n- `Ns.Type.New|job=Ci`\n## Removed benchmarks\n\n- `Ns.Type.Old|job=Ci`\n";

        var output = Render(ComparisonReportingFixtures.Sample());

        output.ReplaceLineEndings("\n").Should().Be(golden.ReplaceLineEndings("\n"));
    }
}
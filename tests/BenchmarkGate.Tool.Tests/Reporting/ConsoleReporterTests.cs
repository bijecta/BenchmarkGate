using Bijecta.BenchmarkGate.Core.Evaluation;
using Bijecta.BenchmarkGate.Core.Identity;
using Bijecta.BenchmarkGate.Tool.Reporting;
using FluentAssertions;
using Xunit;

namespace Bijecta.BenchmarkGate.Tool.Tests.Reporting;

public class ConsoleReporterTests
{
    private static BenchmarkIdentity Id(string method = "Method") =>
        new("Ns.Type", method, "Default");

    private static MetricDecision Metric(string name, BenchmarkGateStatus status) =>
        new(name, status, BaselineValue: 1000, CurrentValue: 1100, AbsoluteDelta: 100, RelativeDeltaPercent: 10, "explanation");

    private static string Render(SuiteDecision decision)
    {
        var writer = new StringWriter();
        ConsoleReporter.Write(writer, decision);
        return writer.ToString();
    }

    [Fact]
    public void Empty_suite_prints_a_no_benchmarks_message()
    {
        var output = Render(new SuiteDecision([]));

        output.Should().Contain("No benchmarks evaluated.");
    }

    [Fact]
    public void One_line_per_benchmark_metric_pair()
    {
        var decision = new SuiteDecision([
            new BenchmarkDecision(Id(), BenchmarkGateStatus.Regressed,
                [
                    Metric("meanNanoseconds", BenchmarkGateStatus.Regressed),
                    Metric("allocatedBytesPerOperation", BenchmarkGateStatus.Passed),
                ],
                "aggregate")
        ]);

        var output = Render(decision);

        output.Should().Contain("meanNanoseconds");
        output.Should().Contain("allocatedBytesPerOperation");
        output.Should().Contain("REGRESSED");
        output.Should().Contain("PASSED");
    }

    [Fact]
    public void Benchmark_with_no_metrics_still_gets_one_line()
    {
        var decision = new SuiteDecision([
            new BenchmarkDecision(Id(), BenchmarkGateStatus.Missing, [], "no current observation")
        ]);

        var output = Render(decision);

        output.Should().Contain(Id().CanonicalString);
        output.Should().Contain("MISSING");
    }

    [Fact]
    public void Summary_line_reports_all_status_counts()
    {
        var decision = new SuiteDecision([
            new BenchmarkDecision(Id("A"), BenchmarkGateStatus.Improved, [Metric("meanNanoseconds", BenchmarkGateStatus.Improved)], "e"),
            new BenchmarkDecision(Id("B"), BenchmarkGateStatus.Passed, [Metric("meanNanoseconds", BenchmarkGateStatus.Passed)], "e"),
            new BenchmarkDecision(Id("C"), BenchmarkGateStatus.Warning, [Metric("meanNanoseconds", BenchmarkGateStatus.Warning)], "e"),
            new BenchmarkDecision(Id("D"), BenchmarkGateStatus.Regressed, [Metric("meanNanoseconds", BenchmarkGateStatus.Regressed)], "e"),
            new BenchmarkDecision(Id("E"), BenchmarkGateStatus.Missing, [], "e"),
            new BenchmarkDecision(Id("F"), BenchmarkGateStatus.New, [], "e"),
            new BenchmarkDecision(Id("G"), BenchmarkGateStatus.Unstable, [], "e"),
        ]);

        var output = Render(decision);

        output.Should().Contain("Total: 7");
        output.Should().Contain("Improved: 1");
        output.Should().Contain("Passed: 1");
        output.Should().Contain("Warning: 1");
        output.Should().Contain("Regressed: 1");
        output.Should().Contain("Missing: 1");
        output.Should().Contain("New: 1");
        output.Should().Contain("Unstable: 1");
    }

    [Fact]
    public void Details_section_lists_regressed_missing_unstable_and_warning_benchmarks()
    {
        var decision = new SuiteDecision([
            new BenchmarkDecision(Id("A"), BenchmarkGateStatus.Regressed, [Metric("meanNanoseconds", BenchmarkGateStatus.Regressed)], "regressed explanation"),
            new BenchmarkDecision(Id("B"), BenchmarkGateStatus.Passed, [Metric("meanNanoseconds", BenchmarkGateStatus.Passed)], "passed explanation"),
        ]);

        var output = Render(decision);

        output.Should().Contain("Details:");
        output.Should().Contain("regressed explanation");
        output.Should().NotContain("passed explanation");
    }

    [Fact]
    public void No_details_section_when_suite_fully_passes()
    {
        var decision = new SuiteDecision([
            new BenchmarkDecision(Id(), BenchmarkGateStatus.Passed, [Metric("meanNanoseconds", BenchmarkGateStatus.Passed)], "e")
        ]);

        var output = Render(decision);

        output.Should().NotContain("Details:");
    }

    [Fact]
    public void Long_benchmark_identity_is_truncated_with_an_ellipsis()
    {
        var longMethodName = new string('X', 100);
        var decision = new SuiteDecision([
            new BenchmarkDecision(Id(longMethodName), BenchmarkGateStatus.Passed, [Metric("meanNanoseconds", BenchmarkGateStatus.Passed)], "e")
        ]);

        var output = Render(decision);

        output.Should().Contain("\u2026");
        output.Should().NotContain(longMethodName);
    }

    [Fact]
    public void Rows_are_ordered_by_canonical_identity()
    {
        var decision = new SuiteDecision([
            new BenchmarkDecision(Id("Z"), BenchmarkGateStatus.Passed, [Metric("meanNanoseconds", BenchmarkGateStatus.Passed)], "e"),
            new BenchmarkDecision(Id("A"), BenchmarkGateStatus.Passed, [Metric("meanNanoseconds", BenchmarkGateStatus.Passed)], "e"),
        ]);

        var output = Render(decision);

        var indexA = output.IndexOf("Ns.Type.A", StringComparison.Ordinal);
        var indexZ = output.IndexOf("Ns.Type.Z", StringComparison.Ordinal);

        indexA.Should().BeLessThan(indexZ);
    }
}
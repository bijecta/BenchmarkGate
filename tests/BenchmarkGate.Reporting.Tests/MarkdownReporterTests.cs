using Bijecta.BenchmarkGate.Core.Evaluation;
using Bijecta.BenchmarkGate.Core.Identity;
using FluentAssertions;

namespace Bijecta.BenchmarkGate.Reporting.Tests;

public sealed class MarkdownReporterTests : IDisposable
{
    private readonly string _tempDirectory =
        Path.Combine(Path.GetTempPath(), $"markdown-reporter-tests-{Guid.NewGuid():N}");

    public MarkdownReporterTests()
    {
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private string PathIn(string fileName) => Path.Combine(_tempDirectory, fileName);

    private static BenchmarkIdentity Id(string method = "Method") =>
        new("Ns.Type", method, "Default");

    private static MetricDecision Metric(string name, BenchmarkGateStatus status) =>
        new(name, status, BaselineValue: 1000, CurrentValue: 1100, AbsoluteDelta: 100, RelativeDeltaPercent: 10, "explanation");

    [Fact]
    public void Overall_heading_reflects_regressed_when_any_benchmark_regressed()
    {
        var target = PathIn("report.md");
        var decision = new SuiteDecision([
            new BenchmarkDecision(Id(), BenchmarkGateStatus.Regressed, [Metric("meanNanoseconds", BenchmarkGateStatus.Regressed)], "e")
        ]);

        MarkdownReporter.Write(target, decision, "suite");

        File.ReadAllText(target).Should().Contain("Regressed");
    }

    [Fact]
    public void Overall_heading_reflects_passed_when_all_benchmarks_pass()
    {
        var target = PathIn("report.md");
        var decision = new SuiteDecision([
            new BenchmarkDecision(Id(), BenchmarkGateStatus.Passed, [Metric("meanNanoseconds", BenchmarkGateStatus.Passed)], "e")
        ]);

        MarkdownReporter.Write(target, decision, "suite");

        File.ReadAllText(target).Should().Contain("Passed");
    }

    [Fact]
    public void One_row_per_benchmark_metric_pair()
    {
        var target = PathIn("report.md");
        var decision = new SuiteDecision([
            new BenchmarkDecision(Id(), BenchmarkGateStatus.Regressed,
                [
                    Metric("meanNanoseconds", BenchmarkGateStatus.Regressed),
                    Metric("allocatedBytesPerOperation", BenchmarkGateStatus.Passed),
                ],
                "aggregate")
        ]);

        MarkdownReporter.Write(target, decision, "suite");

        var content = File.ReadAllText(target);
        content.Should().Contain("meanNanoseconds");
        content.Should().Contain("allocatedBytesPerOperation");
    }

    [Fact]
    public void Benchmark_with_no_metrics_still_gets_a_single_row()
    {
        var target = PathIn("report.md");
        var decision = new SuiteDecision([
            new BenchmarkDecision(Id(), BenchmarkGateStatus.Missing, [], "no current observation")
        ]);

        MarkdownReporter.Write(target, decision, "suite");

        var content = File.ReadAllText(target);
        content.Should().Contain(Id().CanonicalString);
        content.Should().Contain("Missing");
    }

    [Fact]
    public void Failures_section_lists_regressed_missing_unstable_and_warning_benchmarks()
    {
        var target = PathIn("report.md");
        var decision = new SuiteDecision([
            new BenchmarkDecision(Id("A"), BenchmarkGateStatus.Regressed, [Metric("meanNanoseconds", BenchmarkGateStatus.Regressed)], "regressed explanation"),
            new BenchmarkDecision(Id("B"), BenchmarkGateStatus.Passed, [Metric("meanNanoseconds", BenchmarkGateStatus.Passed)], "passed explanation"),
        ]);

        MarkdownReporter.Write(target, decision, "suite");

        var content = File.ReadAllText(target);
        content.Should().Contain("## Failures");
        content.Should().Contain("regressed explanation");
        content.Should().NotContain("passed explanation");
    }

    [Fact]
    public void No_failures_section_when_suite_fully_passes()
    {
        var target = PathIn("report.md");
        var decision = new SuiteDecision([
            new BenchmarkDecision(Id(), BenchmarkGateStatus.Passed, [Metric("meanNanoseconds", BenchmarkGateStatus.Passed)], "e")
        ]);

        MarkdownReporter.Write(target, decision, "suite");

        File.ReadAllText(target).Should().NotContain("## Failures");
    }

    [Fact]
    public void Summary_table_counts_include_warning_and_unstable()
    {
        var target = PathIn("report.md");
        var decision = new SuiteDecision([
            new BenchmarkDecision(Id("A"), BenchmarkGateStatus.Warning, [Metric("meanNanoseconds", BenchmarkGateStatus.Warning)], "e"),
            new BenchmarkDecision(Id("B"), BenchmarkGateStatus.Unstable, [], "e"),
        ]);

        MarkdownReporter.Write(target, decision, "suite");

        var content = File.ReadAllText(target);
        content.Should().Contain("Warning");
        content.Should().Contain("Unstable");
    }
}
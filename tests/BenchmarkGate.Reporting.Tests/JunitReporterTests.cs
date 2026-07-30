using Bijecta.BenchmarkGate.Core.Evaluation;
using Bijecta.BenchmarkGate.Core.Identity;
using FluentAssertions;
using System.Xml.Linq;

namespace Bijecta.BenchmarkGate.Reporting.Tests;

public class JunitReporterTests
{
    private static BenchmarkIdentity Id(string method = "Method") =>
        new("Ns.Type", method, "Default");

    private static MetricDecision Metric(string name, BenchmarkGateStatus status, string explanation = "explanation") =>
        new(name, status, BaselineValue: 1000, CurrentValue: 1100, AbsoluteDelta: 100, RelativeDeltaPercent: 10, explanation);

    [Fact]
    public void Writes_one_testcase_per_benchmark_metric_pair()
    {
        var benchmark = new BenchmarkDecision(
            Id(), BenchmarkGateStatus.Regressed,
            [Metric("meanNanoseconds", BenchmarkGateStatus.Regressed), Metric("allocatedBytesPerOperation", BenchmarkGateStatus.Passed)],
            "aggregate explanation");
        var decision = new SuiteDecision([benchmark]);
        var path = Path.GetTempFileName();

        try
        {
            JunitReporter.Write(path, decision, "suite", failOnWarning: false);

            var doc = XDocument.Load(path);
            var testcases = doc.Descendants("testcase").ToList();

            testcases.Should().HaveCount(2);
            testcases[0].Attribute("name")!.Value.Should().Contain("[meanNanoseconds]");
            testcases[1].Attribute("name")!.Value.Should().Contain("[allocatedBytesPerOperation]");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Benchmark_with_no_metrics_gets_exactly_one_testcase()
    {
        var benchmark = new BenchmarkDecision(Id(), BenchmarkGateStatus.Missing, [], "missing explanation");
        var decision = new SuiteDecision([benchmark]);
        var path = Path.GetTempFileName();

        try
        {
            JunitReporter.Write(path, decision, "suite", failOnWarning: false);

            var doc = XDocument.Load(path);
            var testcases = doc.Descendants("testcase").ToList();

            testcases.Should().ContainSingle();
            testcases[0].Attribute("name")!.Value.Should().Be(Id().CanonicalString);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(BenchmarkGateStatus.Regressed)]
    [InlineData(BenchmarkGateStatus.Missing)]
    [InlineData(BenchmarkGateStatus.Unstable)]
    public void Regressed_missing_and_unstable_always_render_as_failure_regardless_of_fail_on_warning(
        BenchmarkGateStatus status)
    {
        var benchmark = new BenchmarkDecision(Id(), status, [Metric("meanNanoseconds", status)], "explanation");
        var decision = new SuiteDecision([benchmark]);
        var path = Path.GetTempFileName();

        try
        {
            JunitReporter.Write(path, decision, "suite", failOnWarning: false);

            XDocument.Load(path).Descendants("failure").Should().ContainSingle();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Warning_only_renders_as_failure_when_fail_on_warning_is_true()
    {
        var benchmark = new BenchmarkDecision(
            Id(), BenchmarkGateStatus.Warning, [Metric("meanNanoseconds", BenchmarkGateStatus.Warning)], "explanation");
        var decision = new SuiteDecision([benchmark]);

        var withoutFlag = Path.GetTempFileName();
        var withFlag = Path.GetTempFileName();

        try
        {
            JunitReporter.Write(withoutFlag, decision, "suite", failOnWarning: false);
            JunitReporter.Write(withFlag, decision, "suite", failOnWarning: true);

            XDocument.Load(withoutFlag).Descendants("failure").Should().BeEmpty();
            XDocument.Load(withFlag).Descendants("failure").Should().ContainSingle();
        }
        finally
        {
            File.Delete(withoutFlag);
            File.Delete(withFlag);
        }
    }

    [Fact]
    public void Testsuite_failures_attribute_matches_actual_failure_count()
    {
        var regressed = new BenchmarkDecision(
            Id("A"), BenchmarkGateStatus.Regressed, [Metric("meanNanoseconds", BenchmarkGateStatus.Regressed)], "e");
        var passed = new BenchmarkDecision(
            Id("B"), BenchmarkGateStatus.Passed, [Metric("meanNanoseconds", BenchmarkGateStatus.Passed)], "e");
        var decision = new SuiteDecision([regressed, passed]);
        var path = Path.GetTempFileName();

        try
        {
            JunitReporter.Write(path, decision, "suite", failOnWarning: false);

            var testsuite = XDocument.Load(path).Root!;
            testsuite.Attribute("tests")!.Value.Should().Be("2");
            testsuite.Attribute("failures")!.Value.Should().Be("1");
        }
        finally
        {
            File.Delete(path);
        }
    }
}
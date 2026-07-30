using System.Text.Json;
using Bijecta.BenchmarkGate.Core.Evaluation;
using Bijecta.BenchmarkGate.Core.Identity;
using FluentAssertions;

namespace Bijecta.BenchmarkGate.Reporting.Tests;

public sealed class JsonDecisionReporterTests : IDisposable
{
    private readonly string _tempDirectory =
        Path.Combine(Path.GetTempPath(), $"json-decision-reporter-tests-{Guid.NewGuid():N}");

    public JsonDecisionReporterTests()
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
    public void Writes_valid_json()
    {
        var target = PathIn("decision.json");
        var decision = new SuiteDecision([
            new BenchmarkDecision(Id(), BenchmarkGateStatus.Passed, [Metric("meanNanoseconds", BenchmarkGateStatus.Passed)], "explanation")
        ]);

        JsonDecisionReporter.Write(target, decision, failOnWarning: false);

        var act = () => JsonDocument.Parse(File.ReadAllText(target));
        act.Should().NotThrow();
    }

    [Fact]
    public void Exit_code_reflects_fail_on_warning_flag()
    {
        var target = PathIn("decision.json");
        var decision = new SuiteDecision([
            new BenchmarkDecision(Id(), BenchmarkGateStatus.Warning, [Metric("meanNanoseconds", BenchmarkGateStatus.Warning)], "explanation")
        ]);

        JsonDecisionReporter.Write(target, decision, failOnWarning: true);
        using var doc = JsonDocument.Parse(File.ReadAllText(target));
        doc.RootElement.GetProperty("exitCode").GetInt32().Should().Be(ExitCodes.Warning);
    }

    [Fact]
    public void Exit_code_is_passed_when_fail_on_warning_is_false_and_only_warnings_present()
    {
        var target = PathIn("decision.json");
        var decision = new SuiteDecision([
            new BenchmarkDecision(Id(), BenchmarkGateStatus.Warning, [Metric("meanNanoseconds", BenchmarkGateStatus.Warning)], "explanation")
        ]);

        JsonDecisionReporter.Write(target, decision, failOnWarning: false);
        using var doc = JsonDocument.Parse(File.ReadAllText(target));
        doc.RootElement.GetProperty("exitCode").GetInt32().Should().Be(ExitCodes.Passed);
    }

    [Fact]
    public void Benchmark_carries_a_nested_metrics_array()
    {
        var target = PathIn("decision.json");
        var decision = new SuiteDecision([
            new BenchmarkDecision(Id(), BenchmarkGateStatus.Regressed,
                [
                    Metric("meanNanoseconds", BenchmarkGateStatus.Regressed),
                    Metric("allocatedBytesPerOperation", BenchmarkGateStatus.Passed),
                ],
                "aggregate explanation")
        ]);

        JsonDecisionReporter.Write(target, decision, failOnWarning: false);

        using var doc = JsonDocument.Parse(File.ReadAllText(target));
        var metrics = doc.RootElement.GetProperty("benchmarks")[0].GetProperty("metrics");
        metrics.GetArrayLength().Should().Be(2);
        metrics[0].GetProperty("metricName").GetString().Should().Be("meanNanoseconds");
        metrics[1].GetProperty("metricName").GetString().Should().Be("allocatedBytesPerOperation");
    }

    [Fact]
    public void Benchmarks_are_ordered_by_canonical_identity()
    {
        var target = PathIn("decision.json");
        var decision = new SuiteDecision([
            new BenchmarkDecision(Id("Z"), BenchmarkGateStatus.Passed, [Metric("meanNanoseconds", BenchmarkGateStatus.Passed)], "e"),
            new BenchmarkDecision(Id("A"), BenchmarkGateStatus.Passed, [Metric("meanNanoseconds", BenchmarkGateStatus.Passed)], "e"),
        ]);

        JsonDecisionReporter.Write(target, decision, failOnWarning: false);

        using var doc = JsonDocument.Parse(File.ReadAllText(target));
        var identities = doc.RootElement.GetProperty("benchmarks")
            .EnumerateArray()
            .Select(b => b.GetProperty("identity").GetString())
            .ToList();

        identities.Should().Equal(identities.OrderBy(i => i, StringComparer.Ordinal));
    }

    [Fact]
    public void Counts_reflect_suite_decision_including_warning_and_unstable()
    {
        var target = PathIn("decision.json");
        var decision = new SuiteDecision([
            new BenchmarkDecision(Id("A"), BenchmarkGateStatus.Warning, [Metric("meanNanoseconds", BenchmarkGateStatus.Warning)], "e"),
            new BenchmarkDecision(Id("B"), BenchmarkGateStatus.Unstable, [], "e"),
        ]);

        JsonDecisionReporter.Write(target, decision, failOnWarning: false);

        using var doc = JsonDocument.Parse(File.ReadAllText(target));
        doc.RootElement.GetProperty("warning").GetInt32().Should().Be(1);
        doc.RootElement.GetProperty("unstable").GetInt32().Should().Be(1);
    }
}
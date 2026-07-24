using Cedar.BenchmarkGate.Core.Baseline;
using Cedar.BenchmarkGate.Core.Evaluation;
using Cedar.BenchmarkGate.Core.Identity;
using Cedar.BenchmarkGate.Core.Model;
using FluentAssertions;
using Xunit;

namespace Cedar.BenchmarkGate.Core.Tests.Evaluation;

public class RegressionEvaluatorTests
{
    private static BenchmarkIdentity Id(string method = "Method") =>
        new("Ns.Type", method, "Default");

    [Fact]
    public void Identical_baseline_and_current_always_passes()
    {
        var baseline = new BenchmarkBaseline("suite", [new BaselineEntry(Id(), 1000)]);
        var observations = new List<BenchmarkObservation> { new(Id(), 1000) };
        var policy = new RegressionPolicy(FailurePercent: 10);

        var result = RegressionEvaluator.Evaluate(observations, baseline, policy);

        result.Benchmarks.Single().Status.Should().Be(BenchmarkGateStatus.Passed);
    }

    [Fact]
    public void Regression_of_exactly_the_threshold_fails_inclusive_boundary()
    {
        // 15% threshold, baseline 1000ns -> current 1150ns is exactly 15%.
        var baseline = new BenchmarkBaseline("suite", [new BaselineEntry(Id(), 1000)]);
        var observations = new List<BenchmarkObservation> { new(Id(), 1150) };
        var policy = new RegressionPolicy(FailurePercent: 15);

        var result = RegressionEvaluator.Evaluate(observations, baseline, policy);

        result.Benchmarks.Single().Status.Should().Be(BenchmarkGateStatus.Regressed);
    }

    [Fact]
    public void Regression_just_under_the_threshold_passes()
    {
        var baseline = new BenchmarkBaseline("suite", [new BaselineEntry(Id(), 1000)]);
        var observations = new List<BenchmarkObservation> { new(Id(), 1149) };
        var policy = new RegressionPolicy(FailurePercent: 15);

        var result = RegressionEvaluator.Evaluate(observations, baseline, policy);

        result.Benchmarks.Single().Status.Should().Be(BenchmarkGateStatus.Passed);
    }

    [Fact]
    public void Better_lower_is_better_value_never_regresses()
    {
        var baseline = new BenchmarkBaseline("suite", [new BaselineEntry(Id(), 1000)]);
        var observations = new List<BenchmarkObservation> { new(Id(), 500) };
        var policy = new RegressionPolicy(FailurePercent: 5);

        var result = RegressionEvaluator.Evaluate(observations, baseline, policy);

        result.Benchmarks.Single().Status.Should().Be(BenchmarkGateStatus.Improved);
    }

    [Fact]
    public void Minimum_absolute_change_guard_suppresses_tiny_noisy_regressions()
    {
        // 1ns -> 1.2ns is 20% (over a 15% threshold) but only 0.2ns absolute,
        // under a 100ns minimum absolute change guard.
        var baseline = new BenchmarkBaseline("suite", [new BaselineEntry(Id(), 1)]);
        var observations = new List<BenchmarkObservation> { new(Id(), 1.2) };
        var policy = new RegressionPolicy(FailurePercent: 15, MinimumAbsoluteChangeNanoseconds: 100);

        var result = RegressionEvaluator.Evaluate(observations, baseline, policy);

        result.Benchmarks.Single().Status.Should().Be(BenchmarkGateStatus.Passed);
    }

    [Fact]
    public void Zero_baseline_with_positive_current_fails()
    {
        var baseline = new BenchmarkBaseline("suite", [new BaselineEntry(Id(), 0)]);
        var observations = new List<BenchmarkObservation> { new(Id(), 10) };
        var policy = new RegressionPolicy(FailurePercent: 5);

        var result = RegressionEvaluator.Evaluate(observations, baseline, policy);

        result.Benchmarks.Single().Status.Should().Be(BenchmarkGateStatus.Regressed);
    }

    [Fact]
    public void Benchmark_missing_from_current_results_is_reported_as_missing()
    {
        var baseline = new BenchmarkBaseline("suite", [new BaselineEntry(Id(), 1000)]);
        var observations = new List<BenchmarkObservation>();
        var policy = new RegressionPolicy(FailurePercent: 10);

        var result = RegressionEvaluator.Evaluate(observations, baseline, policy);

        result.Benchmarks.Single().Status.Should().Be(BenchmarkGateStatus.Missing);
        result.ExitCode.Should().Be(ExitCodes.IncompleteResultSet);
    }

    [Fact]
    public void Benchmark_not_in_baseline_is_reported_as_new()
    {
        var baseline = new BenchmarkBaseline("suite", []);
        var observations = new List<BenchmarkObservation> { new(Id(), 1000) };
        var policy = new RegressionPolicy(FailurePercent: 10);

        var result = RegressionEvaluator.Evaluate(observations, baseline, policy);

        result.Benchmarks.Single().Status.Should().Be(BenchmarkGateStatus.New);
    }

    [Fact]
    public void Regression_exit_code_takes_precedence_over_missing()
    {
        var baseline = new BenchmarkBaseline("suite",
        [
            new BaselineEntry(Id("A"), 1000),
            new BaselineEntry(Id("B"), 1000),
        ]);
        // A regresses badly, B is missing from current results entirely.
        var observations = new List<BenchmarkObservation> { new(Id("A"), 2000) };
        var policy = new RegressionPolicy(FailurePercent: 10);

        var result = RegressionEvaluator.Evaluate(observations, baseline, policy);

        result.RegressedCount.Should().Be(1);
        result.MissingCount.Should().Be(1);
        result.ExitCode.Should().Be(ExitCodes.Regressed);
    }

    [Fact]
    public void Output_ordering_is_deterministic_regardless_of_input_order()
    {
        var baseline = new BenchmarkBaseline("suite",
        [
            new BaselineEntry(Id("Z"), 1000),
            new BaselineEntry(Id("A"), 1000),
        ]);
        var observations = new List<BenchmarkObservation>
        {
            new(Id("Z"), 1000),
            new(Id("A"), 1000),
        };
        var policy = new RegressionPolicy(FailurePercent: 10);

        var result1 = RegressionEvaluator.Evaluate(observations, baseline, policy);
        var result2 = RegressionEvaluator.Evaluate(observations, baseline, policy);

        result1.Benchmarks.Select(b => b.Identity.CanonicalString)
            .Should().Equal(result2.Benchmarks.Select(b => b.Identity.CanonicalString));
    }
}

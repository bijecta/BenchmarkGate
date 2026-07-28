using Bijecta.BenchmarkGate.Core.Baseline;
using Bijecta.BenchmarkGate.Core.Evaluation;
using Bijecta.BenchmarkGate.Core.Identity;
using Bijecta.BenchmarkGate.Core.Model;
using FluentAssertions;
using Xunit;

namespace Bijecta.BenchmarkGate.Core.Tests.Evaluation;

public class RegressionEvaluatorTests
{
    private static BenchmarkIdentity Id(string method = "Method") =>
        new("Ns.Type", method, "Default");

    private static Dictionary<string, double> MeanOnly(double nanoseconds) =>
        new Dictionary<string, double> { [BenchmarkObservation.MeanNanosecondsMetric] = nanoseconds };

    private static BenchmarkObservation Observation(
        string method,
        double meanNanoseconds,
        int measurementCount = 20,
        double standardDeviationNanoseconds = 0) =>
        new(Id(method), MeanOnly(meanNanoseconds), measurementCount, standardDeviationNanoseconds);

    private static GatePolicy Policy(
        double warningPercent,
        double failurePercent,
        double minimumAbsoluteChange = 0,
        int minimumMeasurements = 1,
        double maximumCoefficientOfVariation = 1.0) =>
        new()
        {
            Stability = new StabilityPolicy
            {
                MinimumMeasurements = minimumMeasurements,
                MaximumCoefficientOfVariation = maximumCoefficientOfVariation
            },
            Metrics = new Dictionary<string, MetricPolicy>
            {
                [BenchmarkObservation.MeanNanosecondsMetric] = new()
                {
                    Direction = MetricDirection.LowerIsBetter,
                    WarningPercent = warningPercent,
                    FailurePercent = failurePercent,
                    MinimumAbsoluteChange = minimumAbsoluteChange
                }
            }
        };

    [Fact]
    public void Identical_baseline_and_current_always_passes()
    {
        var baseline = new BenchmarkBaseline("suite", [new BaselineEntry(Id(), MeanOnly(1000))]);
        var observations = new List<BenchmarkObservation> { Observation("Method", 1000) };
        var policy = Policy(warningPercent: 5, failurePercent: 10);

        var result = RegressionEvaluator.Evaluate(observations, baseline, policy);

        result.Benchmarks.Single().Status.Should().Be(BenchmarkGateStatus.Passed);
    }

    [Fact]
    public void Regression_of_exactly_the_failure_threshold_fails_inclusive_boundary()
    {
        // 15% failure threshold, baseline 1000ns -> current 1150ns is exactly 15%.
        var baseline = new BenchmarkBaseline("suite", [new BaselineEntry(Id(), MeanOnly(1000))]);
        var observations = new List<BenchmarkObservation> { Observation("Method", 1150) };
        var policy = Policy(warningPercent: 7.5, failurePercent: 15);

        var result = RegressionEvaluator.Evaluate(observations, baseline, policy);

        result.Benchmarks.Single().Status.Should().Be(BenchmarkGateStatus.Regressed);
    }

    [Fact]
    public void Regression_just_under_failure_but_over_warning_is_a_warning()
    {
        var baseline = new BenchmarkBaseline("suite", [new BaselineEntry(Id(), MeanOnly(1000))]);
        var observations = new List<BenchmarkObservation> { Observation("Method", 1100) }; // 10%
        var policy = Policy(warningPercent: 7.5, failurePercent: 15);

        var result = RegressionEvaluator.Evaluate(observations, baseline, policy);

        result.Benchmarks.Single().Status.Should().Be(BenchmarkGateStatus.Warning);
    }

    [Fact]
    public void Regression_just_under_the_warning_threshold_passes()
    {
        var baseline = new BenchmarkBaseline("suite", [new BaselineEntry(Id(), MeanOnly(1000))]);
        var observations = new List<BenchmarkObservation> { Observation("Method", 1074) }; // 7.4%
        var policy = Policy(warningPercent: 7.5, failurePercent: 15);

        var result = RegressionEvaluator.Evaluate(observations, baseline, policy);

        result.Benchmarks.Single().Status.Should().Be(BenchmarkGateStatus.Passed);
    }

    [Fact]
    public void Better_lower_is_better_value_never_regresses()
    {
        var baseline = new BenchmarkBaseline("suite", [new BaselineEntry(Id(), MeanOnly(1000))]);
        var observations = new List<BenchmarkObservation> { Observation("Method", 500) };
        var policy = Policy(warningPercent: 2.5, failurePercent: 5);

        var result = RegressionEvaluator.Evaluate(observations, baseline, policy);

        result.Benchmarks.Single().Status.Should().Be(BenchmarkGateStatus.Improved);
    }

    [Fact]
    public void Minimum_absolute_change_guard_suppresses_tiny_noisy_regressions()
    {
        // 1ns -> 1.2ns is 20% (over both thresholds) but only 0.2ns absolute,
        // under a 100ns minimum absolute change guard.
        var baseline = new BenchmarkBaseline("suite", [new BaselineEntry(Id(), MeanOnly(1))]);
        var observations = new List<BenchmarkObservation> { Observation("Method", 1.2) };
        var policy = Policy(warningPercent: 7.5, failurePercent: 15, minimumAbsoluteChange: 100);

        var result = RegressionEvaluator.Evaluate(observations, baseline, policy);

        result.Benchmarks.Single().Status.Should().Be(BenchmarkGateStatus.Passed);
    }

    [Fact]
    public void Zero_baseline_with_positive_current_fails()
    {
        var baseline = new BenchmarkBaseline("suite", [new BaselineEntry(Id(), MeanOnly(0))]);
        var observations = new List<BenchmarkObservation> { Observation("Method", 10) };
        var policy = Policy(warningPercent: 2.5, failurePercent: 5);

        var result = RegressionEvaluator.Evaluate(observations, baseline, policy);

        result.Benchmarks.Single().Status.Should().Be(BenchmarkGateStatus.Regressed);
    }

    [Fact]
    public void Benchmark_missing_from_current_results_is_reported_as_missing()
    {
        var baseline = new BenchmarkBaseline("suite", [new BaselineEntry(Id(), MeanOnly(1000))]);
        var observations = new List<BenchmarkObservation>();
        var policy = Policy(warningPercent: 5, failurePercent: 10);

        var result = RegressionEvaluator.Evaluate(observations, baseline, policy);

        result.Benchmarks.Single().Status.Should().Be(BenchmarkGateStatus.Missing);
        result.GetExitCode(failOnWarning: false).Should().Be(ExitCodes.IncompleteResultSet);
    }

    [Fact]
    public void Benchmark_not_in_baseline_is_reported_as_new()
    {
        var baseline = new BenchmarkBaseline("suite", []);
        var observations = new List<BenchmarkObservation> { Observation("Method", 1000) };
        var policy = Policy(warningPercent: 5, failurePercent: 10);

        var result = RegressionEvaluator.Evaluate(observations, baseline, policy);

        result.Benchmarks.Single().Status.Should().Be(BenchmarkGateStatus.New);
    }

    [Fact]
    public void Regression_exit_code_takes_precedence_over_missing()
    {
        var baseline = new BenchmarkBaseline("suite",
        [
            new BaselineEntry(Id("A"), MeanOnly(1000)),
            new BaselineEntry(Id("B"), MeanOnly(1000)),
        ]);
        // A regresses badly, B is missing from current results entirely.
        var observations = new List<BenchmarkObservation> { Observation("A", 2000) };
        var policy = Policy(warningPercent: 5, failurePercent: 10);

        var result = RegressionEvaluator.Evaluate(observations, baseline, policy);

        result.RegressedCount.Should().Be(1);
        result.MissingCount.Should().Be(1);
        result.GetExitCode(failOnWarning: false).Should().Be(ExitCodes.Regressed);
    }

    [Fact]
    public void Unstable_measurement_count_short_circuits_metric_evaluation()
    {
        // Identical values (would otherwise Pass), but below the minimum
        // measurement count — should report Unstable, not Passed.
        var baseline = new BenchmarkBaseline("suite", [new BaselineEntry(Id(), MeanOnly(1000))]);
        var observations = new List<BenchmarkObservation>
        {
            Observation("Method", 1000, measurementCount: 3)
        };
        var policy = Policy(warningPercent: 5, failurePercent: 10, minimumMeasurements: 10);

        var result = RegressionEvaluator.Evaluate(observations, baseline, policy);

        result.Benchmarks.Single().Status.Should().Be(BenchmarkGateStatus.Unstable);
        result.GetExitCode(failOnWarning: false).Should().Be(ExitCodes.UnstableResults);
    }

    [Fact]
    public void High_coefficient_of_variation_is_reported_as_unstable()
    {
        var baseline = new BenchmarkBaseline("suite", [new BaselineEntry(Id(), MeanOnly(1000))]);
        var observations = new List<BenchmarkObservation>
        {
            // CoV = stddev/mean = 300/1000 = 0.30, over a 0.05 max.
            Observation("Method", 1000, measurementCount: 20, standardDeviationNanoseconds: 300)
        };
        var policy = Policy(warningPercent: 5, failurePercent: 10, maximumCoefficientOfVariation: 0.05);

        var result = RegressionEvaluator.Evaluate(observations, baseline, policy);

        result.Benchmarks.Single().Status.Should().Be(BenchmarkGateStatus.Unstable);
    }

    [Fact]
    public void Warning_only_suite_exit_code_depends_on_fail_on_warning_flag()
    {
        var baseline = new BenchmarkBaseline("suite", [new BaselineEntry(Id(), MeanOnly(1000))]);
        var observations = new List<BenchmarkObservation> { Observation("Method", 1100) }; // 10%
        var policy = Policy(warningPercent: 7.5, failurePercent: 15);

        var result = RegressionEvaluator.Evaluate(observations, baseline, policy);

        result.GetExitCode(failOnWarning: false).Should().Be(ExitCodes.Passed);
        result.GetExitCode(failOnWarning: true).Should().Be(ExitCodes.Warning);
    }

    [Fact]
    public void Output_ordering_is_deterministic_regardless_of_input_order()
    {
        var baseline = new BenchmarkBaseline("suite",
        [
            new BaselineEntry(Id("Z"), MeanOnly(1000)),
            new BaselineEntry(Id("A"), MeanOnly(1000)),
        ]);
        var observations = new List<BenchmarkObservation>
        {
            Observation("Z", 1000),
            Observation("A", 1000),
        };
        var policy = Policy(warningPercent: 5, failurePercent: 10);

        var result1 = RegressionEvaluator.Evaluate(observations, baseline, policy);
        var result2 = RegressionEvaluator.Evaluate(observations, baseline, policy);

        result1.Benchmarks.Select(b => b.Identity.CanonicalString)
            .Should().Equal(result2.Benchmarks.Select(b => b.Identity.CanonicalString));
    }
}
using Bijecta.BenchmarkGate.Core.Baseline;
using Bijecta.BenchmarkGate.Core.Comparison;
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
        new() { [BenchmarkObservation.MeanNanosecondsMetric] = nanoseconds };

    private static Dictionary<string, double> MeanAndAllocated(double nanoseconds, double allocatedBytes) =>
        new()
        {
            [BenchmarkObservation.MeanNanosecondsMetric] = nanoseconds,
            [BenchmarkObservation.AllocatedBytesMetric] = allocatedBytes,
        };

    private static BenchmarkObservation Observation(
        string method,
        double meanNanoseconds,
        int measurementCount = 20,
        double standardDeviationNanoseconds = 0) =>
        new(Id(method), MeanOnly(meanNanoseconds), measurementCount, standardDeviationNanoseconds);

    private static BenchmarkObservation ObservationWithMetrics(
        string method,
        IReadOnlyDictionary<string, double> metrics,
        int measurementCount = 20,
        double standardDeviationNanoseconds = 0) =>
        new(Id(method), metrics, measurementCount, standardDeviationNanoseconds);

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

    private static GatePolicy TwoMetricPolicy(
        double meanWarningPercent, double meanFailurePercent,
        double allocatedWarningPercent, double allocatedFailurePercent) =>
        new()
        {
            Stability = new StabilityPolicy { MinimumMeasurements = 1, MaximumCoefficientOfVariation = 1.0 },
            Metrics = new Dictionary<string, MetricPolicy>
            {
                [BenchmarkObservation.MeanNanosecondsMetric] = new()
                {
                    Direction = MetricDirection.LowerIsBetter,
                    WarningPercent = meanWarningPercent,
                    FailurePercent = meanFailurePercent,
                    MinimumAbsoluteChange = 0
                },
                [BenchmarkObservation.AllocatedBytesMetric] = new()
                {
                    Direction = MetricDirection.LowerIsBetter,
                    WarningPercent = allocatedWarningPercent,
                    FailurePercent = allocatedFailurePercent,
                    MinimumAbsoluteChange = 0
                },
            }
        };

    private static GatePolicy AllocatedOnlyPolicy(
        double warningPercent, double failurePercent, double maximumCoefficientOfVariation = 1.0) =>
        new()
        {
            Stability = new StabilityPolicy { MinimumMeasurements = 1, MaximumCoefficientOfVariation = maximumCoefficientOfVariation },
            Metrics = new Dictionary<string, MetricPolicy>
            {
                [BenchmarkObservation.AllocatedBytesMetric] = new()
                {
                    Direction = MetricDirection.LowerIsBetter,
                    WarningPercent = warningPercent,
                    FailurePercent = failurePercent,
                    MinimumAbsoluteChange = 0
                }
            }
        };

    private static SuiteDecision Evaluate(
        BenchmarkBaseline baseline, IReadOnlyCollection<BenchmarkObservation> observations, GatePolicy policy)
    {
        var comparison = BenchmarkComparisonEngine.Compare(baseline, observations);
        return RegressionEvaluator.Evaluate(comparison, policy);
    }

    // ============================================================
    // Migrated from the pre-v0.4.0 evaluator suite — same scenarios,
    // same assertions, now driven through BenchmarkComparisonEngine.
    // ============================================================

    [Fact]
    public void Identical_baseline_and_current_always_passes()
    {
        var baseline = new BenchmarkBaseline("suite", [new BaselineEntry(Id(), MeanOnly(1000))]);
        var observations = new List<BenchmarkObservation> { Observation("Method", 1000) };
        var policy = Policy(warningPercent: 5, failurePercent: 10);

        var result = Evaluate(baseline, observations, policy);

        result.Benchmarks.Single().Status.Should().Be(BenchmarkGateStatus.Passed);
    }

    [Fact]
    public void Regression_of_exactly_the_failure_threshold_fails_inclusive_boundary()
    {
        var baseline = new BenchmarkBaseline("suite", [new BaselineEntry(Id(), MeanOnly(1000))]);
        var observations = new List<BenchmarkObservation> { Observation("Method", 1150) };
        var policy = Policy(warningPercent: 7.5, failurePercent: 15);

        var result = Evaluate(baseline, observations, policy);

        result.Benchmarks.Single().Status.Should().Be(BenchmarkGateStatus.Regressed);
    }

    [Fact]
    public void Regression_just_under_failure_but_over_warning_is_a_warning()
    {
        var baseline = new BenchmarkBaseline("suite", [new BaselineEntry(Id(), MeanOnly(1000))]);
        var observations = new List<BenchmarkObservation> { Observation("Method", 1100) };
        var policy = Policy(warningPercent: 7.5, failurePercent: 15);

        var result = Evaluate(baseline, observations, policy);

        result.Benchmarks.Single().Status.Should().Be(BenchmarkGateStatus.Warning);
    }

    [Fact]
    public void Regression_just_under_the_warning_threshold_passes()
    {
        var baseline = new BenchmarkBaseline("suite", [new BaselineEntry(Id(), MeanOnly(1000))]);
        var observations = new List<BenchmarkObservation> { Observation("Method", 1074) };
        var policy = Policy(warningPercent: 7.5, failurePercent: 15);

        var result = Evaluate(baseline, observations, policy);

        result.Benchmarks.Single().Status.Should().Be(BenchmarkGateStatus.Passed);
    }

    [Fact]
    public void Better_lower_is_better_value_never_regresses()
    {
        var baseline = new BenchmarkBaseline("suite", [new BaselineEntry(Id(), MeanOnly(1000))]);
        var observations = new List<BenchmarkObservation> { Observation("Method", 500) };
        var policy = Policy(warningPercent: 2.5, failurePercent: 5);

        var result = Evaluate(baseline, observations, policy);

        result.Benchmarks.Single().Status.Should().Be(BenchmarkGateStatus.Improved);
    }

    [Fact]
    public void Minimum_absolute_change_guard_suppresses_tiny_noisy_regressions()
    {
        var baseline = new BenchmarkBaseline("suite", [new BaselineEntry(Id(), MeanOnly(1))]);
        var observations = new List<BenchmarkObservation> { Observation("Method", 1.2) };
        var policy = Policy(warningPercent: 7.5, failurePercent: 15, minimumAbsoluteChange: 100);

        var result = Evaluate(baseline, observations, policy);

        result.Benchmarks.Single().Status.Should().Be(BenchmarkGateStatus.Passed);
    }

    [Fact]
    public void Zero_baseline_with_positive_current_fails()
    {
        var baseline = new BenchmarkBaseline("suite", [new BaselineEntry(Id(), MeanOnly(0))]);
        var observations = new List<BenchmarkObservation> { Observation("Method", 10) };
        var policy = Policy(warningPercent: 2.5, failurePercent: 5);

        var result = Evaluate(baseline, observations, policy);

        result.Benchmarks.Single().Status.Should().Be(BenchmarkGateStatus.Regressed);
    }

    [Fact]
    public void Benchmark_missing_from_current_results_is_reported_as_missing()
    {
        var baseline = new BenchmarkBaseline("suite", [new BaselineEntry(Id(), MeanOnly(1000))]);
        var observations = new List<BenchmarkObservation>();
        var policy = Policy(warningPercent: 5, failurePercent: 10);

        var result = Evaluate(baseline, observations, policy);

        result.Benchmarks.Single().Status.Should().Be(BenchmarkGateStatus.Missing);
        result.GetExitCode(failOnWarning: false).Should().Be(ExitCodes.IncompleteResultSet);
    }

    [Fact]
    public void Benchmark_not_in_baseline_is_reported_as_new()
    {
        var baseline = new BenchmarkBaseline("suite", []);
        var observations = new List<BenchmarkObservation> { Observation("Method", 1000) };
        var policy = Policy(warningPercent: 5, failurePercent: 10);

        var result = Evaluate(baseline, observations, policy);

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
        var observations = new List<BenchmarkObservation> { Observation("A", 2000) };
        var policy = Policy(warningPercent: 5, failurePercent: 10);

        var result = Evaluate(baseline, observations, policy);

        result.RegressedCount.Should().Be(1);
        result.MissingCount.Should().Be(1);
        result.GetExitCode(failOnWarning: false).Should().Be(ExitCodes.Regressed);
    }

    [Fact]
    public void Unstable_measurement_count_short_circuits_metric_evaluation()
    {
        var baseline = new BenchmarkBaseline("suite", [new BaselineEntry(Id(), MeanOnly(1000))]);
        var observations = new List<BenchmarkObservation>
        {
            Observation("Method", 1000, measurementCount: 3)
        };
        var policy = Policy(warningPercent: 5, failurePercent: 10, minimumMeasurements: 10);

        var result = Evaluate(baseline, observations, policy);

        result.Benchmarks.Single().Status.Should().Be(BenchmarkGateStatus.Unstable);
        result.GetExitCode(failOnWarning: false).Should().Be(ExitCodes.UnstableResults);
    }

    [Fact]
    public void High_coefficient_of_variation_is_reported_as_unstable()
    {
        var baseline = new BenchmarkBaseline("suite", [new BaselineEntry(Id(), MeanOnly(1000))]);
        var observations = new List<BenchmarkObservation>
        {
            Observation("Method", 1000, measurementCount: 20, standardDeviationNanoseconds: 300)
        };
        var policy = Policy(warningPercent: 5, failurePercent: 10, maximumCoefficientOfVariation: 0.05);

        var result = Evaluate(baseline, observations, policy);

        result.Benchmarks.Single().Status.Should().Be(BenchmarkGateStatus.Unstable);
    }

    [Fact]
    public void Warning_only_suite_exit_code_depends_on_fail_on_warning_flag()
    {
        var baseline = new BenchmarkBaseline("suite", [new BaselineEntry(Id(), MeanOnly(1000))]);
        var observations = new List<BenchmarkObservation> { Observation("Method", 1100) };
        var policy = Policy(warningPercent: 7.5, failurePercent: 15);

        var result = Evaluate(baseline, observations, policy);

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

        var result1 = Evaluate(baseline, observations, policy);
        var result2 = Evaluate(baseline, observations, policy);

        result1.Benchmarks.Select(b => b.Identity.CanonicalString)
            .Should().Equal(result2.Benchmarks.Select(b => b.Identity.CanonicalString));
    }

    // ============================================================
    // New: deliberate ordering-behavior change (canonical order, not
    // caller-supplied order) — flagged in the PR description.
    // ============================================================

    [Fact]
    public void SuiteDecision_benchmarks_are_in_canonical_identity_order_not_input_order()
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

        var result = Evaluate(baseline, observations, policy);

        result.Benchmarks.Select(b => b.Identity.MethodName).Should().Equal("A", "Z");
    }

    // ============================================================
    // New: stability characterization beyond what the pre-v0.4.0
    // suite covered — measurement-count case was already migrated
    // above; these fill the corrected acceptance criteria's list.
    // ============================================================

    [Fact]
    public void Coefficient_of_variation_exactly_at_the_threshold_is_not_unstable()
    {
        // stddev/mean = 50/1000 = 0.05, threshold = 0.05 (strict >, not >=).
        var baseline = new BenchmarkBaseline("suite", [new BaselineEntry(Id(), MeanOnly(1000))]);
        var observations = new List<BenchmarkObservation>
        {
            Observation("Method", 1000, measurementCount: 20, standardDeviationNanoseconds: 50)
        };
        var policy = Policy(warningPercent: 5, failurePercent: 10, maximumCoefficientOfVariation: 0.05);

        var result = Evaluate(baseline, observations, policy);

        result.Benchmarks.Single().Status.Should().NotBe(BenchmarkGateStatus.Unstable);
    }

    [Fact]
    public void Coefficient_of_variation_below_the_threshold_is_not_unstable()
    {
        var baseline = new BenchmarkBaseline("suite", [new BaselineEntry(Id(), MeanOnly(1000))]);
        var observations = new List<BenchmarkObservation>
        {
            Observation("Method", 1000, measurementCount: 20, standardDeviationNanoseconds: 20)
        };
        var policy = Policy(warningPercent: 5, failurePercent: 10, maximumCoefficientOfVariation: 0.05);

        var result = Evaluate(baseline, observations, policy);

        result.Benchmarks.Single().Status.Should().NotBe(BenchmarkGateStatus.Unstable);
    }

    [Fact]
    public void Zero_candidate_mean_skips_the_coefficient_of_variation_check()
    {
        // A huge stddev over a zero mean would be Infinity if computed —
        // legacy code short-circuited on mean==0 before dividing, and the
        // benchmark still gets evaluated on its merits (here: a 100% drop
        // to zero, an improvement for a lower-is-better metric).
        var baseline = new BenchmarkBaseline("suite", [new BaselineEntry(Id(), MeanOnly(1000))]);
        var observations = new List<BenchmarkObservation>
        {
            Observation("Method", 0, measurementCount: 20, standardDeviationNanoseconds: 5000)
        };
        var policy = Policy(warningPercent: 7.5, failurePercent: 15, maximumCoefficientOfVariation: 0.01);

        var result = Evaluate(baseline, observations, policy);

        result.Benchmarks.Single().Status.Should().Be(BenchmarkGateStatus.Improved);
    }

    [Fact]
    public void NaN_candidate_mean_does_not_reclassify_the_benchmark_as_unstable()
    {
        // meanNanoseconds is NaN (a stability input) but is deliberately
        // NOT the policy metric here — isolates the stability gate's NaN
        // handling from the per-metric loop's skip-invalid behavior, which
        // is covered separately below.
        var baseline = new BenchmarkBaseline("suite", [new BaselineEntry(Id(), MeanAndAllocated(1000, 100))]);
        var observations = new List<BenchmarkObservation>
        {
            ObservationWithMetrics("Method", MeanAndAllocated(double.NaN, 100),
                measurementCount: 20, standardDeviationNanoseconds: 10)
        };
        var policy = AllocatedOnlyPolicy(warningPercent: 5, failurePercent: 10, maximumCoefficientOfVariation: 0.05);

        var result = Evaluate(baseline, observations, policy);

        result.Benchmarks.Single().Status.Should().Be(BenchmarkGateStatus.Passed);
    }

    [Fact]
    public void Infinite_candidate_mean_does_not_reclassify_the_benchmark_as_unstable()
    {
        // stddev/Infinity == 0, so 0 > threshold is false -> not unstable.
        var baseline = new BenchmarkBaseline("suite", [new BaselineEntry(Id(), MeanAndAllocated(1000, 100))]);
        var observations = new List<BenchmarkObservation>
        {
            ObservationWithMetrics("Method", MeanAndAllocated(double.PositiveInfinity, 100),
                measurementCount: 20, standardDeviationNanoseconds: 300)
        };
        var policy = AllocatedOnlyPolicy(warningPercent: 5, failurePercent: 10, maximumCoefficientOfVariation: 0.05);

        var result = Evaluate(baseline, observations, policy);

        result.Benchmarks.Single().Status.Should().Be(BenchmarkGateStatus.Passed);
    }

    [Fact]
    public void NaN_standard_deviation_does_not_reclassify_as_unstable()
    {
        var baseline = new BenchmarkBaseline("suite", [new BaselineEntry(Id(), MeanAndAllocated(1000, 100))]);
        var observations = new List<BenchmarkObservation>
        {
            ObservationWithMetrics("Method", MeanAndAllocated(1000, 100),
                measurementCount: 20, standardDeviationNanoseconds: double.NaN)
        };
        var policy = AllocatedOnlyPolicy(warningPercent: 5, failurePercent: 10, maximumCoefficientOfVariation: 0.05);

        var result = Evaluate(baseline, observations, policy);

        result.Benchmarks.Single().Status.Should().Be(BenchmarkGateStatus.Passed);
    }

    // ============================================================
    // Deliberate v0.4.0 behavior change: a policy metric with an
    // InvalidReferenceValue/InvalidCandidateValue status produces no
    // MetricDecision at all, rather than reproducing the legacy
    // evaluator's silent NaN-arithmetic verdict. Required by #26/#27's
    // boundary — MetricComparison carries no AbsoluteDelta/PercentDelta
    // for any non-Comparable status, and RegressionEvaluator must not
    // recompute them itself.
    // ============================================================

    [Fact]
    public void Invalid_candidate_value_for_the_policy_metric_produces_no_metric_decision()
    {
        var baseline = new BenchmarkBaseline("suite", [new BaselineEntry(Id(), MeanOnly(1000))]);
        var observations = new List<BenchmarkObservation>
        {
            Observation("Method", double.NaN, measurementCount: 20, standardDeviationNanoseconds: 10)
        };
        var policy = Policy(warningPercent: 5, failurePercent: 10, maximumCoefficientOfVariation: 0.05);

        var result = Evaluate(baseline, observations, policy);

        var benchmark = result.Benchmarks.Single();
        benchmark.Status.Should().Be(BenchmarkGateStatus.Passed);
        benchmark.Metrics.Should().BeEmpty();
        benchmark.Explanation.Should().Be("No metrics from the policy were present in both the baseline and current observation.");
    }

    [Fact]
    public void Invalid_reference_value_for_the_policy_metric_produces_no_metric_decision()
    {
        var baseline = new BenchmarkBaseline("suite", [new BaselineEntry(Id(), MeanOnly(double.NaN))]);
        var observations = new List<BenchmarkObservation> { Observation("Method", 1000) };
        var policy = Policy(warningPercent: 5, failurePercent: 10);

        var result = Evaluate(baseline, observations, policy);

        var benchmark = result.Benchmarks.Single();
        benchmark.Status.Should().Be(BenchmarkGateStatus.Passed);
        benchmark.Metrics.Should().BeEmpty();
    }

    [Fact]
    public void Missing_candidate_mean_skips_the_stability_check_and_the_metric_is_not_evaluated()
    {
        // Candidate observation reports no metrics at all for this
        // benchmark; baseline has meanNanoseconds. MissingCandidateMetric.
        var baseline = new BenchmarkBaseline("suite", [new BaselineEntry(Id(), MeanOnly(1000))]);
        var observations = new List<BenchmarkObservation>
        {
            ObservationWithMetrics("Method", new Dictionary<string, double>(),
                measurementCount: 20, standardDeviationNanoseconds: 999)
        };
        var policy = Policy(warningPercent: 5, failurePercent: 10, maximumCoefficientOfVariation: 0.01);

        var result = Evaluate(baseline, observations, policy);

        var benchmark = result.Benchmarks.Single();
        benchmark.Status.Should().Be(BenchmarkGateStatus.Passed);
        benchmark.Metrics.Should().BeEmpty();
        benchmark.Explanation.Should().Be("No metrics from the policy were present in both the baseline and current observation.");
    }

    // ============================================================
    // New: multi-metric aggregation and benchmark-level precedence —
    // no pre-v0.4.0 test exercised more than one metric.
    // ============================================================

    [Fact]
    public void Multi_metric_benchmark_status_is_worst_of_its_metrics_regressed_beats_improved()
    {
        var baseline = new BenchmarkBaseline("suite",
            [new BaselineEntry(Id(), MeanAndAllocated(1000, 100))]);
        var observations = new List<BenchmarkObservation>
        {
            // mean: 1000 -> 1200 = 20% regression (over a 15% failure threshold)
            // allocated: 100 -> 50 = 50% improvement (well past a 10% warning threshold)
            ObservationWithMetrics("Method", MeanAndAllocated(1200, 50))
        };
        var policy = TwoMetricPolicy(
            meanWarningPercent: 7.5, meanFailurePercent: 15,
            allocatedWarningPercent: 10, allocatedFailurePercent: 25);

        var result = Evaluate(baseline, observations, policy);

        var benchmark = result.Benchmarks.Single();
        benchmark.Status.Should().Be(BenchmarkGateStatus.Regressed);
        benchmark.Metrics.Should().HaveCount(2);
        benchmark.Metrics.Should().Contain(m => m.MetricName == BenchmarkObservation.MeanNanosecondsMetric
            && m.Status == BenchmarkGateStatus.Regressed);
        benchmark.Metrics.Should().Contain(m => m.MetricName == BenchmarkObservation.AllocatedBytesMetric
            && m.Status == BenchmarkGateStatus.Improved);
    }

    [Fact]
    public void Multi_metric_benchmark_passes_when_both_metrics_are_within_threshold()
    {
        var baseline = new BenchmarkBaseline("suite",
            [new BaselineEntry(Id(), MeanAndAllocated(1000, 100))]);
        var observations = new List<BenchmarkObservation>
        {
            ObservationWithMetrics("Method", MeanAndAllocated(1010, 101))
        };
        var policy = TwoMetricPolicy(
            meanWarningPercent: 7.5, meanFailurePercent: 15,
            allocatedWarningPercent: 7.5, allocatedFailurePercent: 15);

        var result = Evaluate(baseline, observations, policy);

        var benchmark = result.Benchmarks.Single();
        benchmark.Status.Should().Be(BenchmarkGateStatus.Passed);
        benchmark.Metrics.Should().OnlyContain(m => m.Status == BenchmarkGateStatus.Passed);
    }
}
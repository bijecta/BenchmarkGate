using Bijecta.BenchmarkGate.Core.Baseline;
using Bijecta.BenchmarkGate.Core.Comparison;
using Bijecta.BenchmarkGate.Core.Identity;
using Bijecta.BenchmarkGate.Core.Model;
using FluentAssertions;
using Xunit;

namespace Bijecta.BenchmarkGate.Core.Tests.Comparison;

public class BenchmarkComparisonEngineTests
{
    private static BenchmarkIdentity Identity(string method) => new("MyBenchmarks", method, "Ci");

    private static BaselineEntry Entry(string method, IReadOnlyDictionary<string, double> metrics) =>
        new(Identity(method), metrics);

    private static BenchmarkObservation Observation(
        string method, IReadOnlyDictionary<string, double> metrics, int measurementCount = 20, double stdDev = 1.5) =>
        new(Identity(method), metrics, measurementCount, stdDev);

    private static BenchmarkBaseline Baseline(string suite, params BaselineEntry[] entries) =>
        new(suite, entries);

    private static Dictionary<string, double> Metrics(params (string Name, double Value)[] entries) =>
        entries.ToDictionary(e => e.Name, e => e.Value);

    // --- Added / Removed / Comparable classification -----------------

    [Fact]
    public void compare_marks_a_candidate_only_benchmark_as_added()
    {
        var baseline = Baseline("nightly");
        var candidate = new[] { Observation("New", Metrics(("meanNanoseconds", 100d))) };

        var result = BenchmarkComparisonEngine.Compare(baseline, candidate);

        result.Benchmarks.Should().ContainSingle(b => b.Status == BenchmarkComparisonStatus.Added);
    }

    [Fact]
    public void compare_marks_a_reference_only_benchmark_as_removed()
    {
        var baseline = Baseline("nightly", Entry("Old", Metrics(("meanNanoseconds", 100d))));
        var candidate = Array.Empty<BenchmarkObservation>();

        var result = BenchmarkComparisonEngine.Compare(baseline, candidate);

        result.Benchmarks.Should().ContainSingle(b => b.Status == BenchmarkComparisonStatus.Removed);
    }

    [Fact]
    public void compare_marks_a_benchmark_present_on_both_sides_as_comparable()
    {
        var baseline = Baseline("nightly", Entry("Sort", Metrics(("meanNanoseconds", 100d))));
        var candidate = new[] { Observation("Sort", Metrics(("meanNanoseconds", 110d))) };

        var result = BenchmarkComparisonEngine.Compare(baseline, candidate);

        result.Benchmarks.Should().ContainSingle(b => b.Status == BenchmarkComparisonStatus.Comparable);
    }

    [Fact]
    public void compare_populates_candidate_stability_for_an_added_benchmark()
    {
        var baseline = Baseline("nightly");
        var candidate = new[] { Observation("New", Metrics(("meanNanoseconds", 100d)), measurementCount: 30, stdDev: 2.1) };

        var result = BenchmarkComparisonEngine.Compare(baseline, candidate);

        var benchmark = result.Benchmarks.Single();
        benchmark.CandidateStability.Should().Be(new BenchmarkStabilityMeasurement(30, 2.1));
    }

    [Fact]
    public void compare_leaves_candidate_stability_null_for_a_removed_benchmark()
    {
        var baseline = Baseline("nightly", Entry("Old", Metrics(("meanNanoseconds", 100d))));

        var result = BenchmarkComparisonEngine.Compare(baseline, Array.Empty<BenchmarkObservation>());

        result.Benchmarks.Single().CandidateStability.Should().BeNull();
    }

    [Fact]
    public void compare_gives_an_added_benchmarks_metrics_missing_reference_metric_status()
    {
        var baseline = Baseline("nightly");
        var candidate = new[] { Observation("New", Metrics(("meanNanoseconds", 100d))) };

        var result = BenchmarkComparisonEngine.Compare(baseline, candidate);

        var metric = result.Benchmarks.Single().Metrics.Single();
        metric.Status.Should().Be(MetricComparisonStatus.MissingReferenceMetric);
        metric.Reference.Should().BeNull();
        metric.Candidate.Should().NotBeNull();
    }

    [Fact]
    public void compare_gives_a_removed_benchmarks_metrics_missing_candidate_metric_status()
    {
        var baseline = Baseline("nightly", Entry("Old", Metrics(("meanNanoseconds", 100d))));

        var result = BenchmarkComparisonEngine.Compare(baseline, Array.Empty<BenchmarkObservation>());

        var metric = result.Benchmarks.Single().Metrics.Single();
        metric.Status.Should().Be(MetricComparisonStatus.MissingCandidateMetric);
        metric.Candidate.Should().BeNull();
        metric.Reference.Should().NotBeNull();
    }

    // --- Per-metric status within a Comparable benchmark --------------

    [Fact]
    public void compare_flags_a_metric_present_only_in_the_reference_as_missing_candidate_metric()
    {
        var baseline = Baseline("nightly",
            Entry("Sort", Metrics(("meanNanoseconds", 100d), ("allocatedBytesPerOperation", 50d))));
        var candidate = new[] { Observation("Sort", Metrics(("meanNanoseconds", 110d))) };

        var result = BenchmarkComparisonEngine.Compare(baseline, candidate);

        var metric = result.Benchmarks.Single().Metrics
            .Single(m => m.MetricName == "allocatedBytesPerOperation");
        metric.Status.Should().Be(MetricComparisonStatus.MissingCandidateMetric);
        metric.Reference.Should().NotBeNull();
        metric.Candidate.Should().BeNull();
    }

    [Fact]
    public void compare_flags_a_metric_present_only_in_the_candidate_as_missing_reference_metric()
    {
        var baseline = Baseline("nightly", Entry("Sort", Metrics(("meanNanoseconds", 100d))));
        var candidate = new[]
        {
            Observation("Sort", Metrics(("meanNanoseconds", 110d), ("allocatedBytesPerOperation", 64d))),
        };

        var result = BenchmarkComparisonEngine.Compare(baseline, candidate);

        var metric = result.Benchmarks.Single().Metrics
            .Single(m => m.MetricName == "allocatedBytesPerOperation");
        metric.Status.Should().Be(MetricComparisonStatus.MissingReferenceMetric);
        metric.Reference.Should().BeNull();
        metric.Candidate.Should().NotBeNull();
    }

    [Fact]
    public void compare_flags_a_nan_reference_value_as_invalid_reference_value()
    {
        var baseline = Baseline("nightly", Entry("Sort", Metrics(("meanNanoseconds", double.NaN))));
        var candidate = new[] { Observation("Sort", Metrics(("meanNanoseconds", 110d))) };

        var result = BenchmarkComparisonEngine.Compare(baseline, candidate);

        var metric = result.Benchmarks.Single().Metrics.Single();
        metric.Status.Should().Be(MetricComparisonStatus.InvalidReferenceValue);
        metric.AbsoluteDelta.Should().BeNull();
        metric.PercentDelta.Should().BeNull();
        metric.Direction.Should().BeNull();
    }

    [Fact]
    public void compare_flags_an_infinite_candidate_value_as_invalid_candidate_value()
    {
        var baseline = Baseline("nightly", Entry("Sort", Metrics(("meanNanoseconds", 100d))));
        var candidate = new[] { Observation("Sort", Metrics(("meanNanoseconds", double.PositiveInfinity))) };

        var result = BenchmarkComparisonEngine.Compare(baseline, candidate);

        var metric = result.Benchmarks.Single().Metrics.Single();
        metric.Status.Should().Be(MetricComparisonStatus.InvalidCandidateValue);
        metric.AbsoluteDelta.Should().BeNull();
    }

    [Fact]
    public void compare_prefers_invalid_reference_value_when_both_sides_are_invalid()
    {
        var baseline = Baseline("nightly", Entry("Sort", Metrics(("meanNanoseconds", double.NaN))));
        var candidate = new[] { Observation("Sort", Metrics(("meanNanoseconds", double.NaN))) };

        var result = BenchmarkComparisonEngine.Compare(baseline, candidate);

        result.Benchmarks.Single().Metrics.Single().Status.Should().Be(MetricComparisonStatus.InvalidReferenceValue);
    }

    // --- Delta calculation, reusing PercentDeltaCalculator -------------

    [Fact]
    public void compare_calculates_absolute_and_percent_delta_for_a_normal_comparable_metric()
    {
        var baseline = Baseline("nightly", Entry("Sort", Metrics(("meanNanoseconds", 100d))));
        var candidate = new[] { Observation("Sort", Metrics(("meanNanoseconds", 150d))) };

        var result = BenchmarkComparisonEngine.Compare(baseline, candidate);

        var metric = result.Benchmarks.Single().Metrics.Single();
        metric.Status.Should().Be(MetricComparisonStatus.Comparable);
        metric.AbsoluteDelta.Should().Be(50d);
        metric.PercentDelta.Should().Be(50d);
    }

    [Fact]
    public void compare_normalizes_zero_to_zero_to_a_zero_percent_delta()
    {
        var baseline = Baseline("nightly", Entry("Sort", Metrics(("allocatedBytesPerOperation", 0d))));
        var candidate = new[] { Observation("Sort", Metrics(("allocatedBytesPerOperation", 0d))) };

        var result = BenchmarkComparisonEngine.Compare(baseline, candidate);

        var metric = result.Benchmarks.Single().Metrics.Single();
        metric.AbsoluteDelta.Should().Be(0d);
        metric.PercentDelta.Should().Be(0d);
        metric.Direction.Should().Be(ChangeDirection.Unchanged);
    }

    [Fact]
    public void compare_leaves_percent_delta_null_when_reference_is_zero_and_candidate_is_not()
    {
        var baseline = Baseline("nightly", Entry("Sort", Metrics(("allocatedBytesPerOperation", 0d))));
        var candidate = new[] { Observation("Sort", Metrics(("allocatedBytesPerOperation", 64d))) };

        var result = BenchmarkComparisonEngine.Compare(baseline, candidate);

        var metric = result.Benchmarks.Single().Metrics.Single();
        metric.AbsoluteDelta.Should().Be(64d);
        metric.PercentDelta.Should().BeNull();
    }

    // --- Direction derivation ------------------------------------------

    [Fact]
    public void compare_derives_improvement_for_a_decrease_in_a_lower_is_better_metric()
    {
        var baseline = Baseline("nightly", Entry("Sort", Metrics(("meanNanoseconds", 100d))));
        var candidate = new[] { Observation("Sort", Metrics(("meanNanoseconds", 80d))) };

        var result = BenchmarkComparisonEngine.Compare(baseline, candidate);

        result.Benchmarks.Single().Metrics.Single().Direction.Should().Be(ChangeDirection.Improvement);
    }

    [Fact]
    public void compare_derives_degradation_for_an_increase_in_a_lower_is_better_metric()
    {
        var baseline = Baseline("nightly", Entry("Sort", Metrics(("meanNanoseconds", 100d))));
        var candidate = new[] { Observation("Sort", Metrics(("meanNanoseconds", 120d))) };

        var result = BenchmarkComparisonEngine.Compare(baseline, candidate);

        result.Benchmarks.Single().Metrics.Single().Direction.Should().Be(ChangeDirection.Degradation);
    }

    // NOTE: HigherIsBetter and Neutral direction derivation are both
    // implemented in DeriveDirection but NOT exercised by any test in this
    // file — no built-in MetricCatalog entry uses either
    // OptimizationDirection today (both meanNanoseconds and
    // allocatedBytesPerOperation are LowerIsBetter), so Compare's public
    // API cannot reach those branches with real data. See the PR
    // description: flagged as a real gap rather than covered with a
    // fabricated always-passing test or a private-helper test that
    // exercises unreachable behavior — same principle already applied to
    // deferring UnitMismatch.

    [Fact]
    public void compare_derives_unchanged_for_an_unmoved_known_metric_regardless_of_direction()
    {
        var baseline = Baseline("nightly", Entry("Sort", Metrics(("meanNanoseconds", 100d))));
        var candidate = new[] { Observation("Sort", Metrics(("meanNanoseconds", 100d))) };

        var result = BenchmarkComparisonEngine.Compare(baseline, candidate);

        result.Benchmarks.Single().Metrics.Single().Direction.Should().Be(ChangeDirection.Unchanged);
    }

    [Fact]
    public void compare_derives_unchanged_for_an_unknown_metric_with_equal_values()
    {
        var baseline = Baseline("nightly", Entry("Sort", Metrics(("gen0Collections", 4d))));
        var candidate = new[] { Observation("Sort", Metrics(("gen0Collections", 4d))) };

        var result = BenchmarkComparisonEngine.Compare(baseline, candidate);

        var metric = result.Benchmarks.Single().Metrics.Single();
        metric.Descriptor.Should().BeNull();
        metric.Direction.Should().Be(ChangeDirection.Unchanged);
    }

    [Fact]
    public void compare_derives_indeterminate_for_an_unknown_metric_with_changed_values()
    {
        var baseline = Baseline("nightly", Entry("Sort", Metrics(("gen0Collections", 4d))));
        var candidate = new[] { Observation("Sort", Metrics(("gen0Collections", 6d))) };

        var result = BenchmarkComparisonEngine.Compare(baseline, candidate);

        var metric = result.Benchmarks.Single().Metrics.Single();
        metric.Descriptor.Should().BeNull();
        metric.Direction.Should().Be(ChangeDirection.Indeterminate);
        metric.AbsoluteDelta.Should().Be(2d);
    }

    // --- Unit metadata propagation (UnitMismatch deferred) --------------

    [Fact]
    public void compare_propagates_the_catalog_unit_for_a_known_metric()
    {
        var baseline = Baseline("nightly", Entry("Sort", Metrics(("meanNanoseconds", 100d))));
        var candidate = new[] { Observation("Sort", Metrics(("meanNanoseconds", 110d))) };

        var result = BenchmarkComparisonEngine.Compare(baseline, candidate);

        var metric = result.Benchmarks.Single().Metrics.Single();
        metric.Reference!.Value.Unit.Should().Be("ns");
        metric.Candidate!.Value.Unit.Should().Be("ns");
    }

    [Fact]
    public void compare_leaves_unit_null_for_an_unknown_metric()
    {
        var baseline = Baseline("nightly", Entry("Sort", Metrics(("gen0Collections", 4d))));
        var candidate = new[] { Observation("Sort", Metrics(("gen0Collections", 4d))) };

        var result = BenchmarkComparisonEngine.Compare(baseline, candidate);

        var metric = result.Benchmarks.Single().Metrics.Single();
        metric.Reference!.Value.Unit.Should().BeNull();
        metric.Candidate!.Value.Unit.Should().BeNull();
    }

    // --- No policy vocabulary -------------------------------------------

    [Fact]
    public void benchmark_comparison_engine_type_does_not_reference_gate_policy()
    {
        var engineType = typeof(BenchmarkComparisonEngine);

        engineType.GetMethods()
            .SelectMany(m => m.GetParameters())
            .Should().NotContain(p => p.ParameterType.Name.Contains("Policy"));
    }

    // --- Duplicate identity guard ----------------------------------------

    [Fact]
    public void compare_throws_when_candidate_observations_contain_a_duplicate_identity()
    {
        var baseline = Baseline("nightly");
        var candidate = new[]
        {
            Observation("Sort", Metrics(("meanNanoseconds", 100d))),
            Observation("Sort", Metrics(("meanNanoseconds", 110d))),
        };

        var act = () => BenchmarkComparisonEngine.Compare(baseline, candidate);

        act.Should().Throw<ArgumentException>();
    }

    // --- Deterministic ordering ------------------------------------------

    [Fact]
    public void compare_orders_benchmarks_by_canonical_identity_regardless_of_input_order()
    {
        var zoo = Entry("Run", Metrics(("meanNanoseconds", 1d)));
        var alpha = Entry("Write", Metrics(("meanNanoseconds", 1d)));

        var baselineFirstOrder = Baseline("nightly", zoo, alpha);
        var baselineSecondOrder = Baseline("nightly", alpha, zoo);

        var candidateFirstOrder = new[]
        {
            Observation("Read", Metrics(("meanNanoseconds", 1d))),
            Observation("Write", Metrics(("meanNanoseconds", 1d))),
        };
        var candidateSecondOrder = new[]
        {
            Observation("Write", Metrics(("meanNanoseconds", 1d))),
            Observation("Read", Metrics(("meanNanoseconds", 1d))),
        };

        var first = BenchmarkComparisonEngine.Compare(baselineFirstOrder, candidateFirstOrder);
        var second = BenchmarkComparisonEngine.Compare(baselineSecondOrder, candidateSecondOrder);

        var firstOrder = first.Benchmarks.Select(b => b.Identity.MethodName).ToArray();
        var secondOrder = second.Benchmarks.Select(b => b.Identity.MethodName).ToArray();

        firstOrder.Should().Equal(secondOrder);
        firstOrder.Should().Equal("Read", "Run", "Write");
    }

    [Fact]
    public void compare_sorts_metrics_within_a_benchmark_by_ordinal_name_regardless_of_dictionary_insertion_order()
    {
        var baselineA = Baseline("nightly", Entry("Sort",
            Metrics(("meanNanoseconds", 100d), ("allocatedBytesPerOperation", 50d))));
        var candidateA = new[] { Observation("Sort",
            Metrics(("meanNanoseconds", 110d), ("allocatedBytesPerOperation", 55d))) };

        var baselineB = Baseline("nightly", Entry("Sort",
            Metrics(("allocatedBytesPerOperation", 50d), ("meanNanoseconds", 100d))));
        var candidateB = new[] { Observation("Sort",
            Metrics(("allocatedBytesPerOperation", 55d), ("meanNanoseconds", 110d))) };

        var first = BenchmarkComparisonEngine.Compare(baselineA, candidateA);
        var second = BenchmarkComparisonEngine.Compare(baselineB, candidateB);

        var firstNames = first.Benchmarks.Single().Metrics.Select(m => m.MetricName).ToArray();
        var secondNames = second.Benchmarks.Single().Metrics.Select(m => m.MetricName).ToArray();

        firstNames.Should().Equal(secondNames);
        firstNames.Should().Equal("allocatedBytesPerOperation", "meanNanoseconds");
    }
}
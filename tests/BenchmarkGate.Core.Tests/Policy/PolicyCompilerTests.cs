using Bijecta.BenchmarkGate.Core.Evaluation;
using Bijecta.BenchmarkGate.Core.Policy;
using FluentAssertions;
using Xunit;

namespace Bijecta.BenchmarkGate.Core.Tests.Policy;

public class PolicyCompilerTests
{
    private static readonly StabilityDefinition ValidStability = new(MinimumMeasurements: 3, MaximumCoefficientOfVariation: 0.1);

    private static readonly MetricDefinition ValidMetric = new(
        Direction: "lower-is-better",
        WarningPercent: 5,
        FailurePercent: 10,
        MinimumAbsoluteChange: 100);

    private static PolicyDocument ValidDocument() => new(
        SchemaVersion: 1,
        Stability: ValidStability,
        Metrics: new Dictionary<string, MetricDefinition?> { ["meanNanoseconds"] = ValidMetric });

    [Fact]
    public void Valid_document_compiles_to_the_expected_gate_policy()
    {
        var policy = PolicyCompiler.CompileValidated(ValidDocument());

        policy.Stability.MinimumMeasurements.Should().Be(3);
        policy.Stability.MaximumCoefficientOfVariation.Should().Be(0.1);
        policy.Metrics.Should().ContainKey("meanNanoseconds");
        policy.Metrics["meanNanoseconds"].Direction.Should().Be(MetricDirection.LowerIsBetter);
        policy.Metrics["meanNanoseconds"].WarningPercent.Should().Be(5);
        policy.Metrics["meanNanoseconds"].FailurePercent.Should().Be(10);
        policy.Metrics["meanNanoseconds"].MinimumAbsoluteChange.Should().Be(100);
    }

    [Fact]
    public void Missing_minimum_absolute_change_defaults_to_zero()
    {
        var metric = ValidMetric with { MinimumAbsoluteChange = null };
        var document = ValidDocument() with { Metrics = new Dictionary<string, MetricDefinition?> { ["m"] = metric } };

        var policy = PolicyCompiler.CompileValidated(document);

        policy.Metrics["m"].MinimumAbsoluteChange.Should().Be(0);
    }

    [Fact]
    public void Higher_is_better_direction_compiles_correctly()
    {
        var metric = ValidMetric with { Direction = "higher-is-better" };
        var document = ValidDocument() with { Metrics = new Dictionary<string, MetricDefinition?> { ["m"] = metric } };

        var policy = PolicyCompiler.CompileValidated(document);

        policy.Metrics["m"].Direction.Should().Be(MetricDirection.HigherIsBetter);
    }

    [Fact]
    public void Null_document_throws_ArgumentNullException()
    {
        var act = () => PolicyCompiler.CompileValidated(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Missing_stability_throws_ArgumentException_not_NullReferenceException()
    {
        var document = ValidDocument() with { Stability = null };

        var act = () => PolicyCompiler.CompileValidated(document);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Missing_minimum_measurements_throws_ArgumentException()
    {
        var document = ValidDocument() with { Stability = ValidStability with { MinimumMeasurements = null } };

        var act = () => PolicyCompiler.CompileValidated(document);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Empty_metrics_throws_ArgumentException()
    {
        var document = ValidDocument() with { Metrics = new Dictionary<string, MetricDefinition?>() };

        var act = () => PolicyCompiler.CompileValidated(document);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Null_metric_definition_throws_ArgumentException_not_NullReferenceException()
    {
        var document = ValidDocument() with { Metrics = new Dictionary<string, MetricDefinition?> { ["m"] = null } };

        var act = () => PolicyCompiler.CompileValidated(document);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Missing_metric_direction_throws_ArgumentException_not_InvalidOperationException()
    {
        var metric = ValidMetric with { Direction = null };
        var document = ValidDocument() with { Metrics = new Dictionary<string, MetricDefinition?> { ["m"] = metric } };

        var act = () => PolicyCompiler.CompileValidated(document);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Missing_metric_warning_percent_throws_ArgumentException_not_InvalidOperationException()
    {
        var metric = ValidMetric with { WarningPercent = null };
        var document = ValidDocument() with { Metrics = new Dictionary<string, MetricDefinition?> { ["m"] = metric } };

        var act = () => PolicyCompiler.CompileValidated(document);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Missing_metric_failure_percent_throws_ArgumentException_not_InvalidOperationException()
    {
        var metric = ValidMetric with { FailurePercent = null };
        var document = ValidDocument() with { Metrics = new Dictionary<string, MetricDefinition?> { ["m"] = metric } };

        var act = () => PolicyCompiler.CompileValidated(document);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Unrecognized_direction_throws_ArgumentException_via_switch_default()
    {
        var metric = ValidMetric with { Direction = "sideways" };
        var document = ValidDocument() with { Metrics = new Dictionary<string, MetricDefinition?> { ["m"] = metric } };

        var act = () => PolicyCompiler.CompileValidated(document);

        act.Should().Throw<ArgumentException>();
    }
}
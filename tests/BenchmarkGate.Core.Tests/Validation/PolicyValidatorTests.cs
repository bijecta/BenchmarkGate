using Bijecta.BenchmarkGate.Core.Policy;
using Bijecta.BenchmarkGate.Core.Validation;
using FluentAssertions;
using Xunit;

namespace Bijecta.BenchmarkGate.Core.Tests.Validation;

public class PolicyValidatorTests
{
    private static readonly StabilityDefinition ValidStability = new(MinimumMeasurements: 3, MaximumCoefficientOfVariation: 0.1);

    private static readonly MetricDefinition ValidMetric = new(
        Direction: "lower-is-better",
        WarningPercent: 5,
        FailurePercent: 10,
        MinimumAbsoluteChange: 0);

    private static PolicyDocument ValidDocument(
        int? schemaVersion = 1,
        StabilityDefinition? stability = null,
        IReadOnlyDictionary<string, MetricDefinition?>? metrics = null) =>
        new(
            schemaVersion,
            stability ?? ValidStability,
            metrics ?? new Dictionary<string, MetricDefinition?> { ["meanNanoseconds"] = ValidMetric });

    [Fact]
    public void Fully_valid_document_produces_no_diagnostics()
    {
        var result = PolicyValidator.Validate(ValidDocument());

        result.IsValid.Should().BeTrue();
        result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Missing_schema_version_reports_BGV100()
    {
        var result = PolicyValidator.Validate(ValidDocument(schemaVersion: null));

        result.Diagnostics.Should().ContainSingle(d => d.Descriptor.Id == "BGV100");
    }

    [Fact]
    public void Unsupported_schema_version_reports_BGV101_not_BGV100()
    {
        var result = PolicyValidator.Validate(ValidDocument(schemaVersion: 2));

        result.Diagnostics.Should().ContainSingle(d => d.Descriptor.Id == "BGV101");
        result.Diagnostics.Should().NotContain(d => d.Descriptor.Id == "BGV100");
    }

    [Fact]
    public void Missing_stability_reports_BGV102_and_skips_its_sub_checks()
    {
        var document = new PolicyDocument(
            SchemaVersion: 1,
            Stability: null,
            Metrics: new Dictionary<string, MetricDefinition?> { ["meanNanoseconds"] = ValidMetric });

        var result = PolicyValidator.Validate(document);

        result.Diagnostics.Should().ContainSingle(d => d.Descriptor.Id == "BGV102");
        result.Diagnostics.Should().NotContain(d =>
            d.Descriptor.Id == "BGV103" || d.Descriptor.Id == "BGV104" ||
            d.Descriptor.Id == "BGV105" || d.Descriptor.Id == "BGV106");
    }

    [Fact]
    public void Missing_minimum_measurements_reports_BGV103()
    {
        var stability = ValidStability with { MinimumMeasurements = null };
        var result = PolicyValidator.Validate(ValidDocument(stability: stability));

        result.Diagnostics.Should().ContainSingle(d => d.Descriptor.Id == "BGV103");
    }

    [Fact]
    public void Missing_maximum_coefficient_of_variation_reports_BGV104()
    {
        var stability = ValidStability with { MaximumCoefficientOfVariation = null };
        var result = PolicyValidator.Validate(ValidDocument(stability: stability));

        result.Diagnostics.Should().ContainSingle(d => d.Descriptor.Id == "BGV104");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Non_positive_minimum_measurements_reports_BGV105(int minimumMeasurements)
    {
        var stability = ValidStability with { MinimumMeasurements = minimumMeasurements };
        var result = PolicyValidator.Validate(ValidDocument(stability: stability));

        result.Diagnostics.Should().ContainSingle(d => d.Descriptor.Id == "BGV105");
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Invalid_maximum_coefficient_of_variation_reports_BGV106(double value)
    {
        var stability = ValidStability with { MaximumCoefficientOfVariation = value };
        var result = PolicyValidator.Validate(ValidDocument(stability: stability));

        result.Diagnostics.Should().ContainSingle(d => d.Descriptor.Id == "BGV106");
    }

    [Fact]
    public void Null_metrics_dictionary_reports_BGV107()
    {
        var document = new PolicyDocument(SchemaVersion: 1, Stability: ValidStability, Metrics: null);

        var result = PolicyValidator.Validate(document);

        result.Diagnostics.Should().ContainSingle(d => d.Descriptor.Id == "BGV107");
    }

    [Fact]
    public void Empty_metrics_dictionary_reports_BGV107()
    {
        var result = PolicyValidator.Validate(ValidDocument(metrics: new Dictionary<string, MetricDefinition?>()));

        result.Diagnostics.Should().ContainSingle(d => d.Descriptor.Id == "BGV107");
    }

    [Fact]
    public void Empty_metric_name_reports_BGV108()
    {
        var metrics = new Dictionary<string, MetricDefinition?> { [""] = ValidMetric };
        var result = PolicyValidator.Validate(ValidDocument(metrics: metrics));

        result.Diagnostics.Should().ContainSingle(d => d.Descriptor.Id == "BGV108");
    }

    [Fact]
    public void Null_metric_definition_reports_BGV109_and_does_not_throw()
    {
        var metrics = new Dictionary<string, MetricDefinition?> { ["meanNanoseconds"] = null };

        var act = () => PolicyValidator.Validate(ValidDocument(metrics: metrics));

        act.Should().NotThrow();
        act().Diagnostics.Should().ContainSingle(d => d.Descriptor.Id == "BGV109");
    }

    [Fact]
    public void Null_metric_definition_does_not_also_report_field_level_diagnostics()
    {
        var metrics = new Dictionary<string, MetricDefinition?> { ["meanNanoseconds"] = null };
        var result = PolicyValidator.Validate(ValidDocument(metrics: metrics));

        result.Diagnostics.Should().ContainSingle();
    }

    [Fact]
    public void Missing_direction_reports_BGV110()
    {
        var metric = ValidMetric with { Direction = null };
        var result = ValidateSingleMetric(metric);

        result.Diagnostics.Should().ContainSingle(d => d.Descriptor.Id == "BGV110");
    }

    [Fact]
    public void Missing_warning_percent_reports_BGV111()
    {
        var metric = ValidMetric with { WarningPercent = null };
        var result = ValidateSingleMetric(metric);

        result.Diagnostics.Should().ContainSingle(d => d.Descriptor.Id == "BGV111");
    }

    [Fact]
    public void Missing_failure_percent_reports_BGV112()
    {
        var metric = ValidMetric with { FailurePercent = null };
        var result = ValidateSingleMetric(metric);

        result.Diagnostics.Should().ContainSingle(d => d.Descriptor.Id == "BGV112");
    }

    [Theory]
    [InlineData("Lower-Is-Better")]
    [InlineData("lower_is_better")]
    [InlineData("bogus")]
    public void Unrecognized_direction_reports_BGV113(string direction)
    {
        var metric = ValidMetric with { Direction = direction };
        var result = ValidateSingleMetric(metric);

        result.Diagnostics.Should().ContainSingle(d => d.Descriptor.Id == "BGV113");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Invalid_warning_percent_reports_BGV114(double value)
    {
        var metric = ValidMetric with { WarningPercent = value, FailurePercent = double.IsNaN(value) || double.IsInfinity(value) ? 10 : value + 5 };
        var result = ValidateSingleMetric(metric);

        result.Diagnostics.Should().ContainSingle(d => d.Descriptor.Id == "BGV114");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Invalid_failure_percent_reports_BGV115(double value)
    {
        var metric = ValidMetric with { FailurePercent = value };
        var result = ValidateSingleMetric(metric);

        result.Diagnostics.Should().ContainSingle(d => d.Descriptor.Id == "BGV115");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Invalid_minimum_absolute_change_reports_BGV116(double value)
    {
        var metric = ValidMetric with { MinimumAbsoluteChange = value };
        var result = ValidateSingleMetric(metric);

        result.Diagnostics.Should().ContainSingle(d => d.Descriptor.Id == "BGV116");
    }

    [Fact]
    public void Warning_not_less_than_failure_reports_BGV117()
    {
        var metric = ValidMetric with { WarningPercent = 10, FailurePercent = 10 };
        var result = ValidateSingleMetric(metric);

        result.Diagnostics.Should().ContainSingle(d => d.Descriptor.Id == "BGV117");
    }

    [Fact]
    public void Warning_greater_than_failure_reports_BGV117()
    {
        var metric = ValidMetric with { WarningPercent = 20, FailurePercent = 10 };
        var result = ValidateSingleMetric(metric);

        result.Diagnostics.Should().ContainSingle(d => d.Descriptor.Id == "BGV117");
    }

    [Fact]
    public void Cross_field_check_is_skipped_when_warning_percent_is_already_invalid()
    {
        // warning is invalid (-1) and, taken at face value, -1 >= -2 would
        // also look like a threshold violation — but BGV117 must not fire
        // on top of an already-invalid operand.
        var metric = ValidMetric with { WarningPercent = -1, FailurePercent = -2 };
        var result = ValidateSingleMetric(metric);

        result.Diagnostics.Should().Contain(d => d.Descriptor.Id == "BGV114"); // invalid warningPercent
        result.Diagnostics.Should().Contain(d => d.Descriptor.Id == "BGV115"); // invalid failurePercent
        result.Diagnostics.Should().NotContain(d => d.Descriptor.Id == "BGV117");
    }

    [Fact]
    public void Cross_field_check_is_skipped_when_failure_percent_is_already_invalid()
    {
        var metric = ValidMetric with { WarningPercent = 5, FailurePercent = double.NaN };
        var result = ValidateSingleMetric(metric);

        result.Diagnostics.Should().Contain(d => d.Descriptor.Id == "BGV115");
        result.Diagnostics.Should().NotContain(d => d.Descriptor.Id == "BGV117");
    }

    [Fact]
    public void Multiple_unrelated_failures_are_all_reported_in_one_pass()
    {
        var metrics = new Dictionary<string, MetricDefinition?>
        {
            ["meanNanoseconds"] = ValidMetric with { WarningPercent = 10, FailurePercent = 10 },
            ["allocatedBytesPerOperation"] = null,
        };
        var stability = ValidStability with { MinimumMeasurements = 0 };
        var document = ValidDocument(schemaVersion: 99, stability: stability, metrics: metrics);

        var result = PolicyValidator.Validate(document);

        result.Diagnostics.Select(d => d.Descriptor.Id).Should().BeEquivalentTo(
            ["BGV101", "BGV105", "BGV109", "BGV117"]);
    }

    [Theory]
    [InlineData("runtime/mean", "/metrics/runtime~1mean")]
    [InlineData("weird~name", "/metrics/weird~0name")]
    [InlineData("both/and~", "/metrics/both~1and~0")]
    [InlineData("meanNanoseconds", "/metrics/meanNanoseconds")]
    public void Metric_names_are_escaped_as_json_pointer_segments(string metricName, string expectedPath)
    {
        var metrics = new Dictionary<string, MetricDefinition?> { [metricName] = ValidMetric with { Direction = null } };
        var result = PolicyValidator.Validate(ValidDocument(metrics: metrics));

        result.Diagnostics.Should().ContainSingle(d => d.Descriptor.Id == "BGV110" && d.Path == $"{expectedPath}/direction");
    }

    [Fact]
    public void Diagnostic_order_is_deterministic_across_repeated_runs()
    {
        var document = new PolicyDocument(SchemaVersion: null, Stability: null, Metrics: null);

        var first = PolicyValidator.Validate(document).Diagnostics.Select(d => d.Descriptor.Id).ToList();
        var second = PolicyValidator.Validate(document).Diagnostics.Select(d => d.Descriptor.Id).ToList();

        first.Should().Equal(second);
    }

    [Fact]
    public void All_policy_diagnostic_ids_are_unique()
    {
        var ids = PolicyValidatorDiagnostics.All.Select(d => d.Id).ToList();

        ids.Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [MemberData(nameof(AllPolicyDescriptors))]
    public void All_policy_diagnostic_ids_match_the_BGV1_convention(DiagnosticDescriptor descriptor)
    {
        descriptor.Id.Should().MatchRegex("^BGV1\\d{2}$");
    }

    public static IEnumerable<object[]> AllPolicyDescriptors() =>
        PolicyValidatorDiagnostics.All.Select(d => new object[] { d });

    private static ValidationResult ValidateSingleMetric(MetricDefinition metric)
    {
        var metrics = new Dictionary<string, MetricDefinition?> { ["meanNanoseconds"] = metric };
        return PolicyValidator.Validate(ValidDocument(metrics: metrics));
    }
}
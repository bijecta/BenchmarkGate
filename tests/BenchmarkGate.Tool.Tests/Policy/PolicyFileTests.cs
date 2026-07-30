using System.Text;
using Bijecta.BenchmarkGate.Core.Evaluation;
using Bijecta.BenchmarkGate.Tool.Policy;
using FluentAssertions;

namespace Bijecta.BenchmarkGate.Tool.Tests.Policy;

public sealed class PolicyFileTests : IDisposable
{
    private readonly List<string> _temporaryFiles = [];

    private string WriteTempPolicy(string json)
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, json, Encoding.UTF8);
        _temporaryFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var path in _temporaryFiles)
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
                // Test cleanup must not hide the actual test result.
            }
            catch (UnauthorizedAccessException)
            {
                // Same principle: best-effort cleanup.
            }
        }
    }

    private const string ValidLowerIsBetter = """
        {
          "schemaVersion": 1,
          "stability": { "minimumMeasurements": 10, "maximumCoefficientOfVariation": 0.05 },
          "metrics": {
            "meanNanoseconds": { "direction": "lower-is-better", "warningPercent": 7.5, "failurePercent": 15, "minimumAbsoluteChange": 100 }
          }
        }
        """;

    private const string ValidHigherIsBetter = """
        {
          "schemaVersion": 1,
          "stability": { "minimumMeasurements": 10, "maximumCoefficientOfVariation": 0.05 },
          "metrics": {
            "operationsPerSecond": { "direction": "higher-is-better", "warningPercent": 5, "failurePercent": 10 }
          }
        }
        """;

    [Fact]
    public void Missing_file_throws()
    {
        var act = () => PolicyFile.Load(Path.Combine(Path.GetTempPath(), $"does-not-exist-{Guid.NewGuid():N}.json"));
        act.Should().Throw<PolicyFileException>().WithMessage("*does not exist*");
    }

    [Fact]
    public void Exception_exposes_source_file()
    {
        var path = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.json");

        var act = () => PolicyFile.Load(path);

        act.Should().Throw<PolicyFileException>()
            .Which.SourceFile.Should().Be(path);

        act.Should().Throw<PolicyFileException>()
            .WithMessage($"*source file: '{path}'*");
    }

    [Fact]
    public void Malformed_json_throws()
    {
        var path = WriteTempPolicy("{ not valid json");
        var act = () => PolicyFile.Load(path);
        act.Should().Throw<PolicyFileException>().WithMessage("*invalid JSON syntax*");
    }

    [Fact]
    public void Json_null_throws()
    {
        var path = WriteTempPolicy("null");
        var act = () => PolicyFile.Load(path);
        act.Should().Throw<PolicyFileException>().WithMessage("*deserialized to null*");
    }

    [Fact]
    public void Unsupported_schema_version_throws()
    {
        var path = WriteTempPolicy("""{ "schemaVersion": 99, "stability": {}, "metrics": {} }""");
        var act = () => PolicyFile.Load(path);
        act.Should().Throw<PolicyFileException>().WithMessage("*schemaVersion 99*");
    }

    [Fact]
    public void Missing_minimum_measurements_throws()
    {
        var path = WriteTempPolicy("""{ "schemaVersion": 1, "stability": {}, "metrics": {} }""");
        var act = () => PolicyFile.Load(path);
        act.Should().Throw<PolicyFileException>().WithMessage("*minimumMeasurements*");
    }

    [Fact]
    public void Missing_maximum_coefficient_of_variation_throws()
    {
        var path = WriteTempPolicy("""
            { "schemaVersion": 1, "stability": { "minimumMeasurements": 10 }, "metrics": {} }
            """);
        var act = () => PolicyFile.Load(path);
        act.Should().Throw<PolicyFileException>().WithMessage("*maximumCoefficientOfVariation*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Zero_or_negative_minimum_measurements_throws(int minimumMeasurements)
    {
        var path = WriteTempPolicy($$"""
            { "schemaVersion": 1, "stability": { "minimumMeasurements": {{minimumMeasurements}}, "maximumCoefficientOfVariation": 0.05 }, "metrics": {} }
            """);
        var act = () => PolicyFile.Load(path);
        act.Should().Throw<PolicyFileException>().WithMessage("*minimumMeasurements*greater than zero*");
    }

    [Fact]
    public void Negative_coefficient_of_variation_throws()
    {
        var path = WriteTempPolicy("""
            { "schemaVersion": 1, "stability": { "minimumMeasurements": 10, "maximumCoefficientOfVariation": -0.2 }, "metrics": {} }
            """);
        var act = () => PolicyFile.Load(path);
        act.Should().Throw<PolicyFileException>().WithMessage("*maximumCoefficientOfVariation*finite, non-negative*");
    }

    [Fact]
    public void Missing_metrics_throws()
    {
        var path = WriteTempPolicy("""
            { "schemaVersion": 1, "stability": { "minimumMeasurements": 10, "maximumCoefficientOfVariation": 0.05 } }
            """);
        var act = () => PolicyFile.Load(path);
        act.Should().Throw<PolicyFileException>().WithMessage("*at least one entry*");
    }

    [Fact]
    public void Empty_metrics_throws()
    {
        var path = WriteTempPolicy("""
            { "schemaVersion": 1, "stability": { "minimumMeasurements": 10, "maximumCoefficientOfVariation": 0.05 }, "metrics": {} }
            """);
        var act = () => PolicyFile.Load(path);
        act.Should().Throw<PolicyFileException>().WithMessage("*at least one entry*");
    }

    [Fact]
    public void Empty_metric_name_throws()
    {
        var path = WriteTempPolicy("""
            { "schemaVersion": 1, "stability": { "minimumMeasurements": 10, "maximumCoefficientOfVariation": 0.05 },
              "metrics": { "": { "direction": "lower-is-better", "warningPercent": 5, "failurePercent": 10 } } }
            """);
        var act = () => PolicyFile.Load(path);
        act.Should().Throw<PolicyFileException>().WithMessage("*empty metric name*");
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("   ")]
    public void Whitespace_metric_name_throws(string metricName)
    {
        var path = WriteTempPolicy($$"""
            { "schemaVersion": 1, "stability": { "minimumMeasurements": 10, "maximumCoefficientOfVariation": 0.05 },
              "metrics": { "{{metricName}}": { "direction": "lower-is-better", "warningPercent": 5, "failurePercent": 10 } } }
            """);
        var act = () => PolicyFile.Load(path);
        act.Should().Throw<PolicyFileException>().WithMessage("*empty metric name*");
    }

    [Fact]
    public void Missing_direction_throws()
    {
        var path = WriteTempPolicy("""
            { "schemaVersion": 1, "stability": { "minimumMeasurements": 10, "maximumCoefficientOfVariation": 0.05 },
              "metrics": { "meanNanoseconds": { "warningPercent": 5, "failurePercent": 10 } } }
            """);
        var act = () => PolicyFile.Load(path);
        act.Should().Throw<PolicyFileException>().WithMessage("*missing 'direction'*");
    }

    [Fact]
    public void Unknown_direction_throws()
    {
        var path = WriteTempPolicy("""
            { "schemaVersion": 1, "stability": { "minimumMeasurements": 10, "maximumCoefficientOfVariation": 0.05 },
              "metrics": { "meanNanoseconds": { "direction": "sideways", "warningPercent": 5, "failurePercent": 10 } } }
            """);
        var act = () => PolicyFile.Load(path);
        act.Should().Throw<PolicyFileException>().WithMessage("*unrecognized 'direction'*");
    }

    [Fact]
    public void Direction_is_case_sensitive()
    {
        var path = WriteTempPolicy("""
            { "schemaVersion": 1, "stability": { "minimumMeasurements": 10, "maximumCoefficientOfVariation": 0.05 },
              "metrics": { "meanNanoseconds": { "direction": "Lower-Is-Better", "warningPercent": 5, "failurePercent": 10 } } }
            """);
        var act = () => PolicyFile.Load(path);
        act.Should().Throw<PolicyFileException>().WithMessage("*unrecognized 'direction'*");
    }

    [Fact]
    public void Missing_warning_threshold_throws()
    {
        var path = WriteTempPolicy("""
            { "schemaVersion": 1, "stability": { "minimumMeasurements": 10, "maximumCoefficientOfVariation": 0.05 },
              "metrics": { "meanNanoseconds": { "direction": "lower-is-better", "failurePercent": 10 } } }
            """);
        var act = () => PolicyFile.Load(path);
        act.Should().Throw<PolicyFileException>().WithMessage("*missing 'warningPercent'*");
    }

    [Fact]
    public void Missing_failure_threshold_throws()
    {
        var path = WriteTempPolicy("""
            { "schemaVersion": 1, "stability": { "minimumMeasurements": 10, "maximumCoefficientOfVariation": 0.05 },
              "metrics": { "meanNanoseconds": { "direction": "lower-is-better", "warningPercent": 5 } } }
            """);
        var act = () => PolicyFile.Load(path);
        act.Should().Throw<PolicyFileException>().WithMessage("*missing 'failurePercent'*");
    }

    [Fact]
    public void Negative_warning_threshold_throws()
    {
        var path = WriteTempPolicy("""
            { "schemaVersion": 1, "stability": { "minimumMeasurements": 10, "maximumCoefficientOfVariation": 0.05 },
              "metrics": { "meanNanoseconds": { "direction": "lower-is-better", "warningPercent": -5, "failurePercent": 10 } } }
            """);
        var act = () => PolicyFile.Load(path);
        act.Should().Throw<PolicyFileException>().WithMessage("*invalid warningPercent*");
    }

    [Fact]
    public void Negative_failure_threshold_throws()
    {
        var path = WriteTempPolicy("""
            { "schemaVersion": 1, "stability": { "minimumMeasurements": 10, "maximumCoefficientOfVariation": 0.05 },
              "metrics": { "meanNanoseconds": { "direction": "lower-is-better", "warningPercent": 5, "failurePercent": -10 } } }
            """);
        var act = () => PolicyFile.Load(path);
        act.Should().Throw<PolicyFileException>().WithMessage("*invalid failurePercent*");
    }

    [Fact]
    public void Warning_equal_to_failure_throws()
    {
        var path = WriteTempPolicy("""
            { "schemaVersion": 1, "stability": { "minimumMeasurements": 10, "maximumCoefficientOfVariation": 0.05 },
              "metrics": { "meanNanoseconds": { "direction": "lower-is-better", "warningPercent": 10, "failurePercent": 10 } } }
            """);
        var act = () => PolicyFile.Load(path);
        act.Should().Throw<PolicyFileException>().WithMessage("*warningPercent*>=*failurePercent*");
    }

    [Fact]
    public void Warning_greater_than_failure_throws()
    {
        var path = WriteTempPolicy("""
            { "schemaVersion": 1, "stability": { "minimumMeasurements": 10, "maximumCoefficientOfVariation": 0.05 },
              "metrics": { "meanNanoseconds": { "direction": "lower-is-better", "warningPercent": 20, "failurePercent": 10 } } }
            """);
        var act = () => PolicyFile.Load(path);
        act.Should().Throw<PolicyFileException>().WithMessage("*warningPercent*>=*failurePercent*");
    }

    [Fact]
    public void Zero_warning_threshold_is_allowed()
    {
        // Rules only require warningPercent < failurePercent and both
        // non-negative — a zero warning threshold (flag any regression at
        // all as at least a Warning) is intentionally legal.
        var path = WriteTempPolicy("""
            { "schemaVersion": 1, "stability": { "minimumMeasurements": 10, "maximumCoefficientOfVariation": 0.05 },
              "metrics": { "meanNanoseconds": { "direction": "lower-is-better", "warningPercent": 0, "failurePercent": 10 } } }
            """);

        var policy = PolicyFile.Load(path);

        policy.Metrics["meanNanoseconds"].WarningPercent.Should().Be(0);
    }

    [Fact]
    public void Zero_failure_threshold_is_rejected_by_the_ordering_rule()
    {
        // failurePercent must be strictly greater than warningPercent, and
        // warningPercent must be >= 0 — so failurePercent = 0 can never
        // pass validation (0 >= 0 always fails "warning < failure").
        var path = WriteTempPolicy("""
            { "schemaVersion": 1, "stability": { "minimumMeasurements": 10, "maximumCoefficientOfVariation": 0.05 },
              "metrics": { "meanNanoseconds": { "direction": "lower-is-better", "warningPercent": 0, "failurePercent": 0 } } }
            """);
        var act = () => PolicyFile.Load(path);
        act.Should().Throw<PolicyFileException>().WithMessage("*warningPercent*>=*failurePercent*");
    }

    [Fact]
    public void Negative_minimum_absolute_change_throws()
    {
        var path = WriteTempPolicy("""
            { "schemaVersion": 1, "stability": { "minimumMeasurements": 10, "maximumCoefficientOfVariation": 0.05 },
              "metrics": { "meanNanoseconds": { "direction": "lower-is-better", "warningPercent": 5, "failurePercent": 10, "minimumAbsoluteChange": -100 } } }
            """);
        var act = () => PolicyFile.Load(path);
        act.Should().Throw<PolicyFileException>().WithMessage("*invalid minimumAbsoluteChange*");
    }

    [Fact]
    public void Unknown_json_property_throws_under_strict_mode()
    {
        var path = WriteTempPolicy("""
        { "schemaVersion": 1, "stability": { "minimumMeasurements": 10, "maximumCoefficientOfVariation": 0.05 },
          "metrics": { "meanNanoseconds": { "direction": "lower-is-better", "warningPercent": 5, "failurePercent": 10, "warningPercnt": 7 } } }
        """);

        var act = () => PolicyFile.Load(path);

        var exception = act.Should().Throw<PolicyFileException>()
            .WithMessage("*invalid JSON syntax*")
            .Which;

        exception.InnerException.Should().BeOfType<System.Text.Json.JsonException>();
        exception.SourceFile.Should().Be(path);
    }

    [Fact]
    public void Valid_lower_is_better_policy_loads_successfully()
    {
        var path = WriteTempPolicy(ValidLowerIsBetter);

        var policy = PolicyFile.Load(path);

        policy.Stability.MinimumMeasurements.Should().Be(10);
        policy.Stability.MaximumCoefficientOfVariation.Should().Be(0.05);
        policy.Metrics.Should().ContainKey("meanNanoseconds");
        policy.Metrics["meanNanoseconds"].Direction.Should().Be(MetricDirection.LowerIsBetter);
        policy.Metrics["meanNanoseconds"].WarningPercent.Should().Be(7.5);
        policy.Metrics["meanNanoseconds"].FailurePercent.Should().Be(15);
        policy.Metrics["meanNanoseconds"].MinimumAbsoluteChange.Should().Be(100);
    }

    [Fact]
    public void Valid_higher_is_better_policy_loads_successfully()
    {
        var path = WriteTempPolicy(ValidHigherIsBetter);

        var policy = PolicyFile.Load(path);

        policy.Metrics["operationsPerSecond"].Direction.Should().Be(MetricDirection.HigherIsBetter);
        // minimumAbsoluteChange omitted in the JSON -> defaults to 0.
        policy.Metrics["operationsPerSecond"].MinimumAbsoluteChange.Should().Be(0);
    }

    [Fact]
    public void Multiple_metric_policies_load_successfully()
    {
        var path = WriteTempPolicy("""
            { "schemaVersion": 1, "stability": { "minimumMeasurements": 10, "maximumCoefficientOfVariation": 0.05 },
              "metrics": {
                "meanNanoseconds": { "direction": "lower-is-better", "warningPercent": 5, "failurePercent": 10, "minimumAbsoluteChange": 100 },
                "operationsPerSecond": { "direction": "higher-is-better", "warningPercent": 3, "failurePercent": 8 }
              } }
            """);

        var policy = PolicyFile.Load(path);

        policy.Metrics.Should().HaveCount(2);
        policy.Metrics["meanNanoseconds"].Direction.Should().Be(MetricDirection.LowerIsBetter);
        policy.Metrics["operationsPerSecond"].Direction.Should().Be(MetricDirection.HigherIsBetter);
    }

    [Fact]
    public void Validation_failure_exposes_structured_validation_result()
    {
        var path = WriteTempPolicy("""
            { "schemaVersion": 1, "stability": { "minimumMeasurements": 0, "maximumCoefficientOfVariation": 0.05 }, "metrics": {} }
            """);

        var act = () => PolicyFile.Load(path);

        var exception = act.Should().Throw<PolicyFileException>().Which;
        exception.ValidationResult.Should().NotBeNull();
        exception.ValidationResult!.Diagnostics.Select(d => d.Descriptor.Id)
            .Should().BeEquivalentTo(["BGV105", "BGV107"]);
    }

    [Fact]
    public void File_access_failures_do_not_populate_validation_result()
    {
        var act = () => PolicyFile.Load(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.json"));

        act.Should().Throw<PolicyFileException>().Which.ValidationResult.Should().BeNull();
    }

    [Fact]
    public void Malformed_json_does_not_populate_validation_result()
    {
        var path = WriteTempPolicy("{ not valid json");

        var act = () => PolicyFile.Load(path);

        act.Should().Throw<PolicyFileException>().Which.ValidationResult.Should().BeNull();
    }

    [Fact]
    public void Null_metric_definition_reports_a_diagnostic_instead_of_throwing_unexpectedly()
    {
        var path = WriteTempPolicy("""
            { "schemaVersion": 1, "stability": { "minimumMeasurements": 10, "maximumCoefficientOfVariation": 0.05 },
              "metrics": { "meanNanoseconds": null } }
            """);

        var act = () => PolicyFile.Load(path);

        var exception = act.Should().Throw<PolicyFileException>().Which;
        exception.ValidationResult!.Diagnostics.Should().ContainSingle(d => d.Descriptor.Id == "BGV109");
    }

    [Fact]
    public void Exception_message_lists_multiple_diagnostics_one_per_line()
    {
        var path = WriteTempPolicy("""
            { "schemaVersion": 1, "stability": { "minimumMeasurements": 0, "maximumCoefficientOfVariation": 0.05 }, "metrics": {} }
            """);

        var act = () => PolicyFile.Load(path);

        var exception = act.Should().Throw<PolicyFileException>().Which;
        exception.Message.Should().Contain("contains 2 validation error(s)");
        exception.Message.Should().Contain("BGV105");
        exception.Message.Should().Contain("BGV107");
    }
}
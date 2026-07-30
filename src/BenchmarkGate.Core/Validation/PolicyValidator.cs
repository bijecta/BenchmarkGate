using Bijecta.BenchmarkGate.Core.Policy;

namespace Bijecta.BenchmarkGate.Core.Validation;

public static class PolicyValidator
{
    private const int SupportedSchemaVersion = 1;

    public static ValidationResult Validate(PolicyDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var diagnostics = new List<ValidationDiagnostic>();

        ValidateSchemaVersion(document.SchemaVersion, diagnostics);
        ValidateStability(document.Stability, diagnostics);
        ValidateMetrics(document.Metrics, diagnostics);

        return new ValidationResult(diagnostics);
    }

    private static void ValidateSchemaVersion(int? schemaVersion, List<ValidationDiagnostic> diagnostics)
    {
        if (schemaVersion is null)
        {
            diagnostics.Add(new ValidationDiagnostic(
                PolicyValidatorDiagnostics.MissingSchemaVersion, "/schemaVersion",
                "Policy is missing 'schemaVersion'."));
        }
        else if (schemaVersion.Value != SupportedSchemaVersion)
        {
            diagnostics.Add(new ValidationDiagnostic(
                PolicyValidatorDiagnostics.UnsupportedSchemaVersion, "/schemaVersion",
                FormattableString.Invariant(
                    $"Unsupported schemaVersion {schemaVersion.Value}. This build supports schemaVersion {SupportedSchemaVersion}.")));
        }
    }

    private static void ValidateStability(StabilityDefinition? stability, List<ValidationDiagnostic> diagnostics)
    {
        if (stability is null)
        {
            diagnostics.Add(new ValidationDiagnostic(
                PolicyValidatorDiagnostics.MissingStability, "/stability", "Policy is missing 'stability'."));
            return;
        }

        if (stability.MinimumMeasurements is null)
        {
            diagnostics.Add(new ValidationDiagnostic(
                PolicyValidatorDiagnostics.MissingMinimumMeasurements, "/stability/minimumMeasurements",
                "Policy's 'stability' is missing 'minimumMeasurements'."));
        }
        else if (stability.MinimumMeasurements.Value <= 0)
        {
            diagnostics.Add(new ValidationDiagnostic(
                PolicyValidatorDiagnostics.InvalidMinimumMeasurements, "/stability/minimumMeasurements",
                FormattableString.Invariant(
                    $"Value must be greater than zero; actual value was {stability.MinimumMeasurements.Value}.")));
        }

        if (stability.MaximumCoefficientOfVariation is null)
        {
            diagnostics.Add(new ValidationDiagnostic(
                PolicyValidatorDiagnostics.MissingMaximumCoefficientOfVariation,
                "/stability/maximumCoefficientOfVariation",
                "Policy's 'stability' is missing 'maximumCoefficientOfVariation'."));
        }
        else if (!double.IsFinite(stability.MaximumCoefficientOfVariation.Value) ||
                 stability.MaximumCoefficientOfVariation.Value < 0)
        {
            diagnostics.Add(new ValidationDiagnostic(
                 PolicyValidatorDiagnostics.InvalidMaximumCoefficientOfVariation,
                 "/stability/maximumCoefficientOfVariation",
                 FormattableString.Invariant(
                    $"Value must be a finite, non-negative number; actual value was {stability.MaximumCoefficientOfVariation.Value}.")));
        }
    }

    private static void ValidateMetrics(
        IReadOnlyDictionary<string, MetricDefinition?>? metrics,
        List<ValidationDiagnostic> diagnostics)
    {
        if (metrics is null || metrics.Count == 0)
        {
            diagnostics.Add(new ValidationDiagnostic(
                PolicyValidatorDiagnostics.MissingMetrics, "/metrics",
                "Policy must define at least one entry under 'metrics'."));
            return;
        }

        foreach (var (metricName, metric) in metrics)
        {
            var path = MetricPath(metricName);

            if (string.IsNullOrWhiteSpace(metricName))
            {
                diagnostics.Add(new ValidationDiagnostic(
                    PolicyValidatorDiagnostics.EmptyMetricName, path, "Policy contains an empty metric name."));
            }

            if (metric is null)
            {
                diagnostics.Add(new ValidationDiagnostic(
                    PolicyValidatorDiagnostics.MissingMetricDefinition, path,
                    $"Metric '{metricName}' has no definition."));
                continue;
            }

            ValidateMetric(metricName, path, metric, diagnostics);
        }
    }

    private static void ValidateMetric(
        string metricName, string path, MetricDefinition metric, List<ValidationDiagnostic> diagnostics)
    {
        if (metric.Direction is null)
        {
            diagnostics.Add(new ValidationDiagnostic(
                PolicyValidatorDiagnostics.MissingDirection, $"{path}/direction",
                $"Metric '{metricName}' is missing 'direction'."));
        }
        else if (metric.Direction is not ("lower-is-better" or "higher-is-better"))
        {
            diagnostics.Add(new ValidationDiagnostic(
                PolicyValidatorDiagnostics.UnrecognizedDirection, $"{path}/direction",
                $"Metric '{metricName}' has an unrecognized 'direction' value '{metric.Direction}'. " +
                "Expected 'lower-is-better' or 'higher-is-better'."));
        }

        var warningIsValid = metric.WarningPercent is { } warning && double.IsFinite(warning) && warning >= 0;
        var failureIsValid = metric.FailurePercent is { } failure && double.IsFinite(failure) && failure >= 0;

        if (metric.WarningPercent is null)
        {
            diagnostics.Add(new ValidationDiagnostic(
                PolicyValidatorDiagnostics.MissingWarningPercent, $"{path}/warningPercent",
                    $"Metric '{metricName}' is missing 'warningPercent'."));
        }
        else if (!warningIsValid)
        {
            diagnostics.Add(new ValidationDiagnostic(
                PolicyValidatorDiagnostics.InvalidWarningPercent, $"{path}/warningPercent",
                FormattableString.Invariant(
                    $"Metric '{metricName}' has invalid warningPercent ({metric.WarningPercent.Value}). It must be a finite, non-negative number.")));
        }

        if (metric.FailurePercent is null)
        {
            diagnostics.Add(new ValidationDiagnostic(
                PolicyValidatorDiagnostics.MissingFailurePercent, $"{path}/failurePercent",
                    $"Metric '{metricName}' is missing 'failurePercent'."));
        }
        else if (!failureIsValid)
        {
            diagnostics.Add(new ValidationDiagnostic(
                PolicyValidatorDiagnostics.InvalidFailurePercent, $"{path}/failurePercent",
                FormattableString.Invariant(
                    $"Metric '{metricName}' has invalid failurePercent ({metric.FailurePercent.Value}). It must be a finite, non-negative number.")));
        }

        var minimumAbsoluteChange = metric.MinimumAbsoluteChange ?? 0;
        if (!double.IsFinite(minimumAbsoluteChange) || minimumAbsoluteChange < 0)
        {
            diagnostics.Add(new ValidationDiagnostic(
                PolicyValidatorDiagnostics.InvalidMinimumAbsoluteChange, $"{path}/minimumAbsoluteChange",
                FormattableString.Invariant(
                    $"Metric '{metricName}' has invalid minimumAbsoluteChange ({minimumAbsoluteChange}). It must be a finite, non-negative number.")));
        }

        // Cross-field check only runs once both operands are individually valid —
        // avoids a cascading, redundant diagnostic on top of an already-reported
        // invalid warningPercent/failurePercent.
        if (warningIsValid && failureIsValid && metric.WarningPercent!.Value >= metric.FailurePercent!.Value)
        {
            diagnostics.Add(new ValidationDiagnostic(
                PolicyValidatorDiagnostics.WarningNotLessThanFailure, path,
                FormattableString.Invariant(
                    $"Metric '{metricName}' has warningPercent ({metric.WarningPercent.Value}) >= failurePercent ({metric.FailurePercent.Value}). warningPercent must be strictly less than failurePercent for the policy to be meaningful.")));
        }
    }

    /// <summary>
    /// Builds a JSON Pointer path for a metric, escaping '~' and '/' per
    /// RFC 6901 (~0, ~1) so metric names containing those characters
    /// (e.g. "runtime/mean") still produce a path that correctly
    /// identifies the property.
    /// </summary>
    private static string MetricPath(string metricName)
    {
        if (string.IsNullOrWhiteSpace(metricName))
            return "/metrics";

        var escaped = metricName
            .Replace("~", "~0", StringComparison.Ordinal)
            .Replace("/", "~1", StringComparison.Ordinal);

        return $"/metrics/{escaped}";
    }
}
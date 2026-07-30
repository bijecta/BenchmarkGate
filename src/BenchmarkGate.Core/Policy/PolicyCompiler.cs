using Bijecta.BenchmarkGate.Core.Evaluation;

namespace Bijecta.BenchmarkGate.Core.Policy;

public static class PolicyCompiler
{
    /// <exception cref="ArgumentException">
    /// The document does not satisfy the structural preconditions
    /// PolicyValidator.Validate would have required.
    /// </exception>
    public static GatePolicy CompileValidated(PolicyDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.Stability?.MinimumMeasurements is null ||
            document.Stability.MaximumCoefficientOfVariation is null ||
            document.Metrics is null || document.Metrics.Count == 0)
        {
            throw new ArgumentException(
                "The policy document must pass PolicyValidator.Validate before compilation.",
                nameof(document));
        }

        var metrics = new Dictionary<string, MetricPolicy>(StringComparer.Ordinal);
        foreach (var (metricName, metric) in document.Metrics)
        {
            if (metric is null ||
                metric.Direction is null ||
                metric.WarningPercent is null ||
                metric.FailurePercent is null)
            {
                throw new ArgumentException(
                    $"Metric '{metricName}' does not satisfy the structural preconditions. " +
                    "The document must pass PolicyValidator.Validate before compilation.",
                    nameof(document));
            }

            metrics[metricName] = new MetricPolicy
            {
                Direction = metric.Direction switch
                {
                    "lower-is-better" => MetricDirection.LowerIsBetter,
                    "higher-is-better" => MetricDirection.HigherIsBetter,
                    _ => throw new ArgumentException(
                        $"Metric '{metricName}' has an unrecognized direction. " +
                        "The document must pass PolicyValidator.Validate before compilation.",
                        nameof(document)),
                },
                WarningPercent = metric.WarningPercent.Value,
                FailurePercent = metric.FailurePercent.Value,
                MinimumAbsoluteChange = metric.MinimumAbsoluteChange ?? 0,
            };
        }

        return new GatePolicy
        {
            Stability = new StabilityPolicy
            {
                MinimumMeasurements = document.Stability.MinimumMeasurements.Value,
                MaximumCoefficientOfVariation = document.Stability.MaximumCoefficientOfVariation.Value,
            },
            Metrics = metrics,
        };
    }
}
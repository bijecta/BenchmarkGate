using System.Globalization;
using Bijecta.BenchmarkGate.Core.Baseline;
using Bijecta.BenchmarkGate.Core.Model;

namespace Bijecta.BenchmarkGate.Core.Evaluation;

/// <summary>
/// Compares a set of current observations against an approved baseline
/// under a gate policy. Pure: no I/O, no console output, no process
/// termination — see ADR-0001.
/// </summary>
public static class RegressionEvaluator
{
    public static SuiteDecision Evaluate(
        IReadOnlyList<BenchmarkObservation> observations,
        BenchmarkBaseline baseline,
        GatePolicy policy)
    {
        var decisions = new List<BenchmarkDecision>();
        var seenBaselineIdentities = new HashSet<string>(StringComparer.Ordinal);

        foreach (var observation in observations)
        {
            var baselineEntry = baseline.TryFind(observation.Identity);

            if (baselineEntry is null)
            {
                decisions.Add(new BenchmarkDecision(
                    observation.Identity,
                    BenchmarkGateStatus.New,
                    Metrics: [],
                    Explanation: "No baseline entry exists for this benchmark."));
                continue;
            }

            seenBaselineIdentities.Add(baselineEntry.Identity.CanonicalString);
            decisions.Add(EvaluateAgainstBaseline(observation, baselineEntry, policy));
        }

        // Anything in the baseline that wasn't matched by a current
        // observation is Missing.
        foreach (var baselineEntry in baseline.Benchmarks)
        {
            if (seenBaselineIdentities.Contains(baselineEntry.Identity.CanonicalString))
                continue;

            decisions.Add(new BenchmarkDecision(
                baselineEntry.Identity,
                BenchmarkGateStatus.Missing,
                Metrics: [],
                Explanation: "This benchmark exists in the baseline but was not present in the current results."));
        }

        return new SuiteDecision(decisions);
    }

    private static BenchmarkDecision EvaluateAgainstBaseline(
        BenchmarkObservation observation,
        BaselineEntry baselineEntry,
        GatePolicy policy)
    {
        // Stability gates the whole benchmark before any metric is compared:
        // a noisy sample can't say anything trustworthy either way, so we
        // don't want a coincidentally-passing metric to mask instability.
        if (IsUnstable(observation, policy.Stability, out var stabilityExplanation))
        {
            return new BenchmarkDecision(
                observation.Identity,
                BenchmarkGateStatus.Unstable,
                Metrics: [],
                stabilityExplanation);
        }

        var metricDecisions = new List<MetricDecision>();

        foreach (var (metricName, metricPolicy) in policy.Metrics)
        {
            // A metric absent from either side isn't evaluated — it just
            // wasn't measured (e.g. no MemoryDiagnoser enabled), not a
            // failure and not something to fabricate a comparison for.
            if (!observation.Metrics.TryGetValue(metricName, out var currentValue) ||
                !baselineEntry.Metrics.TryGetValue(metricName, out var baselineValue))
            {
                continue;
            }

            metricDecisions.Add(EvaluateMetric(metricName, baselineValue, currentValue, metricPolicy));
        }

        var aggregateStatus = AggregateStatus(metricDecisions);
        var explanation = metricDecisions.Count == 0
            ? "No metrics from the policy were present in both the baseline and current observation."
            : string.Join(" ", metricDecisions.Select(m => m.Explanation));

        return new BenchmarkDecision(observation.Identity, aggregateStatus, metricDecisions, explanation);
    }

    private static bool IsUnstable(
        BenchmarkObservation observation,
        StabilityPolicy stability,
        out string explanation)
    {
        if (observation.MeasurementCount < stability.MinimumMeasurements)
        {
            explanation = string.Create(CultureInfo.InvariantCulture,
                $"Only {observation.MeasurementCount} measurements were taken, " +
                $"below the configured minimum of {stability.MinimumMeasurements}.");
            return true;
        }

        if (!observation.Metrics.TryGetValue(BenchmarkObservation.MeanNanosecondsMetric, out var mean) || mean == 0)
        {
            // No mean-time metric to compute a coefficient of variation
            // against — nothing to gate on, so treat as stable and let the
            // per-metric loop evaluate whatever metrics ARE present.
            explanation = string.Empty;
            return false;
        }

        var coefficientOfVariation = observation.StandardDeviationNanoseconds / mean;
        if (coefficientOfVariation > stability.MaximumCoefficientOfVariation)
        {
            explanation = string.Create(CultureInfo.InvariantCulture,
                $"Coefficient of variation {coefficientOfVariation:P2} exceeds the configured " +
                $"maximum of {stability.MaximumCoefficientOfVariation:P2} " +
                $"(stddev {FormatNanoseconds(observation.StandardDeviationNanoseconds)} " +
                $"over mean {FormatNanoseconds(mean)}).");
            return true;
        }

        explanation = string.Empty;
        return false;
    }

    private static MetricDecision EvaluateMetric(
        string metricName,
        double baselineValue,
        double currentValue,
        MetricPolicy policy)
    {
        var formatter = MetricFormatters.For(metricName);
        var absoluteDelta = currentValue - baselineValue;

        double relativeDeltaPercent;
        if (baselineValue == 0)
        {
            var movedWorse = policy.Direction == MetricDirection.LowerIsBetter
                ? currentValue > 0
                : currentValue < 0;
            relativeDeltaPercent = movedWorse ? double.PositiveInfinity : 0;
        }
        else
        {
            var rawPercent = absoluteDelta / baselineValue * 100.0;
            relativeDeltaPercent = policy.Direction == MetricDirection.LowerIsBetter
                ? rawPercent
                : -rawPercent;
        }

        var absoluteChangeMeetsFloor = Math.Abs(absoluteDelta) >= policy.MinimumAbsoluteChange;

        BenchmarkGateStatus status;
        string explanation;

        if (relativeDeltaPercent >= policy.FailurePercent && absoluteChangeMeetsFloor)
        {
            status = BenchmarkGateStatus.Regressed;
            explanation = string.Create(CultureInfo.InvariantCulture,
                $"{metricName} regressed by {relativeDeltaPercent:F2}% " +
                $"({formatter.Format(baselineValue)} -> {formatter.Format(currentValue)}), " +
                $">= failure threshold of {policy.FailurePercent:F2}%.");
        }
        else if (relativeDeltaPercent >= policy.WarningPercent && absoluteChangeMeetsFloor)
        {
            status = BenchmarkGateStatus.Warning;
            explanation = string.Create(CultureInfo.InvariantCulture,
                $"{metricName} regressed by {relativeDeltaPercent:F2}% " +
                $"({formatter.Format(baselineValue)} -> {formatter.Format(currentValue)}), " +
                $">= warning threshold of {policy.WarningPercent:F2}% but below failure threshold.");
        }
        else if (relativeDeltaPercent <= -policy.WarningPercent && absoluteChangeMeetsFloor)
        {
            status = BenchmarkGateStatus.Improved;
            explanation = string.Create(CultureInfo.InvariantCulture,
                $"{metricName} improved by {Math.Abs(relativeDeltaPercent):F2}% " +
                $"({formatter.Format(baselineValue)} -> {formatter.Format(currentValue)}).");
        }
        else
        {
            status = BenchmarkGateStatus.Passed;
            explanation = string.Create(CultureInfo.InvariantCulture,
                $"{metricName} changed by {relativeDeltaPercent:F2}% " +
                $"({formatter.Format(baselineValue)} -> {formatter.Format(currentValue)}), " +
                $"within the configured warning threshold of {policy.WarningPercent:F2}%.");
        }

        return new MetricDecision(metricName, status, baselineValue, currentValue, absoluteDelta, relativeDeltaPercent, explanation);
    }

    /// <summary>
    /// Worst-wins across a benchmark's per-metric decisions:
    /// Regressed > Warning > Missing > New > Improved > Passed.
    /// (Unstable is handled earlier and never reaches this function.)
    /// </summary>
    private static BenchmarkGateStatus AggregateStatus(List<MetricDecision> metrics)
    {
        if (metrics.Count == 0) return BenchmarkGateStatus.Passed;
        if (metrics.Any(m => m.Status == BenchmarkGateStatus.Regressed)) return BenchmarkGateStatus.Regressed;
        if (metrics.Any(m => m.Status == BenchmarkGateStatus.Warning)) return BenchmarkGateStatus.Warning;
        if (metrics.Any(m => m.Status == BenchmarkGateStatus.Improved)) return BenchmarkGateStatus.Improved;
        return BenchmarkGateStatus.Passed;
    }

    /// <summary>
    /// Formats a metric value for embedding in explanation text. This
    /// intentionally duplicates similar formatting in
    /// <c>Bijecta.BenchmarkGate.Tool.Reporting.MarkdownBuilder</c> — Core must
    /// never depend on Tool (see ADR-0001's dependency direction), so a
    /// small amount of formatting duplication here is the correct tradeoff,
    /// not an oversight. Note: this assumes nanosecond-scale values: correct
    /// for meanNanoseconds, but allocatedBytesPerOperation values will print
    /// with the same ns/µs/ms unit suffixes, which is misleading. Flagging —
    /// this needs a per-metric-name formatter, not a single nanosecond
    /// formatter, before allocation numbers show up in real report output.
    /// </summary>
    private static string FormatNanoseconds(double nanoseconds) =>
        nanoseconds >= 1_000_000
            ? string.Create(CultureInfo.InvariantCulture, $"{nanoseconds / 1_000_000:F3} ms")
            : nanoseconds >= 1_000
                ? string.Create(CultureInfo.InvariantCulture, $"{nanoseconds / 1_000:F3} \u00b5s")
                : string.Create(CultureInfo.InvariantCulture, $"{nanoseconds:F3} ns");
}
using System.Diagnostics;
using System.Globalization;
using Bijecta.BenchmarkGate.Core.Comparison;
using Bijecta.BenchmarkGate.Core.Model;

namespace Bijecta.BenchmarkGate.Core.Evaluation;

/// <summary>
/// Applies a gate policy to a policy-free <see cref="ComparisonResult"/>,
/// producing a <see cref="SuiteDecision"/>. Pure: no I/O, no console
/// output, no process termination — see ADR-0001.
/// </summary>
/// <remarks>
/// Benchmark matching, metric matching, descriptor lookup, and delta
/// calculation all live in <c>BenchmarkComparisonEngine</c> — this type
/// never duplicates any of it, including arithmetic: it consumes
/// <see cref="MetricComparison.AbsoluteDelta"/>/<see cref="MetricComparison.PercentDelta"/>
/// as already-computed facts and interprets them under policy, it never
/// recomputes <c>candidate - reference</c> or a percentage itself.
/// </remarks>
public static class RegressionEvaluator
{
    public static SuiteDecision Evaluate(ComparisonResult comparison, GatePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(comparison);
        ArgumentNullException.ThrowIfNull(policy);

        var decisions = comparison.Benchmarks
            .Select(benchmark => EvaluateBenchmark(benchmark, policy))
            .ToList();

        return new SuiteDecision(decisions);
    }

    private static BenchmarkDecision EvaluateBenchmark(BenchmarkComparison benchmark, GatePolicy policy) =>
        benchmark.Status switch
        {
            BenchmarkComparisonStatus.Added => new BenchmarkDecision(
                benchmark.Identity, BenchmarkGateStatus.New, Metrics: [],
                Explanation: "No baseline entry exists for this benchmark."),

            BenchmarkComparisonStatus.Removed => new BenchmarkDecision(
                benchmark.Identity, BenchmarkGateStatus.Missing, Metrics: [],
                Explanation: "This benchmark exists in the baseline but was not present in the current results."),

            BenchmarkComparisonStatus.Comparable => EvaluateComparable(benchmark, policy),

            _ => throw new UnreachableException($"Unhandled {nameof(BenchmarkComparisonStatus)}: {benchmark.Status}"),
        };

    private static BenchmarkDecision EvaluateComparable(BenchmarkComparison benchmark, GatePolicy policy)
    {
        // Stability gates the whole benchmark before any metric is compared:
        // a noisy sample can't say anything trustworthy either way, so we
        // don't want a coincidentally-passing metric to mask instability.
        if (IsUnstable(benchmark, policy.Stability, out var stabilityExplanation))
        {
            return new BenchmarkDecision(benchmark.Identity, BenchmarkGateStatus.Unstable, Metrics: [], stabilityExplanation);
        }

        var metricDecisions = new List<MetricDecision>();

        foreach (var (metricName, metricPolicy) in policy.Metrics)
        {
            var metric = FindMetric(benchmark, metricName);

            // Only a Comparable metric carries the precomputed
            // AbsoluteDelta/PercentDelta this evaluator relies on — every
            // other status (missing on either side, unit mismatch,
            // non-finite value on either side) has them null by design
            // (see MetricComparison's remarks), so there is nothing here
            // to evaluate without recalculating arithmetic ourselves, which
            // this type must not do. This is a deliberate behavior change
            // from the pre-v0.4.0 evaluator for non-finite metric values —
            // see the #27 PR description.
            if (metric is null || metric.Status != MetricComparisonStatus.Comparable)
            {
                continue;
            }

            metricDecisions.Add(EvaluateMetric(metric, metricPolicy));
        }

        var aggregateStatus = AggregateStatus(metricDecisions);
        var explanation = metricDecisions.Count == 0
            ? "No metrics from the policy were present in both the baseline and current observation."
            : string.Join(" ", metricDecisions.Select(m => m.Explanation));

        return new BenchmarkDecision(benchmark.Identity, aggregateStatus, metricDecisions, explanation);
    }

    private static MetricComparison? FindMetric(BenchmarkComparison benchmark, string metricName) =>
        benchmark.Metrics.FirstOrDefault(m => string.Equals(m.MetricName, metricName, StringComparison.Ordinal));

    /// <summary>
    /// The candidate run's raw mean-time value, or null if it isn't
    /// available (structurally missing, or the benchmark has no
    /// meanNanoseconds entry at all). Named explicitly since the stability
    /// gate depends on it independently of whether meanNanoseconds is
    /// itself one of <see cref="GatePolicy.Metrics"/>.
    /// </summary>
    private static double? FindCandidateMean(BenchmarkComparison benchmark) =>
        FindMetric(benchmark, BenchmarkObservation.MeanNanosecondsMetric)?.Candidate?.Value;

    private static bool IsUnstable(BenchmarkComparison benchmark, StabilityPolicy stability, out string explanation)
    {
        var candidateStability = benchmark.CandidateStability;

        // Structurally shouldn't happen for a Comparable benchmark today —
        // BenchmarkComparisonEngine always populates CandidateStability for
        // Comparable/Added, only Removed leaves it null — but the field is
        // nullable, so this is handled defensively rather than assumed.
        if (candidateStability is null)
        {
            explanation = string.Empty;
            return false;
        }

        if (candidateStability.MeasurementCount < stability.MinimumMeasurements)
        {
            explanation = string.Create(CultureInfo.InvariantCulture,
                $"Only {candidateStability.MeasurementCount} measurements were taken, " +
                $"below the configured minimum of {stability.MinimumMeasurements}.");
            return true;
        }

        var mean = FindCandidateMean(benchmark);

        if (mean is null or 0d)
        {
            // No candidate mean to compute a coefficient of variation
            // against (either the metric is structurally missing from the
            // candidate, or it's exactly zero) — nothing to gate on, so
            // treat as stable and let the per-metric loop evaluate whatever
            // metrics ARE present. A non-finite (NaN/Infinity) mean is
            // deliberately NOT special-cased here — it falls through to the
            // same division below, exactly as the legacy evaluator did.
            explanation = string.Empty;
            return false;
        }

        var coefficientOfVariation = candidateStability.StandardDeviationNanoseconds / mean.Value;
        if (coefficientOfVariation > stability.MaximumCoefficientOfVariation)
        {
            explanation = string.Create(CultureInfo.InvariantCulture,
                $"Coefficient of variation {coefficientOfVariation:P2} exceeds the configured " +
                $"maximum of {stability.MaximumCoefficientOfVariation:P2} " +
                $"(stddev {FormatNanoseconds(candidateStability.StandardDeviationNanoseconds)} " +
                $"over mean {FormatNanoseconds(mean.Value)}).");
            return true;
        }

        explanation = string.Empty;
        return false;
    }

    /// <summary>
    /// Interprets a Comparable metric's precomputed <see cref="MetricComparison"/>
    /// facts under policy thresholds. Consumes <see cref="MetricComparison.AbsoluteDelta"/>
    /// and <see cref="MetricComparison.PercentDelta"/> directly — never
    /// recalculates <c>candidate - reference</c> or a percentage.
    /// </summary>
    private static MetricDecision EvaluateMetric(MetricComparison metric, MetricPolicy policy)
    {
        var metricName = metric.MetricName;
        var formatter = MetricFormatters.For(metricName);

        var baselineValue = metric.Reference?.Value
            ?? throw InvalidComparisonState(metric, nameof(metric.Reference));
        var currentValue = metric.Candidate?.Value
            ?? throw InvalidComparisonState(metric, nameof(metric.Candidate));
        var absoluteDelta = metric.AbsoluteDelta
            ?? throw InvalidComparisonState(metric, nameof(metric.AbsoluteDelta));

        var relativeDeltaPercent = ResolvePolicyRelativePercent(metric, policy);
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
    /// Turns a raw, direction-agnostic <see cref="MetricComparison.PercentDelta"/>
    /// into a policy-relative percentage (positive means "moved toward
    /// regression" for <paramref name="policy"/>'s configured direction).
    /// </summary>
    /// <remarks>
    /// When <see cref="MetricComparison.PercentDelta"/> is null, the engine
    /// has already established the reference value is zero (see
    /// <c>PercentDeltaCalculator</c>'s <c>ReferenceZero</c> status) — a
    /// non-zero reference with a null PercentDelta would be an engine
    /// invariant violation, so that case throws rather than silently
    /// treating it as zero-reference. This reproduces the legacy
    /// evaluator's zero-baseline handling: a move away from zero is treated
    /// as an infinite regression/improvement in the policy's configured
    /// direction; a stay-at-zero is zero change.
    /// </remarks>
    private static double ResolvePolicyRelativePercent(MetricComparison metric, MetricPolicy policy)
    {
        if (metric.PercentDelta is { } rawPercent)
        {
            return policy.Direction == MetricDirection.LowerIsBetter ? rawPercent : -rawPercent;
        }

        var reference = metric.Reference?.Value
            ?? throw InvalidComparisonState(metric, nameof(metric.Reference));
        var candidate = metric.Candidate?.Value
            ?? throw InvalidComparisonState(metric, nameof(metric.Candidate));

        if (reference != 0d)
        {
            throw new InvalidOperationException(
                $"Comparable metric '{metric.MetricName}' has a null PercentDelta despite a " +
                $"non-zero reference value ({reference}). This indicates a BenchmarkComparisonEngine " +
                "invariant violation, not a case this evaluator should interpret.");
        }

        var movedWorse = policy.Direction == MetricDirection.LowerIsBetter ? candidate > 0d : candidate < 0d;
        return movedWorse ? double.PositiveInfinity : 0d;
    }

    private static InvalidOperationException InvalidComparisonState(MetricComparison metric, string missingField) =>
        new($"Comparable metric '{metric.MetricName}' unexpectedly has a null {missingField}. " +
            "This indicates a BenchmarkComparisonEngine invariant violation: a Comparable status " +
            "must always carry Reference, Candidate, and AbsoluteDelta.");

    /// <summary>
    /// Worst-wins across evaluated metrics: Regressed > Warning > Improved >
    /// Passed. Unstable is handled before metric evaluation and never
    /// reaches this function; Missing and New are benchmark-level
    /// structural outcomes, never a metric-level status here.
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
    /// Formats timing values used by stability explanations. Both the
    /// candidate mean and standard deviation are expressed in nanoseconds
    /// by the comparison model — this is not used for arbitrary policy
    /// metrics (those go through <c>MetricFormatters.For(metricName)</c> in
    /// <see cref="EvaluateMetric"/> instead), so there's no allocation-bytes
    /// ambiguity here to worry about.
    /// </summary>
    private static string FormatNanoseconds(double nanoseconds) =>
        nanoseconds >= 1_000_000
            ? string.Create(CultureInfo.InvariantCulture, $"{nanoseconds / 1_000_000:F3} ms")
            : nanoseconds >= 1_000
                ? string.Create(CultureInfo.InvariantCulture, $"{nanoseconds / 1_000:F3} \u00b5s")
                : string.Create(CultureInfo.InvariantCulture, $"{nanoseconds:F3} ns");
}
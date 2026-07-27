using System.Globalization;
using Bijecta.BenchmarkGate.Core.Baseline;
using Bijecta.BenchmarkGate.Core.Model;

namespace Bijecta.BenchmarkGate.Core.Evaluation;

/// <summary>
/// Compares a set of current observations against an approved baseline
/// under a regression policy. Pure: no I/O, no console output, no process
/// termination — see ADR-0001.
/// </summary>
public static class RegressionEvaluator
{
    public static SuiteDecision Evaluate(
        IReadOnlyList<BenchmarkObservation> observations,
        BenchmarkBaseline baseline,
        RegressionPolicy policy)
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
                    BaselineMeanNanoseconds: null,
                    CurrentMeanNanoseconds: observation.MeanNanoseconds,
                    AbsoluteDeltaNanoseconds: null,
                    RelativeDeltaPercent: null,
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
                BaselineMeanNanoseconds: baselineEntry.MeanNanoseconds,
                CurrentMeanNanoseconds: null,
                AbsoluteDeltaNanoseconds: null,
                RelativeDeltaPercent: null,
                Explanation: "This benchmark exists in the baseline but was not present in the current results."));
        }

        return new SuiteDecision(decisions);
    }

    private static BenchmarkDecision EvaluateAgainstBaseline(
        BenchmarkObservation observation,
        BaselineEntry baselineEntry,
        RegressionPolicy policy)
    {
        var baselineMean = baselineEntry.MeanNanoseconds;
        var currentMean = observation.MeanNanoseconds;
        var absoluteDelta = currentMean - baselineMean;

        // Mean time is lower-is-better: a positive delta (slower) is a
        // regression direction; a negative delta (faster) is an improvement
        // direction. See master spec section 10 for the general formula.
        double relativeDeltaPercent;
        if (baselineMean == 0)
        {
            // Zero-baseline: any positive absolute change is treated as an
            // infinite relative regression. v0.1 always fails on increase
            // from a zero baseline; the configurable zero-baseline policy
            // from the master spec (section 8) is deferred to v0.2.
            relativeDeltaPercent = currentMean > 0 ? double.PositiveInfinity : 0;
        }
        else
        {
            relativeDeltaPercent = absoluteDelta / baselineMean * 100.0;
        }

        var isRegression =
            relativeDeltaPercent >= policy.FailurePercent &&
            Math.Abs(absoluteDelta) >= policy.MinimumAbsoluteChangeNanoseconds;

        BenchmarkGateStatus status;
        string explanation;

        if (isRegression)
        {
            status = BenchmarkGateStatus.Regressed;
            explanation = string.Create(CultureInfo.InvariantCulture,
                $"Mean time regressed by {relativeDeltaPercent:F2}% " +
                $"({FormatNanoseconds(baselineMean)} -> {FormatNanoseconds(currentMean)}), " +
                $"which is >= the configured failure threshold of {policy.FailurePercent:F2}%.");
        }
        else if (absoluteDelta < 0 &&
                 Math.Abs(relativeDeltaPercent) >= policy.FailurePercent &&
                 Math.Abs(absoluteDelta) >= policy.MinimumAbsoluteChangeNanoseconds)
        {
            status = BenchmarkGateStatus.Improved;
            explanation = string.Create(CultureInfo.InvariantCulture,
                $"Mean time improved by {Math.Abs(relativeDeltaPercent):F2}% " +
                $"({FormatNanoseconds(baselineMean)} -> {FormatNanoseconds(currentMean)}).");
        }
        else
        {
            status = BenchmarkGateStatus.Passed;
            explanation = string.Create(CultureInfo.InvariantCulture,
                $"Mean time changed by {relativeDeltaPercent:F2}% " +
                $"({FormatNanoseconds(baselineMean)} -> {FormatNanoseconds(currentMean)}), " +
                $"within the configured threshold of {policy.FailurePercent:F2}%.");
        }

        return new BenchmarkDecision(
            observation.Identity,
            status,
            baselineMean,
            currentMean,
            absoluteDelta,
            relativeDeltaPercent,
            explanation);
    }

    /// <summary>
    /// Formats nanoseconds for embedding in <see cref="BenchmarkDecision.Explanation"/>.
    /// This intentionally duplicates similar formatting in
    /// <c>Bijecta.BenchmarkGate.Tool.Reporting.MarkdownBuilder</c> — Core must
    /// never depend on Tool (see ADR-0001's dependency direction), so a
    /// small amount of formatting duplication here is the correct tradeoff,
    /// not an oversight.
    /// </summary>
    private static string FormatNanoseconds(double nanoseconds) =>
        nanoseconds >= 1_000_000
            ? string.Create(CultureInfo.InvariantCulture, $"{nanoseconds / 1_000_000:F3} ms")
            : nanoseconds >= 1_000
                ? string.Create(CultureInfo.InvariantCulture, $"{nanoseconds / 1_000:F3} \u00b5s")
                : string.Create(CultureInfo.InvariantCulture, $"{nanoseconds:F3} ns");
}

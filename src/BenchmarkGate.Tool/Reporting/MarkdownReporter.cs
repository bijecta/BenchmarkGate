using Bijecta.BenchmarkGate.Core.Evaluation;

namespace Bijecta.BenchmarkGate.Tool.Reporting;

/// <summary>
/// Writes a GitHub-friendly Markdown summary using <see cref="MarkdownBuilder"/>.
/// One table row per (benchmark, metric) pair — a benchmark with mean-time
/// and allocation metrics produces two rows, sharing the identity/status
/// columns, since each metric can have its own baseline/current/delta.
/// </summary>
public static class MarkdownReporter
{
    public static void Write(string path, SuiteDecision decision, string suite)
    {
        var overall = decision.RegressedCount > 0 ? "\u274c Regressed"
            : decision.MissingCount > 0 ? "\u26a0\ufe0f Incomplete"
            : decision.UnstableCount > 0 ? "\u2753 Unstable"
            : decision.WarningCount > 0 ? "\u26a0\ufe0f Warning"
            : "\u2705 Passed";

        var md = new MarkdownBuilder()
            .Heading(1, $"Benchmark Gate — {suite}")
            .Bold("Overall", overall)
            .Table(
                ["Total", "Improved", "Passed", "Warning", "Regressed", "Missing", "New", "Unstable"],
                [[
                    decision.Benchmarks.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    decision.ImprovedCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    decision.PassedCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    decision.WarningCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    decision.RegressedCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    decision.MissingCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    decision.NewCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    decision.UnstableCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ]])
            .Table(
                ["Benchmark", "Metric", "Baseline", "Current", "Delta", "Status"],
                decision.Benchmarks
                    .OrderBy(b => b.Identity.CanonicalString, StringComparer.Ordinal)
                    .SelectMany(BuildRows));

        var failures = decision.Benchmarks.Where(b =>
            b.Status is BenchmarkGateStatus.Regressed or BenchmarkGateStatus.Missing
                or BenchmarkGateStatus.Unstable or BenchmarkGateStatus.Warning).ToList();

        if (failures.Count > 0)
        {
            md.Heading(2, "Failures");
            foreach (var row in failures)
            {
                md.Bullet($"**[{row.Status}]** `{row.Identity.CanonicalString}`: {row.Explanation}");
            }
        }

        AtomicFileWriter.Write(path, md.ToString());
    }

    private static IEnumerable<IReadOnlyList<string>> BuildRows(BenchmarkDecision benchmark)
    {
        // A benchmark with no metric decisions (New/Missing/Unstable) still
        // gets one row, so it isn't silently dropped from the table.
        if (benchmark.Metrics.Count == 0)
        {
            yield return
            [
                benchmark.Identity.CanonicalString,
                "-",
                "-",
                "-",
                "-",
                benchmark.Status.ToString(),
            ];
            yield break;
        }

        foreach (var metric in benchmark.Metrics)
        {
            var formatter = MetricFormatters.For(metric.MetricName);
            yield return
            [
                benchmark.Identity.CanonicalString,
                metric.MetricName,
                formatter.Format(metric.BaselineValue),
                formatter.Format(metric.CurrentValue),
                MarkdownBuilder.FormatDeltaPercent(metric.RelativeDeltaPercent),
                metric.Status.ToString(),
            ];
        }
    }
}
using System.Globalization;
using Bijecta.BenchmarkGate.Core.Evaluation;

namespace Bijecta.BenchmarkGate.Tool.Reporting;

/// <summary>
/// Renders a <see cref="SuiteDecision"/> as a compact console table. One
/// printed row per (benchmark, metric) pair. Reporting only consumes the
/// evaluation result — it never re-evaluates anything.
/// </summary>
public static class ConsoleReporter
{
    public static void Write(TextWriter output, SuiteDecision decision)
    {
        var rows = decision.Benchmarks
            .OrderBy(b => b.Identity.CanonicalString, StringComparer.Ordinal)
            .ToList();

        if (rows.Count == 0)
        {
            output.WriteLine("No benchmarks evaluated.");
            return;
        }

        const int nameWidth = 46;
        const int metricWidth = 24;
        const int valueWidth = 12;

        output.WriteLine(
            $"{"Benchmark".PadRight(nameWidth)} {"Metric".PadRight(metricWidth)} {"Baseline".PadRight(valueWidth)} {"Current".PadRight(valueWidth)} {"Delta".PadRight(10)} Status");

        foreach (var benchmark in rows)
        {
            var name = Truncate(benchmark.Identity.CanonicalString, nameWidth);

            if (benchmark.Metrics.Count == 0)
            {
                output.WriteLine(
                    $"{name.PadRight(nameWidth)} {"-".PadRight(metricWidth)} {"-".PadRight(valueWidth)} {"-".PadRight(valueWidth)} {"-".PadRight(10)} {benchmark.Status.ToString().ToUpperInvariant()}");
                continue;
            }

            foreach (var metric in benchmark.Metrics)
            {
                var formatter = MetricFormatters.For(metric.MetricName);
                var baselineText = formatter.Format(metric.BaselineValue);
                var currentText = formatter.Format(metric.CurrentValue);
                var deltaText = MarkdownBuilder.FormatDeltaPercent(metric.RelativeDeltaPercent);

                output.WriteLine(
                    $"{name.PadRight(nameWidth)} {metric.MetricName.PadRight(metricWidth)} {baselineText.PadRight(valueWidth)} {currentText.PadRight(valueWidth)} {deltaText.PadRight(10)} {metric.Status.ToString().ToUpperInvariant()}");

                // Only print the benchmark name on the first metric row to
                // avoid visual repetition on multi-metric benchmarks.
                name = "";
            }
        }

        output.WriteLine();
        output.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"Total: {decision.Benchmarks.Count}  " +
            $"Improved: {decision.ImprovedCount}  " +
            $"Passed: {decision.PassedCount}  " +
            $"Warning: {decision.WarningCount}  " +
            $"Regressed: {decision.RegressedCount}  " +
            $"Missing: {decision.MissingCount}  " +
            $"New: {decision.NewCount}  " +
            $"Unstable: {decision.UnstableCount}"));

        var failures = rows.Where(r =>
            r.Status is BenchmarkGateStatus.Regressed or BenchmarkGateStatus.Missing
                or BenchmarkGateStatus.Unstable or BenchmarkGateStatus.Warning).ToList();

        if (failures.Count > 0)
        {
            output.WriteLine();
            output.WriteLine("Details:");
            foreach (var row in failures)
            {
                output.WriteLine($"  [{row.Status}] {row.Identity.CanonicalString}: {row.Explanation}");
            }
        }
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..(maxLength - 1)] + "\u2026";
}
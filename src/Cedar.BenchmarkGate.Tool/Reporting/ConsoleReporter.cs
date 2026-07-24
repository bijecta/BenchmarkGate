using System.Globalization;
using Cedar.BenchmarkGate.Core.Evaluation;

namespace Cedar.BenchmarkGate.Tool.Reporting;

/// <summary>
/// Renders a <see cref="SuiteDecision"/> as a compact console table, in the
/// style of master spec section 13. Reporting only consumes the evaluation
/// result — it never re-evaluates anything. Shares number formatting with
/// <see cref="MarkdownReporter"/> via <see cref="MarkdownBuilder"/>'s static
/// helpers so the two reports never drift apart on unit thresholds.
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
        const int valueWidth = 12;

        output.WriteLine(
            $"{"Benchmark".PadRight(nameWidth)} {"Baseline".PadRight(valueWidth)} {"Current".PadRight(valueWidth)} {"Delta".PadRight(10)} Status");

        foreach (var row in rows)
        {
            var name = Truncate(row.Identity.CanonicalString, nameWidth);
            var baselineText = MarkdownBuilder.FormatNanoseconds(row.BaselineMeanNanoseconds);
            var currentText = MarkdownBuilder.FormatNanoseconds(row.CurrentMeanNanoseconds);
            var deltaText = MarkdownBuilder.FormatDeltaPercent(row.RelativeDeltaPercent);

            output.WriteLine(
                $"{name.PadRight(nameWidth)} {baselineText.PadRight(valueWidth)} {currentText.PadRight(valueWidth)} {deltaText.PadRight(10)} {row.Status.ToString().ToUpperInvariant()}");
        }

        output.WriteLine();
        output.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"Total: {decision.Benchmarks.Count}  " +
            $"Improved: {decision.ImprovedCount}  " +
            $"Passed: {decision.PassedCount}  " +
            $"Regressed: {decision.RegressedCount}  " +
            $"Missing: {decision.MissingCount}  " +
            $"New: {decision.NewCount}"));

        if (decision.RegressedCount > 0 || decision.MissingCount > 0)
        {
            output.WriteLine();
            output.WriteLine("Details:");
            foreach (var row in rows.Where(r =>
                         r.Status is BenchmarkGateStatus.Regressed or BenchmarkGateStatus.Missing))
            {
                output.WriteLine($"  [{row.Status}] {row.Identity.CanonicalString}: {row.Explanation}");
            }
        }
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..(maxLength - 1)] + "\u2026";
}

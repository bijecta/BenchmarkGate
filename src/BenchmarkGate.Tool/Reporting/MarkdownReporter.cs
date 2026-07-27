using Bijecta.BenchmarkGate.Core.Evaluation;

namespace Bijecta.BenchmarkGate.Tool.Reporting;

/// <summary>
/// Writes a GitHub-friendly Markdown summary using <see cref="MarkdownBuilder"/>.
/// v0.1.0-alpha.1 covers the core fields from master spec section 13
/// (overall result, counts, comparison table); provenance/environment
/// sections are deferred to v0.2.
/// </summary>
public static class MarkdownReporter
{
    public static void Write(string path, SuiteDecision decision, string suite)
    {
        var overall = decision.RegressedCount > 0 ? "\u274c Regressed"
            : decision.MissingCount > 0 ? "\u26a0\ufe0f Incomplete"
            : "\u2705 Passed";

        var md = new MarkdownBuilder()
            .Heading(1, $"Benchmark Gate — {suite}")
            .Bold("Overall", overall)
            .Table(
                ["Total", "Improved", "Passed", "Regressed", "Missing", "New"],
                [[
                    decision.Benchmarks.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    decision.ImprovedCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    decision.PassedCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    decision.RegressedCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    decision.MissingCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    decision.NewCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ]])
            .Table(
                ["Benchmark", "Baseline", "Current", "Delta", "Status"],
                decision.Benchmarks
                    .OrderBy(b => b.Identity.CanonicalString, StringComparer.Ordinal)
                    .Select(row => (IReadOnlyList<string>)
                    [
                        row.Identity.CanonicalString,
                        MarkdownBuilder.FormatNanoseconds(row.BaselineMeanNanoseconds),
                        MarkdownBuilder.FormatNanoseconds(row.CurrentMeanNanoseconds),
                        MarkdownBuilder.FormatDeltaPercent(row.RelativeDeltaPercent),
                        row.Status.ToString(),
                    ]));

        if (decision.RegressedCount > 0 || decision.MissingCount > 0)
        {
            md.Heading(2, "Failures");
            foreach (var row in decision.Benchmarks.Where(b =>
                         b.Status is BenchmarkGateStatus.Regressed or BenchmarkGateStatus.Missing))
            {
                md.Bullet($"**[{row.Status}]** `{row.Identity.CanonicalString}`: {row.Explanation}");
            }
        }

        AtomicFileWriter.Write(path, md.ToString());
    }
}

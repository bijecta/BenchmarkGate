using Bijecta.BenchmarkGate.Core.Comparison;
using Bijecta.BenchmarkGate.Storage.FileSystem;
using System.Globalization;

namespace Bijecta.BenchmarkGate.Reporting;

/// <summary>
/// Writes a GitHub-friendly Markdown summary of a <see cref="ComparisonResult"/>
/// using <see cref="MarkdownBuilder"/>. Describes change — useful for PR
/// comments and build summaries — and deliberately does not imitate a
/// <c>check</c> report with the verdict column removed: there is no
/// verdict here to remove a column for.
/// </summary>
/// <remarks>
/// Preserves <see cref="ComparisonResult.Benchmarks"/>' given order rather
/// than re-sorting — see <see cref="ConsoleComparisonReporter"/>'s remarks
/// for why (ADR-0004: canonical ordering is the engine's responsibility
/// alone).
/// </remarks>
public static class MarkdownComparisonReporter
{
    public static void Write(string path, ComparisonResult comparison)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(comparison);

        var md = new MarkdownBuilder()
            .Heading(1, $"Benchmark Compare — {comparison.Suite}")
            .Table(
                ["Comparable", "Added", "Removed"],
                [[
                    comparison.ComparableCount.ToString(CultureInfo.InvariantCulture),
                    comparison.AddedCount.ToString(CultureInfo.InvariantCulture),
                    comparison.RemovedCount.ToString(CultureInfo.InvariantCulture),
                ]]);

        var comparableBenchmarks = comparison.Benchmarks
            .Where(b => b.Status == BenchmarkComparisonStatus.Comparable)
            .ToList();

        if (comparableBenchmarks.Count > 0)
        {
            md.Table(
                ["Benchmark", "Metric", "Reference", "Candidate", "Absolute delta", "Percent delta", "Direction", "Status"],
                comparableBenchmarks.SelectMany(BuildRows));
        }

        WriteNameSection(md, "Added benchmarks", comparison.Benchmarks, BenchmarkComparisonStatus.Added);
        WriteNameSection(md, "Removed benchmarks", comparison.Benchmarks, BenchmarkComparisonStatus.Removed);

        try
        {
            AtomicFileWriter.Write(path, md.ToString());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ReportWriteException(path, "Failed to write Markdown comparison report.", ex);
        }
    }

    private static void WriteNameSection(
        MarkdownBuilder md, string heading, IReadOnlyList<BenchmarkComparison> benchmarks, BenchmarkComparisonStatus status)
    {
        var matching = benchmarks.Where(b => b.Status == status).ToList();
        if (matching.Count == 0)
        {
            return;
        }

        md.Heading(2, heading);
        foreach (var benchmark in matching)
        {
            // CodeSpan chooses a safe delimiter even if the identity
            // contains a backtick (real case: .NET names generic types
            // like List`1, and BenchmarkIdentity.TypeName can be one).
            md.Bullet(MarkdownBuilder.CodeSpan(benchmark.Identity.CanonicalString));
        }
    }

    private static IEnumerable<IReadOnlyList<string>> BuildRows(BenchmarkComparison benchmark)
    {
        // A benchmark with no metric comparisons still gets one row, so it
        // isn't silently dropped from the table. The benchmark itself IS
        // Comparable here — "No metrics" says why the row is empty, rather
        // than "-" which could read as an unknown status.
        if (benchmark.Metrics.Count == 0)
        {
            yield return
            [
                benchmark.Identity.CanonicalString,
                "-", "-", "-", "-", "-",
                "No metrics",
            ];
            yield break;
        }

        foreach (var metric in benchmark.Metrics)
        {
            var unit = metric.Reference?.Unit ?? metric.Candidate?.Unit;
            yield return
            [
                benchmark.Identity.CanonicalString,
                metric.MetricName,
                ComparisonValueFormatter.FormatMetricValue(metric.MetricName, metric.Reference),
                ComparisonValueFormatter.FormatMetricValue(metric.MetricName, metric.Candidate),
                ComparisonValueFormatter.FormatAbsoluteDelta(metric.MetricName, unit, metric.AbsoluteDelta),
                ComparisonValueFormatter.FormatPercentDelta(metric.PercentDelta),
                metric.Direction?.ToString() ?? "-",
                metric.Status.ToString(),
            ];
        }
    }
}
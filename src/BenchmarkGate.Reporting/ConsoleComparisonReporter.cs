using System.Globalization;
using Bijecta.BenchmarkGate.Core.Comparison;

namespace Bijecta.BenchmarkGate.Reporting;

/// <summary>
/// Renders a <see cref="ComparisonResult"/> as a compact console table. One
/// printed row per (benchmark, metric) pair for Comparable benchmarks;
/// Added/Removed benchmarks are listed separately by name, not folded into
/// the metric table, since they have no meaningful reference-vs-candidate
/// row. Reporting only consumes the comparison result — it never
/// re-compares anything, and never prints SuiteDecision's pass/fail
/// vocabulary (Passed/Warning/Regressed/Unstable).
/// </summary>
/// <remarks>
/// Preserves <see cref="ComparisonResult.Benchmarks"/>' given order rather
/// than re-sorting — per ADR-0004, canonical ordering is
/// <c>BenchmarkComparisonEngine</c>'s responsibility alone; a reporter
/// re-sorting by <c>CanonicalString</c> would duplicate that responsibility
/// with a different (string, not structured) comparator.
/// </remarks>
public static class ConsoleComparisonReporter
{
    public static void Write(TextWriter output, ComparisonResult comparison)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(comparison);

        output.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"Suite: {comparison.Suite}  " +
            $"Comparable: {comparison.ComparableCount}  " +
            $"Added: {comparison.AddedCount}  " +
            $"Removed: {comparison.RemovedCount}"));
        output.WriteLine();

        var comparableBenchmarks = comparison.Benchmarks
            .Where(b => b.Status == BenchmarkComparisonStatus.Comparable)
            .ToList();

        if (comparableBenchmarks.Count == 0 && comparison.AddedCount == 0 && comparison.RemovedCount == 0)
        {
            output.WriteLine("No benchmarks compared.");
            return;
        }

        if (comparableBenchmarks.Count > 0)
        {
            WriteMetricTable(output, comparableBenchmarks);
        }

        WriteNameList(output, "Added benchmarks:", comparison.Benchmarks, BenchmarkComparisonStatus.Added);
        WriteNameList(output, "Removed benchmarks:", comparison.Benchmarks, BenchmarkComparisonStatus.Removed);
    }

    private static void WriteMetricTable(TextWriter output, IReadOnlyList<BenchmarkComparison> benchmarks)
    {
        const int nameWidth = 40;
        const int metricWidth = 20;
        const int valueWidth = 12;
        const int absoluteWidth = 14;
        const int percentWidth = 10;
        const int directionWidth = 14;

        output.WriteLine(
            $"{"Benchmark".PadRight(nameWidth)} {"Metric".PadRight(metricWidth)} {"Reference".PadRight(valueWidth)} " +
            $"{"Candidate".PadRight(valueWidth)} {"Abs Delta".PadRight(absoluteWidth)} {"% Delta".PadRight(percentWidth)} " +
            $"{"Direction".PadRight(directionWidth)} Status");

        foreach (var benchmark in benchmarks)
        {
            var name = Truncate(benchmark.Identity.CanonicalString, nameWidth);

            if (benchmark.Metrics.Count == 0)
            {
                // The benchmark itself IS Comparable — "No metrics" says
                // why the row has no values, rather than "-" which could
                // read as an unknown/missing status.
                output.WriteLine(
                    $"{name.PadRight(nameWidth)} {"-".PadRight(metricWidth)} {"-".PadRight(valueWidth)} " +
                    $"{"-".PadRight(valueWidth)} {"-".PadRight(absoluteWidth)} {"-".PadRight(percentWidth)} " +
                    $"{"-".PadRight(directionWidth)} No metrics");
                continue;
            }

            foreach (var metric in benchmark.Metrics)
            {
                var metricName = Truncate(metric.MetricName, metricWidth);
                var unit = metric.Reference?.Unit ?? metric.Candidate?.Unit;
                var referenceText = Truncate(ComparisonValueFormatter.FormatMetricValue(metric.MetricName, metric.Reference), valueWidth);
                var candidateText = Truncate(ComparisonValueFormatter.FormatMetricValue(metric.MetricName, metric.Candidate), valueWidth);
                var absoluteText = Truncate(ComparisonValueFormatter.FormatAbsoluteDelta(metric.MetricName, unit, metric.AbsoluteDelta), absoluteWidth);
                var percentText = Truncate(ComparisonValueFormatter.FormatPercentDelta(metric.PercentDelta), percentWidth);
                var directionText = Truncate(metric.Direction?.ToString() ?? "-", directionWidth);

                output.WriteLine(
                    $"{name.PadRight(nameWidth)} {metricName.PadRight(metricWidth)} {referenceText.PadRight(valueWidth)} " +
                    $"{candidateText.PadRight(valueWidth)} {absoluteText.PadRight(absoluteWidth)} {percentText.PadRight(percentWidth)} " +
                    $"{directionText.PadRight(directionWidth)} {metric.Status}");

                // Only print the benchmark name on the first metric row to
                // avoid visual repetition on multi-metric benchmarks.
                name = "";
            }
        }

        output.WriteLine();
    }

    private static void WriteNameList(
        TextWriter output, string heading, IReadOnlyList<BenchmarkComparison> benchmarks, BenchmarkComparisonStatus status)
    {
        var matching = benchmarks.Where(b => b.Status == status).ToList();
        if (matching.Count == 0)
        {
            return;
        }

        output.WriteLine(heading);
        foreach (var benchmark in matching)
        {
            output.WriteLine($"  {benchmark.Identity.CanonicalString}");
        }

        output.WriteLine();
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..(maxLength - 1)] + "\u2026";
}
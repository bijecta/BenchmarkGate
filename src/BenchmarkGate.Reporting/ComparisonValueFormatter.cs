using Bijecta.BenchmarkGate.Core.Comparison;
using Bijecta.BenchmarkGate.Core.Evaluation;
using System.Globalization;

namespace Bijecta.BenchmarkGate.Reporting;

/// <summary>
/// Shared formatting for human-readable comparison reporters (Console,
/// Markdown). Policy-free — this is reporting-layer sharing, not a Core
/// dependency.
/// </summary>
internal static class ComparisonValueFormatter
{
    /// <summary>
    /// Formats a raw metric value. Uses <see cref="MetricValue.Unit"/> as
    /// the source of truth for whether a known-unit formatter applies —
    /// when unit is null (unknown metric), falls back to a raw
    /// round-trippable numeric representation rather than reconstructing
    /// unit semantics from the metric name alone.
    /// </summary>
    public static string FormatMetricValue(string metricName, MetricValue? value)
    {
        if (value is not { } metricValue)
        {
            return "-";
        }

        return metricValue.Unit is null
            ? metricValue.Value.ToString("G17", CultureInfo.InvariantCulture)
            : MetricFormatters.For(metricName).Format(metricValue.Value);
    }

    /// <summary>
    /// Formats an absolute delta with an explicit "+" for positive values
    /// (formatter output already carries "-" naturally for negative ones).
    /// Meaningful even when <see cref="MetricComparison.PercentDelta"/> is
    /// null (a zero-reference change) — callers must not skip this column
    /// just because the percent column shows "n/a".
    /// </summary>
    /// <param name="metricName">
    /// The metric's name, used to select a display formatter when
    /// <paramref name="unit"/> is known.
    /// </param>
    /// <param name="unit">
    /// The comparison's known unit for this metric (from either side's
    /// <see cref="MetricValue.Unit"/> — safe to use either today, since
    /// genuine unit-mismatch detection isn't producible yet and a
    /// Comparable metric receives the same catalog-derived unit on both
    /// sides). Null means unknown, same as <see cref="FormatMetricValue"/>'s
    /// contract — the delta then gets a raw round-trippable representation
    /// rather than assuming a formatter derived from the metric name alone
    /// knows the right unit.
    /// </param>
    /// <param name="absoluteDelta">
    /// The raw delta from <see cref="MetricComparison.AbsoluteDelta"/>, or
    /// null when no delta was computed (any non-Comparable metric status).
    /// </param>
    public static string FormatAbsoluteDelta(string metricName, string? unit, double? absoluteDelta)
    {
        if (absoluteDelta is not { } delta)
        {
            return "-";
        }

        var formatted = unit is null
            ? delta.ToString("G17", CultureInfo.InvariantCulture)
            : MetricFormatters.For(metricName).Format(delta);

        return delta > 0d ? $"+{formatted}" : formatted;
    }

    public static string FormatPercentDelta(double? percentDelta) =>
        percentDelta is { } percent ? MarkdownBuilder.FormatDeltaPercent(percent) : "n/a";
}
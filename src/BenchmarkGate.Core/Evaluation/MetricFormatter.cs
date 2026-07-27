using Bijecta.BenchmarkGate.Core.Model;
using System.Globalization;

namespace Bijecta.BenchmarkGate.Core.Evaluation;

/// <summary>
/// Formats a metric's raw numeric value into a human-readable string for
/// report/explanation output, in whatever unit is natural for that metric.
/// </summary>
public interface IMetricFormatter
{
    string Format(double value);
}

/// <summary>Nanosecond-scale durations: ns / µs / ms.</summary>
public sealed class NanosecondsFormatter : IMetricFormatter
{
    public string Format(double value) =>
        value >= 1_000_000
            ? string.Create(CultureInfo.InvariantCulture, $"{value / 1_000_000:F3} ms")
            : value >= 1_000
                ? string.Create(CultureInfo.InvariantCulture, $"{value / 1_000:F3} \u00b5s")
                : string.Create(CultureInfo.InvariantCulture, $"{value:F3} ns");
}

/// <summary>Byte-scale sizes: B / KB / MB. Binary (1024) scaling, not decimal.</summary>
public sealed class BytesFormatter : IMetricFormatter
{
    public string Format(double value) =>
        value >= 1_048_576
            ? string.Create(CultureInfo.InvariantCulture, $"{value / 1_048_576:F3} MB")
            : value >= 1_024
                ? string.Create(CultureInfo.InvariantCulture, $"{value / 1_024:F3} KB")
                : string.Create(CultureInfo.InvariantCulture, $"{value:F0} B");
}

/// <summary>
/// Fallback for metrics with no dedicated formatter: a bare, unitless number.
/// Used for future count-style metrics (e.g. branch mispredictions, GC
/// collections) until they get a real formatter of their own.
/// </summary>
public sealed class CountFormatter : IMetricFormatter
{
    public string Format(double value) => string.Create(CultureInfo.InvariantCulture, $"{value:F0}");
}

/// <summary>
/// Resolves the right <see cref="IMetricFormatter"/> for a given metric name.
/// New metrics need an entry here to get correctly-unit-labeled output;
/// until then they fall back to <see cref="CountFormatter"/> (unitless).
/// </summary>
public static class MetricFormatters
{
    private static readonly IReadOnlyDictionary<string, IMetricFormatter> ByMetricName =
        new Dictionary<string, IMetricFormatter>(StringComparer.Ordinal)
        {
            [BenchmarkObservation.MeanNanosecondsMetric] = new NanosecondsFormatter(),
            [BenchmarkObservation.AllocatedBytesMetric] = new BytesFormatter(),
        };

    private static readonly IMetricFormatter Fallback = new CountFormatter();

    public static IMetricFormatter For(string metricName) =>
        ByMetricName.GetValueOrDefault(metricName, Fallback);
}
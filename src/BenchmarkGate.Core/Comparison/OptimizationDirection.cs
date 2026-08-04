namespace Bijecta.BenchmarkGate.Core.Comparison;

/// <summary>
/// Describes which direction of change is desirable for a metric.
/// </summary>
public enum OptimizationDirection
{
    /// <summary>
    /// A smaller value is better (e.g. mean execution time, allocated bytes).
    /// </summary>
    LowerIsBetter,

    /// <summary>
    /// A larger value is better (e.g. throughput).
    /// </summary>
    HigherIsBetter,

    /// <summary>
    /// Changes are recorded but are not classified as improvements or
    /// degradations. An unchanged value maps to <see cref="ChangeDirection.Unchanged"/>;
    /// any actual change — positive or negative — maps to
    /// <see cref="ChangeDirection.Indeterminate"/>, since neither direction
    /// of movement is inherently better or worse for this metric.
    /// </summary>
    Neutral
}
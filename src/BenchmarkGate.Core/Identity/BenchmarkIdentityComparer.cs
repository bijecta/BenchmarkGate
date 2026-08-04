namespace Bijecta.BenchmarkGate.Core.Identity;

/// <summary>
/// Orders <see cref="BenchmarkIdentity"/> values deterministically, by
/// comparing structured identity components rather than a concatenated
/// string — avoiding any delimiter/escaping ambiguity.
/// </summary>
/// <remarks>
/// Order: <see cref="BenchmarkIdentity.TypeName"/> (ordinal), then
/// <see cref="BenchmarkIdentity.MethodName"/> (ordinal), then
/// <see cref="BenchmarkIdentity.Job"/> (ordinal), then
/// <see cref="BenchmarkIdentity.Parameters"/> compared entry-by-entry
/// (ordinal key, then ordinal value; a shorter parameter set sorts before
/// a longer one that agrees on their shared prefix). Consumers needing a
/// stable, reproducible ordering — independent of file ordering, parser
/// ordering, or dictionary enumeration — should sort by this comparer
/// rather than relying on any incidental input order.
/// </remarks>
public sealed class BenchmarkIdentityComparer : IComparer<BenchmarkIdentity>
{
    public static BenchmarkIdentityComparer Instance { get; } = new();

    private BenchmarkIdentityComparer()
    {
    }

    public int Compare(BenchmarkIdentity? x, BenchmarkIdentity? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;

        var typeNameComparison = string.CompareOrdinal(x.TypeName, y.TypeName);
        if (typeNameComparison != 0) return typeNameComparison;

        var methodNameComparison = string.CompareOrdinal(x.MethodName, y.MethodName);
        if (methodNameComparison != 0) return methodNameComparison;

        var jobComparison = string.CompareOrdinal(x.Job, y.Job);
        if (jobComparison != 0) return jobComparison;

        return CompareParameters(x.Parameters, y.Parameters);
    }

    private static int CompareParameters(
        IReadOnlyDictionary<string, string> left,
        IReadOnlyDictionary<string, string> right)
    {
        using var leftEnumerator = left.GetEnumerator();
        using var rightEnumerator = right.GetEnumerator();

        while (true)
        {
            var leftHasNext = leftEnumerator.MoveNext();
            var rightHasNext = rightEnumerator.MoveNext();

            if (!leftHasNext && !rightHasNext) return 0;
            if (!leftHasNext) return -1;
            if (!rightHasNext) return 1;

            var keyComparison = string.CompareOrdinal(leftEnumerator.Current.Key, rightEnumerator.Current.Key);
            if (keyComparison != 0) return keyComparison;

            var valueComparison = string.CompareOrdinal(leftEnumerator.Current.Value, rightEnumerator.Current.Value);
            if (valueComparison != 0) return valueComparison;
        }
    }
}
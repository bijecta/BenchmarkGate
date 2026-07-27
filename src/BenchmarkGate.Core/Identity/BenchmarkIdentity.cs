using System.Text;

namespace Bijecta.BenchmarkGate.Core.Identity;

/// <summary>
/// A stable, deterministic identity for a single benchmark. Two observations
/// (baseline vs current run) are considered "the same benchmark" if and only
/// if their <see cref="BenchmarkIdentity"/> values are equal.
/// </summary>
/// <remarks>
/// Environment (OS, runtime, CPU, ...) is deliberately NOT part of the
/// identity. Environment compatibility is a separate concern (see the
/// environment evaluator, deferred past v0.1).
/// </remarks>
public sealed class BenchmarkIdentity : IEquatable<BenchmarkIdentity>
{
    public string TypeName { get; }
    public string MethodName { get; }
    public string Job { get; }
    public IReadOnlyDictionary<string, string> Parameters { get; }

    private readonly string _canonical;

    public BenchmarkIdentity(
        string typeName,
        string methodName,
        string job,
        IReadOnlyDictionary<string, string>? parameters = null)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            throw new ArgumentException("Type name must not be empty.", nameof(typeName));
        if (string.IsNullOrWhiteSpace(methodName))
            throw new ArgumentException("Method name must not be empty.", nameof(methodName));
        if (string.IsNullOrWhiteSpace(job))
            throw new ArgumentException("Job must not be empty.", nameof(job));

        TypeName = typeName;
        MethodName = methodName;
        Job = job;

        // Sort parameter keys ordinally so identity is independent of
        // the order parameters were declared or serialized in.
        Parameters = (parameters ?? new Dictionary<string, string>())
            .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        _canonical = BuildCanonicalString(TypeName, MethodName, Job, Parameters);
    }

    /// <summary>
    /// Canonical textual representation, e.g.
    /// "Namespace.Type.Method|job=Ci|N=1000000|Distribution=Canonical"
    /// Parameter keys are sorted ordinally. Values are rendered using
    /// invariant culture. This string is what identity equality and
    /// dictionary lookups are keyed on.
    /// </summary>
    public string CanonicalString => _canonical;

    private static string BuildCanonicalString(
        string typeName,
        string methodName,
        string job,
        IReadOnlyDictionary<string, string> parameters)
    {
        var sb = new StringBuilder();
        sb.Append(typeName).Append('.').Append(methodName);
        sb.Append("|job=").Append(job);

        foreach (var (key, value) in parameters)
        {
            // Values are already strings by the time they reach Core;
            // callers (the BenchmarkDotNet adapter) are responsible for
            // rendering numeric/other values using invariant culture
            // before constructing a BenchmarkIdentity.
            sb.Append('|').Append(key).Append('=').Append(value);
        }

        return sb.ToString();
    }

    public bool Equals(BenchmarkIdentity? other) =>
        other is not null && string.Equals(_canonical, other._canonical, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as BenchmarkIdentity);

    public override int GetHashCode() => string.GetHashCode(_canonical, StringComparison.Ordinal);

    public override string ToString() => _canonical;
}

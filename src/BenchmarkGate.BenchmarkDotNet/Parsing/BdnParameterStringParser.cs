namespace Bijecta.BenchmarkGate.BenchmarkDotNet.Parsing;

/// <summary>
/// The categories of malformed parameter fragment that
/// <see cref="BdnParameterStringParser"/> can encounter. Each corresponds
/// to a distinct malformed input shape — see <see cref="ParameterParseIssue"/>.
/// No broader taxonomy is introduced until a concrete BenchmarkDotNet
/// output shape requires it (see Issue #16).
/// </summary>
internal enum ParameterParseIssueKind
{
    /// <summary>The fragment contains no '=' separator at all, e.g. "N1000000".</summary>
    MissingSeparator,

    /// <summary>
    /// The fragment contains a '=' separator, but the key portion before it
    /// is empty, e.g. "=1000000".
    /// </summary>
    EmptyKey
}

/// <summary>
/// One malformed fragment encountered while parsing a BenchmarkDotNet
/// parameter display string. Preserved rather than discarded so callers
/// can report a precise diagnostic instead of silently losing the
/// fragment.
/// </summary>
/// <param name="FragmentIndex">
/// The position of this fragment among the non-empty, comma-separated
/// fragments of the display string (empty fragments, e.g. from a trailing
/// comma, are skipped before indexing — matching this parser's prior
/// behavior — so this is not a raw character offset).
/// </param>
/// <param name="Fragment">
/// The raw, untrimmed fragment text exactly as it appeared in the display
/// string.
/// </param>
/// <param name="Kind">The specific way this fragment is malformed.</param>
internal sealed record ParameterParseIssue(
    int FragmentIndex,
    string Fragment,
    ParameterParseIssueKind Kind);

/// <summary>
/// The outcome of parsing a BenchmarkDotNet parameter display string:
/// every successfully parsed key/value pair, plus every malformed
/// fragment encountered along the way. Both are preserved in full so
/// downstream diagnostics never need to reparse or guess — see Issue #16.
/// </summary>
/// <param name="Parameters">Successfully parsed key/value pairs, keyed by parameter name.</param>
/// <param name="Issues">
/// Malformed fragments encountered during parsing, in the order they were
/// found. Empty when every fragment parsed successfully.
/// </param>
internal sealed record ParameterParseResult(
    IReadOnlyDictionary<string, string> Parameters,
    IReadOnlyList<ParameterParseIssue> Issues);

/// <summary>
/// Parses BenchmarkDotNet's single-string parameter display format
/// ("N=1000000,Distribution=Canonical") into a key/value dictionary,
/// preserving any malformed fragments encountered rather than discarding
/// them. Key ordering and invariant-culture value normalization are
/// handled by <see cref="Core.Identity.BenchmarkIdentity"/> itself; this
/// class only splits the display string.
/// </summary>
internal static class BdnParameterStringParser
{
    public static ParameterParseResult Parse(string? parametersDisplayString)
    {
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
        var issues = new List<ParameterParseIssue>();

        if (string.IsNullOrWhiteSpace(parametersDisplayString))
            return new ParameterParseResult(parameters, issues);

        var fragments = parametersDisplayString.Split(',', StringSplitOptions.RemoveEmptyEntries);

        for (var fragmentIndex = 0; fragmentIndex < fragments.Length; fragmentIndex++)
        {
            var fragment = fragments[fragmentIndex];
            var separatorIndex = fragment.IndexOf('=');

            if (separatorIndex < 0)
            {
                issues.Add(new ParameterParseIssue(fragmentIndex, fragment, ParameterParseIssueKind.MissingSeparator));
                continue;
            }

            if (separatorIndex == 0)
            {
                issues.Add(new ParameterParseIssue(fragmentIndex, fragment, ParameterParseIssueKind.EmptyKey));
                continue;
            }

            var key = fragment[..separatorIndex].Trim();
            var value = fragment[(separatorIndex + 1)..].Trim();
            parameters[key] = value;
        }

        return new ParameterParseResult(parameters, issues);
    }
}
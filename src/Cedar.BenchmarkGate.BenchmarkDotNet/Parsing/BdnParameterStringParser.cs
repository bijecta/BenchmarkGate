namespace Cedar.BenchmarkGate.BenchmarkDotNet.Parsing;

/// <summary>
/// Parses BenchmarkDotNet's single-string parameter display format
/// ("N=1000000,Distribution=Canonical") into a key/value dictionary.
/// Key ordering and invariant-culture value normalization are handled by
/// <see cref="Core.Identity.BenchmarkIdentity"/> itself; this class only
/// splits the display string.
/// </summary>
internal static class BdnParameterStringParser
{
    public static Dictionary<string, string> Parse(string? parametersDisplayString)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(parametersDisplayString))
            return result;

        foreach (var pair in parametersDisplayString.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var separatorIndex = pair.IndexOf('=');
            if (separatorIndex <= 0)
            {
                // Malformed pair without '=' — skip rather than throw here;
                // the identity will simply not include this fragment.
                // A future version could surface this as a diagnostic.
                continue;
            }

            var key = pair[..separatorIndex].Trim();
            var value = pair[(separatorIndex + 1)..].Trim();
            result[key] = value;
        }

        return result;
    }
}

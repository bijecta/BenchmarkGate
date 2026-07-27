namespace Bijecta.BenchmarkGate.BenchmarkDotNet.Parsing;

/// <summary>
/// Thrown when a BenchmarkDotNet result file is malformed, missing required
/// fields, or otherwise cannot be parsed into normalized observations.
/// Callers at the CLI boundary map this to
/// <c>ExitCodes.UnsupportedSchema</c> (see master spec section 9 — expected
/// failures use typed results/exceptions, not generic ones).
/// </summary>
public sealed class BenchmarkResultParseException : Exception
{
    public string SourceFile { get; }

    public BenchmarkResultParseException(string sourceFile, string message)
        : base($"{message} (source file: '{sourceFile}')")
    {
        SourceFile = sourceFile;
    }

    public BenchmarkResultParseException(string sourceFile, string message, Exception innerException)
        : base($"{message} (source file: '{sourceFile}')", innerException)
    {
        SourceFile = sourceFile;
    }
}

using Bijecta.BenchmarkGate.Core.Validation;

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

    /// <summary>
    /// Structured semantic-validation diagnostics, present when this
    /// exception represents an ObservationValidator or
    /// ObservationSetValidator failure (a document/entry that failed
    /// semantic checks). Null when the failure is instead file access,
    /// JSON syntax, deserialization shape, or path discovery — those never
    /// reach the validators. Public so callers can render structured
    /// diagnostics (JSON, Markdown, IDE annotations) instead of parsing
    /// the exception message text.
    /// </summary>
    public ValidationResult? ValidationResult { get; }

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

    // Deliberately does not append "(source file: '...')" the way the two
    // constructors above do — BuildMessage's heading already names the
    // source file in its first line ("Result file '...' contains N
    // validation error(s):"), so appending it again would be redundant.
    // Two different message-construction conventions for two different
    // failure categories (throw-directly vs. validation-result), each
    // self-identifying in its own way.
    private BenchmarkResultParseException(string sourceFile, string message, ValidationResult validationResult)
        : base(message)
    {
        SourceFile = sourceFile;
        ValidationResult = validationResult;
    }

    internal static BenchmarkResultParseException FromValidationResult(string sourceFile, ValidationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.IsValid)
        {
            throw new ArgumentException(
                "A valid result cannot be converted into a parse exception.", nameof(result));
        }

        var errors = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        var lines = errors.Select(d => $"  {d.Descriptor.Id} {d.Path}: {d.Message}");
        var message = $"Result file '{sourceFile}' contains {errors.Count} validation error(s):" +
                       Environment.NewLine + string.Join(Environment.NewLine, lines);

        return new BenchmarkResultParseException(sourceFile, message, result);
    }
}
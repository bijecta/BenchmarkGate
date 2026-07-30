namespace Bijecta.BenchmarkGate.Reporting;

/// <summary>
/// Thrown when a report file cannot be written (invalid path, access
/// denied, missing directory, disk full, atomic-write failure, etc).
/// Every reporter (Markdown/Json/Junit) wraps its underlying I/O
/// exceptions in this so CheckCommand can catch one type and produce a
/// stable exit code instead of an unhandled stack trace.
/// </summary>
public sealed class ReportWriteException : Exception
{
    public string OutputPath { get; }

    public ReportWriteException(string outputPath, string message, Exception innerException)
        : base($"{message} (output file: '{outputPath}')", innerException)
    {
        OutputPath = outputPath;
    }
}
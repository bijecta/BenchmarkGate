using Bijecta.BenchmarkGate.BenchmarkDotNet.Parsing;
using Bijecta.BenchmarkGate.Core.Baseline;
using Bijecta.BenchmarkGate.Core.Comparison;
using Bijecta.BenchmarkGate.Core.Evaluation;
using Bijecta.BenchmarkGate.Core.Model;
using Bijecta.BenchmarkGate.Reporting;
using Bijecta.BenchmarkGate.Storage.FileSystem;
using Bijecta.BenchmarkGate.Tool.Baseline;

namespace Bijecta.BenchmarkGate.Tool.Commands;

/// <summary>
/// Implements <c>benchmark-gate compare</c>. Argument acquisition lives in
/// Program.cs (System.CommandLine, per ADR-0002). This type orchestrates:
/// parse -> load baseline -> BenchmarkComparisonEngine.Compare -> report ->
/// exit code.
/// </summary>
/// <remarks>
/// No policy acquisition anywhere in this path — compare is deliberately
/// policy-free. This type must not itself match benchmarks, match metrics,
/// check units, calculate deltas, or derive direction — all of that is
/// <c>BenchmarkComparisonEngine</c>'s job, called once, from one place,
/// same as <c>CheckCommand</c>.
/// </remarks>
internal static class CompareCommand
{
    public static int Run(
        string resultsPath,
        string baselinePath,
        string format,
        string? outputPath,
        bool quiet,
        TextWriter stdout,
        TextWriter stderr)
    {
        if (format is not ("console" or "json" or "markdown"))
        {
            stderr.WriteLine($"Invalid --format value '{format}'. Expected console, json, or markdown.");
            return ExitCodes.InvalidArguments;
        }

        // console can go to stdout with no --output; json/markdown
        // reporters only know how to write to a path (see #28), so
        // --output is required for those two. Validated here, before any
        // I/O — nothing has been attempted yet, so this is InvalidArguments,
        // not a write failure. This is a conditional requirement
        // System.CommandLine's per-option Required flag can't express on
        // its own.
        if (format is "json" or "markdown" && string.IsNullOrWhiteSpace(outputPath))
        {
            stderr.WriteLine($"--output is required when --format is '{format}'.");
            return ExitCodes.InvalidArguments;
        }

        IReadOnlyList<BenchmarkObservation> observations;
        try
        {
            observations = BenchmarkDotNetResultParser.ParsePath(resultsPath);
        }
        catch (BenchmarkResultParseException ex)
        {
            stderr.WriteLine($"Failed to parse results: {ex.Message}");
            return ExitCodes.UnsupportedSchema;
        }

        BenchmarkBaseline baseline;
        try
        {
            baseline = BaselineFile.Load(baselinePath);
        }
        catch (BaselineFileException ex)
        {
            stderr.WriteLine($"Failed to load baseline: {ex.Message}");
            return ExitCodes.InvalidBaselineOrPolicy;
        }

        var comparison = BenchmarkComparisonEngine.Compare(baseline, observations);

        try
        {
            WriteReport(comparison, format, outputPath, quiet, stdout);
        }
        catch (ReportWriteException ex)
        {
            stderr.WriteLine($"Failed to write report: {ex.Message}");
            return ExitCodes.OutputWriteFailure;
        }

        // Comparison completed, regardless of what it found — compare has
        // no severity verdict. A regression, an added/removed benchmark, a
        // missing metric: none of these are process failures, they're
        // comparison results.
        return ExitCodes.Passed;
    }

    /// <summary>
    /// --quiet controls terminal output only — it must never suppress an
    /// explicitly requested output artifact (a file the user asked for via
    /// --output still gets written even under --quiet).
    /// </summary>
    internal static void WriteReport(
        ComparisonResult comparison, string format, string? outputPath, bool quiet, TextWriter stdout)
    {
        switch (format)
        {
            case "console" when outputPath is not null:
                WriteConsoleReportToFile(comparison, outputPath);
                break;

            case "console":
                if (!quiet)
                {
                    ConsoleComparisonReporter.Write(stdout, comparison);
                }
                break;

            case "json":
                JsonComparisonReporter.Write(outputPath!, comparison);
                break;

            case "markdown":
                MarkdownComparisonReporter.Write(outputPath!, comparison);
                break;
        }
    }

    /// <summary>
    /// Renders console-format output to a string first, then writes it via
    /// <see cref="AtomicFileWriter"/> — matching <c>JsonComparisonReporter</c>/
    /// <c>MarkdownComparisonReporter</c>'s atomicity guarantee (and
    /// <c>MarkdownReporter</c>/<c>JunitReporter</c>'s established pattern for
    /// a text-rendered report) rather than a one-off raw <see cref="StreamWriter"/>
    /// that could leave a partially-written file on a mid-write failure.
    /// </summary>
    private static void WriteConsoleReportToFile(ComparisonResult comparison, string outputPath)
    {
        using var stringWriter = new StringWriter();
        ConsoleComparisonReporter.Write(stringWriter, comparison);

        try
        {
            AtomicFileWriter.Write(outputPath, stringWriter.ToString());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ReportWriteException(outputPath, "Failed to write console-format comparison report.", ex);
        }
    }
}
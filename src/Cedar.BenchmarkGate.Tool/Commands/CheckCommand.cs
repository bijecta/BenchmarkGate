using Cedar.BenchmarkGate.BenchmarkDotNet.Parsing;
using Cedar.BenchmarkGate.Core.Evaluation;
using Cedar.BenchmarkGate.Tool.Baseline;
using Cedar.BenchmarkGate.Tool.Reporting;

namespace Cedar.BenchmarkGate.Tool.Commands;

/// <summary>
/// Implements `cedar-benchmark-gate check`. Argument acquisition lives in
/// Program.cs (System.CommandLine, per ADR-0002) — this type only
/// orchestrates parse -> load baseline -> evaluate -> report -> exit code.
/// </summary>
internal static class CheckCommand
{
    public static int Run(
        string resultsPath,
        string baselinePath,
        double thresholdPercent,
        double minimumAbsoluteChangeNs,
        string? markdownPath,
        string? jsonPath,
        bool quiet,
        TextWriter stdout,
        TextWriter stderr)
    {
        IReadOnlyList<Core.Model.BenchmarkObservation> observations;
        try
        {
            observations = BenchmarkDotNetResultParser.ParsePath(resultsPath);
        }
        catch (BenchmarkResultParseException ex)
        {
            stderr.WriteLine($"Failed to parse results: {ex.Message}");
            return ExitCodes.UnsupportedSchema;
        }

        Core.Baseline.BenchmarkBaseline baselineDoc;
        try
        {
            baselineDoc = BaselineFile.Load(baselinePath);
        }
        catch (BaselineFileException ex)
        {
            stderr.WriteLine($"Failed to load baseline: {ex.Message}");
            return ExitCodes.InvalidBaselineOrPolicy;
        }

        var policy = new RegressionPolicy(thresholdPercent, minimumAbsoluteChangeNs);
        var decision = RegressionEvaluator.Evaluate(observations, baselineDoc, policy);

        if (!quiet)
        {
            ConsoleReporter.Write(stdout, decision);
        }

        if (markdownPath is not null)
        {
            MarkdownReporter.Write(markdownPath, decision, baselineDoc.Suite);
        }

        if (jsonPath is not null)
        {
            JsonDecisionReporter.Write(jsonPath, decision);
        }

        return decision.ExitCode;
    }
}

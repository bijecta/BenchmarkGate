using Bijecta.BenchmarkGate.BenchmarkDotNet.Parsing;
using Bijecta.BenchmarkGate.Core.Baseline;
using Bijecta.BenchmarkGate.Core.Evaluation;
using Bijecta.BenchmarkGate.Core.Model;
using Bijecta.BenchmarkGate.Tool.Baseline;
using Bijecta.BenchmarkGate.Tool.Policy;
using Bijecta.BenchmarkGate.Tool.Reporting;

namespace Bijecta.BenchmarkGate.Tool.Commands;

/// <summary>
/// Implements <c>benchmark-gate check</c>. Argument acquisition lives in
/// Program.cs (System.CommandLine, per ADR-0002). This type orchestrates:
/// parse -> load baseline -> load policy -> evaluate -> report -> exit code.
/// </summary>
/// <remarks>
/// Report files are written independently (Markdown, JSON, JUnit are not a
/// transaction) — if a later report fails, earlier ones may already exist
/// on disk. That's deliberate: these are separate artifacts, not a single
/// atomic output, and best-effort sequential writing is the right model
/// here rather than a cross-file rollback.
/// </remarks>
internal static class CheckCommand
{
    public static int Run(
        string resultsPath,
        string baselinePath,
        string policyPath,
        string? markdownPath,
        string? jsonPath,
        string? junitPath,
        bool failOnWarning,
        bool quiet,
        TextWriter stdout,
        TextWriter stderr)
    {
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

        GatePolicy policy;
        try
        {
            policy = PolicyFile.Load(policyPath);
        }
        catch (PolicyFileException ex)
        {
            stderr.WriteLine($"Failed to load policy: {ex.Message}");
            return ExitCodes.InvalidBaselineOrPolicy;
        }

        var decision = RegressionEvaluator.Evaluate(observations, baseline, policy);

        if (!quiet)
        {
            ConsoleReporter.Write(stdout, decision);
        }

        try
        {
            if (markdownPath is not null)
            {
                MarkdownReporter.Write(markdownPath, decision, baseline.Suite);
            }

            if (jsonPath is not null)
            {
                JsonDecisionReporter.Write(jsonPath, decision, failOnWarning);
            }

            if (junitPath is not null)
            {
                JunitReporter.Write(junitPath, decision, baseline.Suite, failOnWarning);
            }
        }
        catch (ReportWriteException ex)
        {
            stderr.WriteLine($"Failed to write report: {ex.Message}");
            return ExitCodes.OutputWriteFailure;
        }

        return decision.GetExitCode(failOnWarning);
    }
}
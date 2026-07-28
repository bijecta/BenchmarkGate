using Bijecta.BenchmarkGate.BenchmarkDotNet.Parsing;
using Bijecta.BenchmarkGate.Core.Evaluation;
using Bijecta.BenchmarkGate.Core.Model;
using Bijecta.BenchmarkGate.Tool.Baseline;

namespace Bijecta.BenchmarkGate.Tool.Commands;

/// <summary>
/// Implements <c>benchmark-gate capture</c>. Argument acquisition lives in
/// Program.cs (System.CommandLine, per ADR-0002).
/// </summary>
internal static class CaptureCommand
{
    public static int Run(
        string resultsPath,
        string outputPath,
        string suite,
        bool overwrite,
        TextWriter stdout,
        TextWriter stderr)
    {
        if (string.IsNullOrWhiteSpace(suite))
        {
            stderr.WriteLine("Suite name must not be empty.");
            return ExitCodes.InvalidArguments;
        }

        suite = suite.Trim();

        // Friendly early check only — the real enforcement against a
        // time-of-check/time-of-use race lives in BaselineFile.WriteCandidate
        // (via AtomicFileWriter's File.Move overwrite: false).
        if (File.Exists(outputPath) && !overwrite)
        {
            stderr.WriteLine(
                $"'{outputPath}' already exists. Re-run with --overwrite if you intend to replace it. " +
                "Baseline changes should be reviewed like any other source change — see docs/baseline-governance.md.");
            return ExitCodes.InvalidArguments;
        }

        BenchmarkObservation[] observations;
        try
        {
            observations = BenchmarkDotNetResultParser.ParsePath(resultsPath).ToArray();
        }
        catch (BenchmarkResultParseException ex)
        {
            stderr.WriteLine($"Failed to parse results: {ex.Message}");
            return ExitCodes.UnsupportedSchema;
        }

        if (observations.Length == 0)
        {
            stderr.WriteLine("No benchmark observations were found. Refusing to create an empty baseline candidate.");
            return ExitCodes.UnsupportedSchema;
        }

        try
        {
            BaselineFile.WriteCandidate(outputPath, suite, observations, overwrite);
        }
        catch (BaselineWriteException ex)
        {
            stderr.WriteLine($"Failed to write baseline candidate: {ex.Message}");
            return ExitCodes.OutputWriteFailure;
        }

        stdout.WriteLine($"Wrote baseline candidate with {observations.Length} benchmark(s) to '{outputPath}'.");
        stdout.WriteLine("This is a candidate — review it like a normal source change before committing it as the approved baseline.");

        return ExitCodes.Passed;
    }
}
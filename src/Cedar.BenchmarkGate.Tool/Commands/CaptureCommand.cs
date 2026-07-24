using System.Globalization;
using Cedar.BenchmarkGate.BenchmarkDotNet.Parsing;
using Cedar.BenchmarkGate.Core.Evaluation;
using Cedar.BenchmarkGate.Tool.Baseline;

namespace Cedar.BenchmarkGate.Tool.Commands;

/// <summary>
/// Implements `cedar-benchmark-gate capture`. Argument acquisition lives in
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
        if (File.Exists(outputPath) && !overwrite)
        {
            stderr.WriteLine(
                $"'{outputPath}' already exists. Re-run with --overwrite if you intend to replace it. " +
                "Baseline changes should be reviewed like any other source change — see docs/baseline-governance.md.");
            return ExitCodes.InvalidArguments;
        }

        Core.Model.BenchmarkObservation[] observations;
        try
        {
            observations = BenchmarkDotNetResultParser.ParsePath(resultsPath).ToArray();
        }
        catch (BenchmarkResultParseException ex)
        {
            stderr.WriteLine($"Failed to parse results: {ex.Message}");
            return ExitCodes.UnsupportedSchema;
        }

        BaselineFile.WriteCandidate(outputPath, suite, observations);
        stdout.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"Wrote baseline candidate with {observations.Length} benchmark(s) to '{outputPath}'."));
        stdout.WriteLine("This is a candidate — review it like a normal source change before committing it as the approved baseline.");

        return ExitCodes.Passed;
    }
}

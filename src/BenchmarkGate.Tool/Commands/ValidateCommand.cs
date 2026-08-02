using Bijecta.BenchmarkGate.BenchmarkDotNet.Validation;
using Bijecta.BenchmarkGate.Core.Evaluation;
using Bijecta.BenchmarkGate.Reporting;
using Bijecta.BenchmarkGate.Tool.Baseline;
using Bijecta.BenchmarkGate.Tool.Policy;

namespace Bijecta.BenchmarkGate.Tool.Commands;

internal static class ValidateCommand
{
    public static int Run(
        string? policyPath, string? baselinePath, string? resultsPath,
        string? jsonPath, bool quiet, TextWriter stdout, TextWriter stderr)
    {
        if (policyPath is null && baselinePath is null && resultsPath is null)
        {
            stderr.WriteLine("At least one of --policy, --baseline, or --results is required.");
            return ExitCodes.InvalidArguments;
        }

        var artifacts = new List<ArtifactValidationResult>();

        if (policyPath is not null)
            artifacts.Add(ValidatePolicy(policyPath));
        if (baselinePath is not null)
            artifacts.Add(ValidateBaseline(baselinePath));
        if (resultsPath is not null)
            artifacts.AddRange(ValidateResults(resultsPath));

        if (!quiet)
        {
            ConsoleValidationReporter.Write(stdout,
                artifacts.Select(a => (a.Source, a.Validation, a.FailureMessage)).ToList());
        }

        if (jsonPath is not null)
        {
            try
            {
                JsonValidationReporter.Write(jsonPath,
                    artifacts.Select(a => (a.Kind.ToString(), a.Source, a.Validation, a.FailureMessage)).ToList());
            }
            catch (ReportWriteException ex)
            {
                stderr.WriteLine($"Failed to write validation report: {ex.Message}");
                return ExitCodes.OutputWriteFailure;
            }
        }

        return artifacts.All(a => a.IsSuccessful) ? ExitCodes.Passed : ExitCodes.ValidationFailed;
    }

    private static ArtifactValidationResult ValidatePolicy(string path)
    {
        try
        {
            var validation = PolicyFile.Validate(path);
            return new ArtifactValidationResult(ValidationArtifactKind.Policy, path, validation, null);
        }
        catch (PolicyFileException ex)
        {
            return new ArtifactValidationResult(
                ValidationArtifactKind.Policy, path, ex.ValidationResult,
                ex.ValidationResult is null ? ex.Message : null);
        }
    }

    private static ArtifactValidationResult ValidateBaseline(string path)
    {
        try
        {
            var validation = BaselineFile.Validate(path);
            return new ArtifactValidationResult(ValidationArtifactKind.Baseline, path, validation, null);
        }
        catch (BaselineFileException ex)
        {
            return new ArtifactValidationResult(
                ValidationArtifactKind.Baseline, path, ex.ValidationResult,
                ex.ValidationResult is null ? ex.Message : null);
        }
    }

    private static List<ArtifactValidationResult> ValidateResults(string path)
    {
        var results = BenchmarkDotNetInputValidator.ValidatePath(path);
        return results
            .Select(r => new ArtifactValidationResult(
                ValidationArtifactKind.BenchmarkDotNetResults, r.SourceFile, r.Validation, null))
            .ToList();
    }
}
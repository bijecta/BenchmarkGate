using Bijecta.BenchmarkGate.Core.Validation;

namespace Bijecta.BenchmarkGate.Reporting;

public static class ConsoleValidationReporter
{
    public static void Write(
        TextWriter output,
        IReadOnlyList<(string Source, ValidationResult? Validation, string? FailureMessage)> artifacts)
    {
        foreach (var artifact in artifacts)
        {
            output.WriteLine(artifact.Source);

            if (artifact.FailureMessage is not null)
            {
                ConsoleColorWriter.WriteLine(output, ConsoleColor.Red, $"  {artifact.FailureMessage}");
                output.WriteLine();
                continue;
            }

            if (artifact.Validation!.Diagnostics.Count == 0)
            {
                output.WriteLine("  valid, no findings.");
                output.WriteLine();
                continue;
            }

            foreach (var diagnostic in artifact.Validation.Diagnostics)
            {
                var color = diagnostic.Severity == DiagnosticSeverity.Error ? ConsoleColor.Red : ConsoleColor.Yellow;
                var label = diagnostic.Severity.ToString().ToUpperInvariant();
                ConsoleColorWriter.WriteLine(output, color,
                    $"  {label} {diagnostic.Descriptor.Id} {diagnostic.Path}: {diagnostic.Message}");
            }
            output.WriteLine();
        }
    }
}
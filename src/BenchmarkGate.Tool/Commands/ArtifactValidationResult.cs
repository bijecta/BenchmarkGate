using Bijecta.BenchmarkGate.Core.Validation;

namespace Bijecta.BenchmarkGate.Tool.Commands;

internal enum ValidationArtifactKind
{
    Policy,
    Baseline,
    BenchmarkDotNetResults,
}

internal sealed record ArtifactValidationResult(
    ValidationArtifactKind Kind,
    string Source,
    ValidationResult? Validation,
    string? FailureMessage)
{
    internal bool IsSuccessful =>
        FailureMessage is null && (Validation?.IsValid ?? false);
}
using Bijecta.BenchmarkGate.Core.Validation;
using FluentAssertions;
using Xunit;

namespace Bijecta.BenchmarkGate.Core.Tests.Validation;

public class ValidationResultTests
{
    private static readonly DiagnosticDescriptor ErrorDescriptor =
        new("BGV901", "Test error", DiagnosticSeverity.Error);

    private static readonly DiagnosticDescriptor WarningDescriptor =
        new("BGV902", "Test warning", DiagnosticSeverity.Warning);

    [Fact]
    public void Empty_diagnostics_is_valid()
    {
        var result = new ValidationResult([]);

        result.IsValid.Should().BeTrue();
        result.ErrorCount.Should().Be(0);
        result.WarningCount.Should().Be(0);
    }

    [Fact]
    public void Any_error_diagnostic_makes_result_invalid()
    {
        var result = new ValidationResult([
            new ValidationDiagnostic(ErrorDescriptor, "/path", "boom")
        ]);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Only_warning_diagnostics_is_still_valid()
    {
        var result = new ValidationResult([
            new ValidationDiagnostic(WarningDescriptor, "/path", "hmm")
        ]);

        result.IsValid.Should().BeTrue();
        result.WarningCount.Should().Be(1);
    }

    [Fact]
    public void Error_and_warning_counts_are_independent()
    {
        var result = new ValidationResult([
            new ValidationDiagnostic(ErrorDescriptor, "/a", "e1"),
            new ValidationDiagnostic(ErrorDescriptor, "/b", "e2"),
            new ValidationDiagnostic(WarningDescriptor, "/c", "w1")
        ]);

        result.ErrorCount.Should().Be(2);
        result.WarningCount.Should().Be(1);
    }
}
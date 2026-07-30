using Bijecta.BenchmarkGate.Core.Validation;
using FluentAssertions;
using Xunit;

namespace Bijecta.BenchmarkGate.Core.Tests.Validation;

public class ValidationDiagnosticTests
{
    [Fact]
    public void Severity_reflects_descriptor_default_severity()
    {
        var descriptor = new DiagnosticDescriptor("BGV903", "Test", DiagnosticSeverity.Error);
        var diagnostic = new ValidationDiagnostic(descriptor, "/path", "message");

        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
    }

    [Fact]
    public void Descriptor_help_link_defaults_to_null()
    {
        var descriptor = new DiagnosticDescriptor("BGV904", "Test", DiagnosticSeverity.Warning);

        descriptor.HelpLink.Should().BeNull();
    }
}
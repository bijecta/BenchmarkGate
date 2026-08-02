using Bijecta.BenchmarkGate.Core.Validation;
using Bijecta.BenchmarkGate.Reporting;
using FluentAssertions;
using Xunit;

namespace Bijecta.BenchmarkGate.Reporting.Tests;

public class ConsoleValidationReporterTests
{
    private static readonly DiagnosticDescriptor ErrorDescriptor =
        new("BGV101", "Test error", DiagnosticSeverity.Error);
    private static readonly DiagnosticDescriptor WarningDescriptor =
        new("BGV102", "Test warning", DiagnosticSeverity.Warning);

    [Fact]
    public void Valid_artifact_with_no_diagnostics_prints_valid_line()
    {
        var writer = new StringWriter();
        var artifacts = new List<(string, ValidationResult?, string?)>
        {
            ("policy.json", new ValidationResult([]), null),
        };

        ConsoleValidationReporter.Write(writer, artifacts);

        writer.ToString().Should().Contain("policy.json");
        writer.ToString().Should().Contain("valid, no findings");
    }

    [Fact]
    public void Diagnostics_are_printed_with_code_path_and_message()
    {
        var writer = new StringWriter();
        var result = new ValidationResult([
            new ValidationDiagnostic(ErrorDescriptor, "/stability", "boom"),
        ]);
        var artifacts = new List<(string, ValidationResult?, string?)> { ("policy.json", result, null) };

        ConsoleValidationReporter.Write(writer, artifacts);

        var output = writer.ToString();
        output.Should().Contain("BGV101");
        output.Should().Contain("/stability");
        output.Should().Contain("boom");
    }

    [Fact]
    public void Failure_message_is_printed_when_validation_is_null()
    {
        var writer = new StringWriter();
        var artifacts = new List<(string, ValidationResult?, string?)>
        {
            ("missing.json", null, "File does not exist."),
        };

        ConsoleValidationReporter.Write(writer, artifacts);

        writer.ToString().Should().Contain("File does not exist.");
    }

    [Fact]
    public void Multiple_artifacts_are_each_printed_under_their_own_source_heading()
    {
        var writer = new StringWriter();
        var artifacts = new List<(string, ValidationResult?, string?)>
        {
            ("policy.json", new ValidationResult([]), null),
            ("baseline.json", new ValidationResult([]), null),
        };

        ConsoleValidationReporter.Write(writer, artifacts);

        var output = writer.ToString();
        output.Should().Contain("policy.json");
        output.Should().Contain("baseline.json");
    }

    [Fact]
    public void StringWriter_output_receives_no_console_color_escape_codes()
    {
        // ConsoleColorWriter only applies color when writer is literally
        // Console.Out — a StringWriter must get plain, deterministic text.
        var writer = new StringWriter();
        var result = new ValidationResult([
            new ValidationDiagnostic(ErrorDescriptor, "/a", "e1"),
            new ValidationDiagnostic(WarningDescriptor, "/b", "w1"),
        ]);
        var artifacts = new List<(string, ValidationResult?, string?)> { ("x.json", result, null) };

        ConsoleValidationReporter.Write(writer, artifacts);

        writer.ToString().Should().NotContain("\u001b["); // no ANSI escape sequences
    }
}
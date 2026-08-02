using System.Text.Json;
using Bijecta.BenchmarkGate.Core.Validation;
using Bijecta.BenchmarkGate.Reporting;
using FluentAssertions;
using Xunit;

namespace Bijecta.BenchmarkGate.Reporting.Tests;

public class JsonValidationReporterTests : IDisposable
{
    private readonly string _tempFile = Path.GetTempFileName();

    public void Dispose()
    {
        File.Delete(_tempFile);
        GC.SuppressFinalize(this);
    }

    private static readonly DiagnosticDescriptor ErrorDescriptor =
        new("BGV101", "Test error", DiagnosticSeverity.Error);

    [Fact]
    public void Writes_top_level_aggregate_fields()
    {
        var artifacts = new List<(string, string, ValidationResult?, string?)>
        {
            ("policy", "policy.json", new ValidationResult([
                new ValidationDiagnostic(ErrorDescriptor, "/a", "boom"),
            ]), null),
        };

        JsonValidationReporter.Write(_tempFile, artifacts);

        using var json = JsonDocument.Parse(File.ReadAllText(_tempFile));
        var root = json.RootElement;

        root.GetProperty("schemaVersion").GetInt32().Should().Be(1);
        root.GetProperty("isValid").GetBoolean().Should().BeFalse();
        root.GetProperty("errorCount").GetInt32().Should().Be(1);
        root.GetProperty("warningCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public void Writes_one_artifact_entry_per_input()
    {
        var artifacts = new List<(string, string, ValidationResult?, string?)>
        {
            ("policy", "policy.json", new ValidationResult([]), null),
            ("baseline", "baseline.json", new ValidationResult([]), null),
        };

        JsonValidationReporter.Write(_tempFile, artifacts);

        using var json = JsonDocument.Parse(File.ReadAllText(_tempFile));
        json.RootElement.GetProperty("artifacts").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public void Diagnostic_fields_use_stable_report_dto_names()
    {
        var artifacts = new List<(string, string, ValidationResult?, string?)>
        {
            ("policy", "policy.json", new ValidationResult([
                new ValidationDiagnostic(ErrorDescriptor, "/stability", "boom"),
            ]), null),
        };

        JsonValidationReporter.Write(_tempFile, artifacts);

        using var json = JsonDocument.Parse(File.ReadAllText(_tempFile));
        var diagnostic = json.RootElement.GetProperty("artifacts")[0].GetProperty("diagnostics")[0];

        diagnostic.GetProperty("code").GetString().Should().Be("BGV101");
        diagnostic.GetProperty("severity").GetString().Should().Be("Error");
        diagnostic.GetProperty("path").GetString().Should().Be("/stability");
        diagnostic.GetProperty("message").GetString().Should().Be("boom");
    }

    [Fact]
    public void Artifact_with_failure_message_has_no_validation_but_still_reports()
    {
        var artifacts = new List<(string, string, ValidationResult?, string?)>
        {
            ("policy", "missing.json", null, "File does not exist."),
        };

        JsonValidationReporter.Write(_tempFile, artifacts);

        using var json = JsonDocument.Parse(File.ReadAllText(_tempFile));
        var artifact = json.RootElement.GetProperty("artifacts")[0];

        artifact.GetProperty("isValid").GetBoolean().Should().BeFalse();
        artifact.GetProperty("diagnostics").GetArrayLength().Should().Be(0);
    }
}
using Bijecta.BenchmarkGate.Core.Evaluation;
using Bijecta.BenchmarkGate.Tool.Commands;
using FluentAssertions;
using Xunit;

namespace Bijecta.BenchmarkGate.Tool.Tests.Commands;

public sealed class ValidateCommandTests : IDisposable
{
    private readonly string _tempDirectory =
        Path.Combine(Path.GetTempPath(), $"validate-command-tests-{Guid.NewGuid():N}");

    public ValidateCommandTests() => Directory.CreateDirectory(_tempDirectory);

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, recursive: true);
    }

    private string WriteFile(string name, string content)
    {
        var path = Path.Combine(_tempDirectory, name);
        File.WriteAllText(path, content);
        return path;
    }

    private const string ValidPolicy = """
        { "schemaVersion": 1, "stability": { "minimumMeasurements": 3, "maximumCoefficientOfVariation": 0.1 },
          "metrics": { "meanNanoseconds": { "direction": "lower-is-better", "warningPercent": 5, "failurePercent": 10 } } }
        """;

    private const string InvalidPolicy = """
        { "schemaVersion": 1, "stability": { "minimumMeasurements": 0, "maximumCoefficientOfVariation": 0.1 },
          "metrics": {} }
        """;

    private const string ValidBaseline = """
        { "schemaVersion": 2, "suite": "S",
          "benchmarks": [ { "identity": { "typeName": "T", "methodName": "M" }, "metrics": { "meanNanoseconds": 1 } } ] }
        """;

    [Fact]
    public void No_flags_returns_InvalidArguments()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = ValidateCommand.Run(null, null, null, null, false, stdout, stderr);

        exitCode.Should().Be(ExitCodes.InvalidArguments);
        stderr.ToString().Should().Contain("--policy");
    }

    [Fact]
    public void Valid_policy_alone_returns_Passed()
    {
        var path = WriteFile("policy.json", ValidPolicy);
        var stdout = new StringWriter();

        var exitCode = ValidateCommand.Run(path, null, null, null, false, stdout, new StringWriter());

        exitCode.Should().Be(ExitCodes.Passed);
    }

    [Fact]
    public void Invalid_policy_alone_returns_ValidationFailed()
    {
        var path = WriteFile("policy.json", InvalidPolicy);

        var exitCode = ValidateCommand.Run(path, null, null, null, false, new StringWriter(), new StringWriter());

        exitCode.Should().Be(ExitCodes.ValidationFailed);
    }

    [Fact]
    public void Valid_baseline_alone_returns_Passed()
    {
        var path = WriteFile("baseline.json", ValidBaseline);

        var exitCode = ValidateCommand.Run(null, path, null, null, false, new StringWriter(), new StringWriter());

        exitCode.Should().Be(ExitCodes.Passed);
    }

    [Fact]
    public void Valid_results_alone_returns_Passed()
    {
        var path = WriteFile("results.json", """
            { "Benchmarks": [ { "Type": "T", "Method": "M", "Statistics": { "Mean": 1.0 } } ] }
            """);

        var exitCode = ValidateCommand.Run(null, null, path, null, false, new StringWriter(), new StringWriter());

        exitCode.Should().Be(ExitCodes.Passed);
    }

    [Fact]
    public void Multiple_artifacts_together_all_valid_returns_Passed()
    {
        var policyPath = WriteFile("policy.json", ValidPolicy);
        var baselinePath = WriteFile("baseline.json", ValidBaseline);

        var exitCode = ValidateCommand.Run(policyPath, baselinePath, null, null, false, new StringWriter(), new StringWriter());

        exitCode.Should().Be(ExitCodes.Passed);
    }

    [Fact]
    public void Multiple_artifacts_one_invalid_returns_ValidationFailed_and_reports_both()
    {
        var policyPath = WriteFile("policy.json", ValidPolicy);
        var baselinePath = WriteFile("baseline.json", "{ not valid json");
        var stdout = new StringWriter();

        var exitCode = ValidateCommand.Run(policyPath, baselinePath, null, null, false, stdout, new StringWriter());

        exitCode.Should().Be(ExitCodes.ValidationFailed);
        stdout.ToString().Should().Contain("policy.json");
        stdout.ToString().Should().Contain("baseline.json");
    }

    [Fact]
    public void Nonexistent_policy_path_returns_ValidationFailed()
    {
        var path = Path.Combine(_tempDirectory, "missing-policy.json");

        var exitCode = ValidateCommand.Run(path, null, null, null, false, new StringWriter(), new StringWriter());

        exitCode.Should().Be(ExitCodes.ValidationFailed);
    }

    [Fact]
    public void Nonexistent_baseline_path_returns_ValidationFailed()
    {
        var path = Path.Combine(_tempDirectory, "missing-baseline.json");

        var exitCode = ValidateCommand.Run(null, path, null, null, false, new StringWriter(), new StringWriter());

        exitCode.Should().Be(ExitCodes.ValidationFailed);
    }

    [Fact]
    public void Nonexistent_results_path_returns_ValidationFailed()
    {
        var path = Path.Combine(_tempDirectory, "missing-results.json");

        var exitCode = ValidateCommand.Run(null, null, path, null, false, new StringWriter(), new StringWriter());

        exitCode.Should().Be(ExitCodes.ValidationFailed);
    }

    [Fact]
    public void Quiet_suppresses_console_output()
    {
        var path = WriteFile("policy.json", ValidPolicy);
        var stdout = new StringWriter();

        ValidateCommand.Run(path, null, null, null, true, stdout, new StringWriter());

        stdout.ToString().Should().BeEmpty();
    }

    [Fact]
    public void Json_option_writes_a_report_file()
    {
        var path = WriteFile("policy.json", ValidPolicy);
        var jsonPath = Path.Combine(_tempDirectory, "report.json");

        ValidateCommand.Run(path, null, null, jsonPath, false, new StringWriter(), new StringWriter());

        File.Exists(jsonPath).Should().BeTrue();
    }
}
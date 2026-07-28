using System.Text;
using Bijecta.BenchmarkGate.Tool.Commands;
using FluentAssertions;
using Xunit;

namespace Bijecta.BenchmarkGate.Tool.Tests.Commands;

public sealed class CheckCommandTests : IDisposable
{
    private readonly List<string> _temporaryFiles = [];
    private readonly string _temporaryDirectory =
        Path.Combine(Path.GetTempPath(), $"check-command-tests-{Guid.NewGuid():N}");

    public CheckCommandTests()
    {
        Directory.CreateDirectory(_temporaryDirectory);
    }

    public void Dispose()
    {
        foreach (var path in _temporaryFiles)
        {
            try { File.Delete(path); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        try { Directory.Delete(_temporaryDirectory, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private string WriteTempFile(string content)
    {
        var path = Path.Combine(_temporaryDirectory, $"{Guid.NewGuid():N}.json");
        File.WriteAllText(path, content, Encoding.UTF8);
        _temporaryFiles.Add(path);
        return path;
    }

    private string TempOutputPath() =>
        Path.Combine(_temporaryDirectory, $"out-{Guid.NewGuid():N}.txt");

    // A single benchmark with mean 1000ns, no allocation metric.
    private const string ResultsPassing = """
        { "Title": "t", "Benchmarks": [
          { "Type": "Ns.Type", "Method": "Method", "Parameters": "",
            "Statistics": { "N": 10, "Mean": 1000.0, "StandardDeviation": 10.0 } }
        ] }
        """;

    // Same benchmark, but a 20% regression against a 1000ns baseline.
    private const string ResultsRegressed = """
        { "Title": "t", "Benchmarks": [
          { "Type": "Ns.Type", "Method": "Method", "Parameters": "",
            "Statistics": { "N": 10, "Mean": 1200.0, "StandardDeviation": 10.0 } }
        ] }
        """;

    // 10% change — over a 5% warning threshold, under a 15% failure threshold.
    private const string ResultsWarning = """
        { "Title": "t", "Benchmarks": [
          { "Type": "Ns.Type", "Method": "Method", "Parameters": "",
            "Statistics": { "N": 10, "Mean": 1100.0, "StandardDeviation": 10.0 } }
        ] }
        """;

    private const string BaselineDocument = """
        { "schemaVersion": 2, "suite": "suite",
          "benchmarks": [
            { "identity": { "typeName": "Ns.Type", "methodName": "Method", "job": "Default" },
              "metrics": { "meanNanoseconds": 1000.0 } }
          ] }
        """;

    private const string PolicyDocument = """
        { "schemaVersion": 1,
          "stability": { "minimumMeasurements": 1, "maximumCoefficientOfVariation": 1.0 },
          "metrics": {
            "meanNanoseconds": { "direction": "lower-is-better", "warningPercent": 5, "failurePercent": 15 }
          } }
        """;

    private static (int ExitCode, string Stdout, string Stderr) Run(
        string resultsPath, string baselinePath, string policyPath,
        string? markdownPath = null, string? jsonPath = null, string? junitPath = null,
        bool failOnWarning = false, bool quiet = false)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = CheckCommand.Run(
            resultsPath, baselinePath, policyPath,
            markdownPath, jsonPath, junitPath,
            failOnWarning, quiet, stdout, stderr);

        return (exitCode, stdout.ToString(), stderr.ToString());
    }

    [Fact]
    public void Passing_suite_returns_passed_exit_code()
    {
        var results = WriteTempFile(ResultsPassing);
        var baseline = WriteTempFile(BaselineDocument);
        var policy = WriteTempFile(PolicyDocument);

        var (exitCode, stdout, _) = Run(results, baseline, policy);

        exitCode.Should().Be(Core.Evaluation.ExitCodes.Passed);
        stdout.Should().Contain("PASSED");
    }

    [Fact]
    public void Regressed_suite_returns_regressed_exit_code()
    {
        var results = WriteTempFile(ResultsRegressed);
        var baseline = WriteTempFile(BaselineDocument);
        var policy = WriteTempFile(PolicyDocument);

        var (exitCode, stdout, _) = Run(results, baseline, policy);

        exitCode.Should().Be(Core.Evaluation.ExitCodes.Regressed);
        stdout.Should().Contain("REGRESSED");
    }

    [Fact]
    public void Malformed_results_file_returns_unsupported_schema_exit_code_with_stderr_message()
    {
        var results = WriteTempFile("{ not valid json");
        var baseline = WriteTempFile(BaselineDocument);
        var policy = WriteTempFile(PolicyDocument);

        var (exitCode, _, stderr) = Run(results, baseline, policy);

        exitCode.Should().Be(Core.Evaluation.ExitCodes.UnsupportedSchema);
        stderr.Should().Contain("Failed to parse results");
    }

    [Fact]
    public void Malformed_baseline_file_returns_invalid_baseline_or_policy_exit_code()
    {
        var results = WriteTempFile(ResultsPassing);
        var baseline = WriteTempFile("{ not valid json");
        var policy = WriteTempFile(PolicyDocument);

        var (exitCode, _, stderr) = Run(results, baseline, policy);

        exitCode.Should().Be(Core.Evaluation.ExitCodes.InvalidBaselineOrPolicy);
        stderr.Should().Contain("Failed to load baseline");
    }

    [Fact]
    public void Malformed_policy_file_returns_invalid_baseline_or_policy_exit_code()
    {
        var results = WriteTempFile(ResultsPassing);
        var baseline = WriteTempFile(BaselineDocument);
        var policy = WriteTempFile("{ not valid json");

        var (exitCode, _, stderr) = Run(results, baseline, policy);

        exitCode.Should().Be(Core.Evaluation.ExitCodes.InvalidBaselineOrPolicy);
        stderr.Should().Contain("Failed to load policy");
    }

    [Fact]
    public void Quiet_suppresses_console_output_but_still_writes_requested_reports_and_exit_code()
    {
        var results = WriteTempFile(ResultsRegressed);
        var baseline = WriteTempFile(BaselineDocument);
        var policy = WriteTempFile(PolicyDocument);
        var jsonPath = TempOutputPath();
        _temporaryFiles.Add(jsonPath);

        var (exitCode, stdout, _) = Run(results, baseline, policy, jsonPath: jsonPath, quiet: true);

        stdout.Should().BeEmpty();
        exitCode.Should().Be(Core.Evaluation.ExitCodes.Regressed);
        File.Exists(jsonPath).Should().BeTrue();
    }

    [Fact]
    public void Markdown_json_and_junit_reports_are_all_written_when_requested()
    {
        var results = WriteTempFile(ResultsPassing);
        var baseline = WriteTempFile(BaselineDocument);
        var policy = WriteTempFile(PolicyDocument);
        var markdownPath = TempOutputPath();
        var jsonPath = TempOutputPath();
        var junitPath = TempOutputPath();
        _temporaryFiles.AddRange([markdownPath, jsonPath, junitPath]);

        Run(results, baseline, policy, markdownPath, jsonPath, junitPath);

        File.Exists(markdownPath).Should().BeTrue();
        File.Exists(jsonPath).Should().BeTrue();
        File.Exists(junitPath).Should().BeTrue();
    }

    [Fact]
    public void Invalid_report_output_path_returns_output_write_failure_exit_code()
    {
        var results = WriteTempFile(ResultsPassing);
        var baseline = WriteTempFile(BaselineDocument);
        var policy = WriteTempFile(PolicyDocument);

        // AtomicFileWriter.PrepareTempPath auto-creates missing parent
        // directories, so a merely-nonexistent directory won't fail. To
        // force a real failure, make a path component collide with an
        // existing FILE — Directory.CreateDirectory can't create a
        // directory where a file of that name already exists.
        var blockingFile = Path.Combine(_temporaryDirectory, "blocking-file");
        File.WriteAllText(blockingFile, "not a directory");
        _temporaryFiles.Add(blockingFile);
        var invalidPath = Path.Combine(blockingFile, "report.json");

        var (exitCode, _, stderr) = Run(results, baseline, policy, jsonPath: invalidPath);

        exitCode.Should().Be(Core.Evaluation.ExitCodes.OutputWriteFailure);
        stderr.Should().Contain("Failed to write report");
    }

    // --- The cross-report failOnWarning consistency check the review asked for. ---

    [Fact]
    public void Warning_only_suite_with_fail_on_warning_true_agrees_across_exit_code_json_and_junit()
    {
        var results = WriteTempFile(ResultsWarning);
        var baseline = WriteTempFile(BaselineDocument);
        var policy = WriteTempFile(PolicyDocument);
        var jsonPath = TempOutputPath();
        var junitPath = TempOutputPath();
        _temporaryFiles.AddRange([jsonPath, junitPath]);

        var (exitCode, stdout, _) = Run(
            results, baseline, policy, jsonPath: jsonPath, junitPath: junitPath, failOnWarning: true);

        exitCode.Should().Be(Core.Evaluation.ExitCodes.Warning);
        exitCode.Should().NotBe(Core.Evaluation.ExitCodes.Passed);

        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(jsonPath));
        doc.RootElement.GetProperty("exitCode").GetInt32().Should().Be(Core.Evaluation.ExitCodes.Warning);

        var junitXml = File.ReadAllText(junitPath);
        junitXml.Should().Contain("<failure");

        stdout.Should().Contain("WARNING");
        stdout.Should().NotContain("REGRESSED");
    }

    [Fact]
    public void Warning_only_suite_with_fail_on_warning_false_passes_everywhere_but_still_shows_warning_status()
    {
        var results = WriteTempFile(ResultsWarning);
        var baseline = WriteTempFile(BaselineDocument);
        var policy = WriteTempFile(PolicyDocument);
        var jsonPath = TempOutputPath();
        var junitPath = TempOutputPath();
        _temporaryFiles.AddRange([jsonPath, junitPath]);

        var (exitCode, stdout, _) = Run(
            results, baseline, policy, jsonPath: jsonPath, junitPath: junitPath, failOnWarning: false);

        exitCode.Should().Be(Core.Evaluation.ExitCodes.Passed);

        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(jsonPath));
        doc.RootElement.GetProperty("exitCode").GetInt32().Should().Be(Core.Evaluation.ExitCodes.Passed);

        var junitXml = File.ReadAllText(junitPath);
        junitXml.Should().NotContain("<failure");

        stdout.Should().Contain("WARNING");
    }
}
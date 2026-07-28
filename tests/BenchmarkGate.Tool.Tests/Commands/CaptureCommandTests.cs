using System.Text;
using Bijecta.BenchmarkGate.Tool.Commands;
using FluentAssertions;
using Xunit;

namespace Bijecta.BenchmarkGate.Tool.Tests.Commands;

public sealed class CaptureCommandTests : IDisposable
{
    private readonly string _tempDirectory =
        Path.Combine(Path.GetTempPath(), $"capture-command-tests-{Guid.NewGuid():N}");

    public CaptureCommandTests()
    {
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private string PathIn(string fileName) => Path.Combine(_tempDirectory, fileName);

    private string WriteResultsFile(string content)
    {
        var path = PathIn($"{Guid.NewGuid():N}.json");
        File.WriteAllText(path, content, Encoding.UTF8);
        return path;
    }

    private const string OneBenchmarkResults = """
        { "Title": "t", "Benchmarks": [
          { "Type": "Ns.Type", "Method": "Method", "Parameters": "",
            "Statistics": { "N": 10, "Mean": 1000.0, "StandardDeviation": 10.0 } }
        ] }
        """;

    private const string EmptyResults = """
        { "Title": "t", "Benchmarks": [] }
        """;

    private static (int ExitCode, string Stdout, string Stderr) Run(
        string resultsPath, string outputPath, string suite, bool overwrite = false)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exitCode = CaptureCommand.Run(resultsPath, outputPath, suite, overwrite, stdout, stderr);
        return (exitCode, stdout.ToString(), stderr.ToString());
    }

    [Fact]
    public void Writes_a_baseline_candidate_and_returns_passed()
    {
        var results = WriteResultsFile(OneBenchmarkResults);
        var output = PathIn("baseline.json");

        var (exitCode, stdout, _) = Run(results, output, "suite");

        exitCode.Should().Be(Core.Evaluation.ExitCodes.Passed);
        File.Exists(output).Should().BeTrue();
        stdout.Should().Contain("Wrote baseline candidate with 1 benchmark(s)");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_or_whitespace_suite_name_is_rejected(string suite)
    {
        var results = WriteResultsFile(OneBenchmarkResults);
        var output = PathIn("baseline.json");

        var (exitCode, _, stderr) = Run(results, output, suite);

        exitCode.Should().Be(Core.Evaluation.ExitCodes.InvalidArguments);
        stderr.Should().Contain("Suite name must not be empty");
        File.Exists(output).Should().BeFalse();
    }

    [Fact]
    public void Suite_name_is_trimmed()
    {
        var results = WriteResultsFile(OneBenchmarkResults);
        var output = PathIn("baseline.json");

        Run(results, output, "  suite  ");

        File.ReadAllText(output).Should().Contain("\"suite\": \"suite\"");
    }

    [Fact]
    public void Existing_output_without_overwrite_is_rejected_before_parsing()
    {
        var results = WriteResultsFile(OneBenchmarkResults);
        var output = PathIn("baseline.json");
        File.WriteAllText(output, "existing content");

        var (exitCode, _, stderr) = Run(results, output, "suite", overwrite: false);

        exitCode.Should().Be(Core.Evaluation.ExitCodes.InvalidArguments);
        stderr.Should().Contain("already exists");
        File.ReadAllText(output).Should().Be("existing content");
    }

    [Fact]
    public void Existing_output_with_overwrite_is_replaced()
    {
        var results = WriteResultsFile(OneBenchmarkResults);
        var output = PathIn("baseline.json");
        File.WriteAllText(output, "existing content");

        var (exitCode, _, _) = Run(results, output, "suite", overwrite: true);

        exitCode.Should().Be(Core.Evaluation.ExitCodes.Passed);
        File.ReadAllText(output).Should().NotBe("existing content");
    }

    [Fact]
    public void Malformed_results_file_returns_unsupported_schema_exit_code()
    {
        var results = WriteResultsFile("{ not valid json");
        var output = PathIn("baseline.json");

        var (exitCode, _, stderr) = Run(results, output, "suite");

        exitCode.Should().Be(Core.Evaluation.ExitCodes.UnsupportedSchema);
        stderr.Should().Contain("Failed to parse results");
        File.Exists(output).Should().BeFalse();
    }

    [Fact]
    public void Empty_benchmarks_array_is_rejected_rather_than_creating_an_empty_baseline()
    {
        var results = WriteResultsFile(EmptyResults);
        var output = PathIn("baseline.json");

        // Note: an empty "benchmarks": [] array in the *results* file is
        // actually rejected earlier, by BenchmarkDotNetResultParser itself
        // ("Result file contains no 'Benchmarks' array, or it is empty"),
        // which throws BenchmarkResultParseException before CaptureCommand's
        // own zero-observations check is ever reached. This test therefore
        // exercises the parser's empty-array rejection path, not
        // CaptureCommand's dedicated check — both converge on the same
        // UnsupportedSchema exit code, so the observable behavior is
        // correct either way, but the *reason* differs from what the
        // "Refusing to create an empty baseline candidate" message implies.
        var (exitCode, _, stderr) = Run(results, output, "suite");

        exitCode.Should().Be(Core.Evaluation.ExitCodes.UnsupportedSchema);
        File.Exists(output).Should().BeFalse();
    }

    [Fact]
    public void Existing_output_still_rejected_by_early_check_when_overwrite_false()
    {
        var results = WriteResultsFile(OneBenchmarkResults);
        var output = PathIn("baseline.json");
        File.WriteAllText(output, "existing content");

        var (exitCode, _, stderr) = Run(results, output, "suite", overwrite: false);

        exitCode.Should().Be(Core.Evaluation.ExitCodes.InvalidArguments);
        stderr.Should().Contain("already exists");
        // Note: this only exercises CaptureCommand's early File.Exists
        // check, not the deeper AtomicFileWriter/File.Move(overwrite:
        // false) enforcement — a single-threaded test can't genuinely
        // race a concurrent writer. The real proof that overwrite=false
        // is enforced atomically (not just checked early) is
        // AtomicFileWriterTests.Write_WithOverwriteFalse_ThrowsWhen...,
        // which bypasses any early check entirely.
    }
}
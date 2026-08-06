using Bijecta.BenchmarkGate.BenchmarkDotNet.Parsing;
using Bijecta.BenchmarkGate.Core.Comparison;
using Bijecta.BenchmarkGate.Core.Evaluation;
using Bijecta.BenchmarkGate.Core.Identity;
using Bijecta.BenchmarkGate.Core.Model;
using Bijecta.BenchmarkGate.Tool.Baseline;
using Bijecta.BenchmarkGate.Tool.Commands;
using FluentAssertions;
using Xunit;

namespace Bijecta.BenchmarkGate.Tool.Tests.Commands;

/// <summary>
/// NOTE: argument-validation and the WriteReport format/output/quiet
/// matrix are unit-level (bypass Run()'s parsing/baseline steps). The
/// Run()-level tests below use a real, trimmed BenchmarkDotNet full-report
/// JSON fixture (Fixtures/sample-results.json — 3 real benchmarks from an
/// actual run, not invented) parsed by the real BenchmarkDotNetResultParser,
/// with baselines built via the real BaselineFile.WriteCandidate from those
/// real parsed observations — this avoids guessing at the parser's
/// Identity.Job mapping or the baseline JSON schema by hand.
/// </summary>
public class CompareCommandTests
{
    private static readonly string FixturesDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures");
    private static string ResultsFixturePath => Path.Combine(FixturesDirectory, "sample-results.json");

    private static IReadOnlyList<BenchmarkObservation> ParseFixtureObservations() =>
        BenchmarkDotNetResultParser.ParsePath(ResultsFixturePath);

    /// <summary>
    /// Writes a baseline file from real parsed observations via the real
    /// BaselineFile.WriteCandidate — guarantees Identity (including Job)
    /// matches exactly what the real parser produces, no hand-guessing.
    /// </summary>
    private static string WriteBaseline(IReadOnlyList<BenchmarkObservation> observations, string suite = "test-suite")
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}-baseline.json");
        BaselineFile.WriteCandidate(path, suite, observations);
        return path;
    }

    /// <summary>
    /// A baseline-only observation with an invented identity — safe to
    /// fabricate since it's deliberately never meant to match anything in
    /// the real results fixture (used for the Removed-benchmark case).
    /// </summary>
    private static BenchmarkObservation InventedObservation() => new(
        new BenchmarkIdentity("Fake.Namespace.FakeType", "RemovedMethod", "Ci"),
        new Dictionary<string, double> { [BenchmarkObservation.MeanNanosecondsMetric] = 1000d },
        MeasurementCount: 20,
        StandardDeviationNanoseconds: 5.0);

    private static string TempPath(string extension) =>
        Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.{extension}");


    // ============================================================
    // Argument validation — no fixtures needed, checked before any I/O.
    // ============================================================

    [Fact]
    public void invalid_format_returns_invalid_arguments()
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = CompareCommand.Run(
            resultsPath: "nonexistent-results.json",
            baselinePath: "nonexistent-baseline.json",
            format: "xml",
            outputPath: null,
            quiet: false,
            stdout, stderr);

        exitCode.Should().Be(ExitCodes.InvalidArguments);
    }

    [Fact]
    public void json_format_without_output_returns_invalid_arguments()
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = CompareCommand.Run(
            resultsPath: "nonexistent-results.json",
            baselinePath: "nonexistent-baseline.json",
            format: "json",
            outputPath: null,
            quiet: false,
            stdout, stderr);

        exitCode.Should().Be(ExitCodes.InvalidArguments);
    }

    [Fact]
    public void markdown_format_without_output_returns_invalid_arguments()
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = CompareCommand.Run(
            resultsPath: "nonexistent-results.json",
            baselinePath: "nonexistent-baseline.json",
            format: "markdown",
            outputPath: null,
            quiet: false,
            stdout, stderr);

        exitCode.Should().Be(ExitCodes.InvalidArguments);
    }

    [Fact]
    public void console_format_without_output_passes_argument_validation()
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = CompareCommand.Run(
            resultsPath: "nonexistent-results.json",
            baselinePath: "nonexistent-baseline.json",
            format: "console",
            outputPath: null,
            quiet: false,
            stdout, stderr);

        exitCode.Should().Be(ExitCodes.UnsupportedSchema);
    }

    // ============================================================
    // Nonexistent path
    // ============================================================

    [Fact]
    public void nonexistent_results_path_returns_unsupported_schema()
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = CompareCommand.Run(
            resultsPath: Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}-does-not-exist.json"),
            baselinePath: "irrelevant.json",
            format: "console",
            outputPath: null,
            quiet: false,
            stdout, stderr);

        exitCode.Should().Be(ExitCodes.UnsupportedSchema);
    }

    [Fact]
    public void nonexistent_baseline_path_returns_invalid_baseline_or_policy()
    {
        // Now unblocked: results parses successfully (real fixture), so
        // Run() actually reaches the baseline-loading step.
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = CompareCommand.Run(
            resultsPath: ResultsFixturePath,
            baselinePath: Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}-does-not-exist.json"),
            format: "console",
            outputPath: null,
            quiet: true,
            stdout, stderr);

        exitCode.Should().Be(ExitCodes.InvalidBaselineOrPolicy);
    }

    // ============================================================
    // Valid comparison through Run() — real fixture end to end.
    // ============================================================

    [Fact]
    public void valid_comparison_with_a_matching_baseline_returns_passed()
    {
        var observations = ParseFixtureObservations();
        var baselinePath = WriteBaseline(observations);
        try
        {
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();

            var exitCode = CompareCommand.Run(
                ResultsFixturePath, baselinePath, "console", null, quiet: true, stdout, stderr);

            exitCode.Should().Be(ExitCodes.Passed);
        }
        finally
        {
            File.Delete(baselinePath);
        }
    }

    [Fact]
    public void console_format_through_run_writes_stdout()
    {
        var observations = ParseFixtureObservations();
        var baselinePath = WriteBaseline(observations);
        try
        {
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();

            CompareCommand.Run(ResultsFixturePath, baselinePath, "console", null, quiet: false, stdout, stderr);

            // "BuildDictionaries" is NOT a safe substring here — the
            // console table's 40-char name-column width legitimately
            // truncates the full canonical identity (type + method + job +
            // params) mid-word for this fixture's longer identities.
            // "meanNanoseconds" is short enough to never be truncated.
            stdout.ToString().Should().Contain("meanNanoseconds");
        }
        finally
        {
            File.Delete(baselinePath);
        }
    }

    [Fact]
    public void json_format_through_run_writes_the_output_file()
    {
        var observations = ParseFixtureObservations();
        var baselinePath = WriteBaseline(observations);
        var outputPath = TempPath("json");
        try
        {
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();

            var exitCode = CompareCommand.Run(
                ResultsFixturePath, baselinePath, "json", outputPath, quiet: true, stdout, stderr);

            exitCode.Should().Be(ExitCodes.Passed);
            File.ReadAllText(outputPath).Should().Contain("BuildDictionaries");
        }
        finally
        {
            File.Delete(baselinePath);
            File.Delete(outputPath);
        }
    }

    [Fact]
    public void markdown_format_through_run_writes_the_output_file()
    {
        var observations = ParseFixtureObservations();
        var baselinePath = WriteBaseline(observations);
        var outputPath = TempPath("md");
        try
        {
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();

            var exitCode = CompareCommand.Run(
                ResultsFixturePath, baselinePath, "markdown", outputPath, quiet: true, stdout, stderr);

            exitCode.Should().Be(ExitCodes.Passed);
            File.ReadAllText(outputPath).Should().Contain("BuildDictionaries");
        }
        finally
        {
            File.Delete(baselinePath);
            File.Delete(outputPath);
        }
    }

    // ============================================================
    // Added / Removed through Run() — real fixture, baseline deliberately
    // out of sync with it.
    // ============================================================

    [Fact]
    public void a_benchmark_absent_from_the_baseline_is_reported_as_added()
    {
        var observations = ParseFixtureObservations();
        // Drop the last observation from the baseline — it exists in
        // results but not baseline, so it becomes Added.
        var baselinePath = WriteBaseline(observations.Take(observations.Count - 1).ToList());
        var outputPath = TempPath("json");
        try
        {
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();

            var exitCode = CompareCommand.Run(
                ResultsFixturePath, baselinePath, "json", outputPath, quiet: true, stdout, stderr);

            exitCode.Should().Be(ExitCodes.Passed);
            File.ReadAllText(outputPath).Should().Contain("\"added\": 1");
        }
        finally
        {
            File.Delete(baselinePath);
            File.Delete(outputPath);
        }
    }

    [Fact]
    public void a_baseline_entry_absent_from_the_results_is_reported_as_removed()
    {
        var observations = ParseFixtureObservations();
        var baselinePath = WriteBaseline([.. observations, InventedObservation()]);
        var outputPath = TempPath("json");
        try
        {
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();

            var exitCode = CompareCommand.Run(
                ResultsFixturePath, baselinePath, "json", outputPath, quiet: true, stdout, stderr);

            exitCode.Should().Be(ExitCodes.Passed);
            File.ReadAllText(outputPath).Should().Contain("\"removed\": 1");
        }
        finally
        {
            File.Delete(baselinePath);
            File.Delete(outputPath);
        }
    }

    // ============================================================
    // WriteReport's format/output/quiet matrix — tested directly against
    // a hand-built ComparisonResult, since this exercises WriteReport's
    // own branching, not parsing/baseline behavior.
    // ============================================================

    private static ComparisonResult SampleComparison() => new(
        "nightly",
        [
            new BenchmarkComparison(
                new BenchmarkIdentity("Ns.Type", "Sort", "Ci"),
                BenchmarkComparisonStatus.Comparable,
                new BenchmarkStabilityMeasurement(20, 1.0),
                [
                    new MetricComparison(
                        "meanNanoseconds", MetricComparisonStatus.Comparable,
                        new MetricDescriptor("meanNanoseconds", OptimizationDirection.LowerIsBetter, "ns"),
                        Reference: new MetricValue(1000d, "ns"), Candidate: new MetricValue(1100d, "ns"),
                        AbsoluteDelta: 100d, PercentDelta: 10d, Direction: ChangeDirection.Degradation),
                ]),
        ]);

    [Fact]
    public void console_with_no_output_and_not_quiet_writes_stdout()
    {
        using var stdout = new StringWriter();

        CompareCommand.WriteReport(SampleComparison(), "console", null, quiet: false, stdout);

        stdout.ToString().Should().Contain("nightly");
    }

    [Fact]
    public void console_with_no_output_and_quiet_writes_nothing_to_stdout()
    {
        using var stdout = new StringWriter();

        CompareCommand.WriteReport(SampleComparison(), "console", null, quiet: true, stdout);

        stdout.ToString().Should().BeEmpty();
    }

    [Fact]
    public void console_with_output_writes_the_file_not_stdout_regardless_of_quiet()
    {
        using var stdout = new StringWriter();
        var path = TempPath("txt");
        try
        {
            CompareCommand.WriteReport(SampleComparison(), "console", path, quiet: false, stdout);

            stdout.ToString().Should().BeEmpty();
            File.ReadAllText(path).Should().Contain("nightly");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void console_with_output_and_quiet_still_writes_the_file()
    {
        using var stdout = new StringWriter();
        var path = TempPath("txt");
        try
        {
            CompareCommand.WriteReport(SampleComparison(), "console", path, quiet: true, stdout);

            File.ReadAllText(path).Should().Contain("nightly");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void json_format_writes_the_output_file()
    {
        using var stdout = new StringWriter();
        var path = TempPath("json");
        try
        {
            CompareCommand.WriteReport(SampleComparison(), "json", path, quiet: false, stdout);

            File.ReadAllText(path).Should().Contain("\"schemaVersion\"");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void markdown_format_writes_the_output_file()
    {
        using var stdout = new StringWriter();
        var path = TempPath("md");
        try
        {
            CompareCommand.WriteReport(SampleComparison(), "markdown", path, quiet: false, stdout);

            File.ReadAllText(path).Should().Contain("Benchmark Compare");
        }
        finally
        {
            File.Delete(path);
        }
    }
}
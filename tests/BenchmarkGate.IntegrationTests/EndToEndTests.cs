using FluentAssertions;
using Xunit;

namespace Bijecta.BenchmarkGate.IntegrationTests;

/// <summary>
/// True black-box end-to-end tests: every test here invokes the real,
/// packed, installed <c>benchmark-gate</c> executable as a subprocess via
/// <see cref="ToolInstallFixture"/> -- never an in-process
/// <c>Command.Run()</c> call. This is what proves the actual artifact
/// people install with <c>dotnet tool install</c> works, not just the code
/// that builds it.
/// </summary>
[Collection(ToolInstallCollection.Name)]
public class EndToEndTests
{
    private readonly ToolInstallFixture _tool;

    public EndToEndTests(ToolInstallFixture tool) => _tool = tool;

    private static string FixturePath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);

    private const string SamplePolicyJson = """
        {
          "schemaVersion": 1,
          "stability": { "minimumMeasurements": 1, "maximumCoefficientOfVariation": 1.0 },
          "metrics": {
            "meanNanoseconds": { "direction": "lower-is-better", "warningPercent": 50, "failurePercent": 90, "minimumAbsoluteChange": 0 }
          }
        }
        """;

    [Fact]
    public async Task version_flag_prints_a_version_and_exits_zero()
    {
        var result = await _tool.RunAsync("--version");

        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task help_flag_lists_every_known_command()
    {
        var result = await _tool.RunAsync("--help");

        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Contain("check");
        result.StandardOutput.Should().Contain("compare");
        result.StandardOutput.Should().Contain("capture");
        result.StandardOutput.Should().Contain("validate");
    }

    [Fact]
    public async Task capture_then_compare_against_the_same_results_exits_zero()
    {
        // Full real pipeline: real `capture` writes a real baseline file
        // to disk, then real `compare` reads it back -- two separate
        // subprocess invocations, no shared in-process state.
        var resultsPath = FixturePath("sample-results.json");
        var baselinePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}-baseline.json");

        try
        {
            var captureResult = await _tool.RunAsync(
                $"capture --results \"{resultsPath}\" --output \"{baselinePath}\" --suite e2e-suite");
            captureResult.ExitCode.Should().Be(0, captureResult.StandardError);
            File.Exists(baselinePath).Should().BeTrue();

            var compareResult = await _tool.RunAsync(
                $"compare --results \"{resultsPath}\" --baseline \"{baselinePath}\"");

            compareResult.ExitCode.Should().Be(0, compareResult.StandardError);
            compareResult.StandardOutput.Should().Contain("Comparable");
        }
        finally
        {
            File.Delete(baselinePath);
        }
    }

    [Fact]
    public async Task capture_then_check_with_a_permissive_policy_passes()
    {
        var resultsPath = FixturePath("sample-results.json");
        var baselinePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}-baseline.json");
        var policyPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}-policy.json");

        try
        {
            await File.WriteAllTextAsync(policyPath, SamplePolicyJson, TestContext.Current.CancellationToken);

            var captureResult = await _tool.RunAsync(
                $"capture --results \"{resultsPath}\" --output \"{baselinePath}\" --suite e2e-suite");
            captureResult.ExitCode.Should().Be(0, captureResult.StandardError);

            var checkResult = await _tool.RunAsync(
                $"check --results \"{resultsPath}\" --baseline \"{baselinePath}\" --policy \"{policyPath}\"");

            checkResult.ExitCode.Should().Be(0, checkResult.StandardError);
        }
        finally
        {
            File.Delete(baselinePath);
            File.Delete(policyPath);
        }
    }

    [Fact]
    public async Task compare_json_format_writes_a_real_file_with_full_precision()
    {
        var resultsPath = FixturePath("sample-results.json");
        var baselinePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}-baseline.json");
        var outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}-compare.json");

        try
        {
            var captureResult = await _tool.RunAsync(
                $"capture --results \"{resultsPath}\" --output \"{baselinePath}\" --suite e2e-suite");
            captureResult.ExitCode.Should().Be(0, captureResult.StandardError);

            var compareResult = await _tool.RunAsync(
                $"compare --results \"{resultsPath}\" --baseline \"{baselinePath}\" --format json --output \"{outputPath}\"");

            compareResult.ExitCode.Should().Be(0, compareResult.StandardError);
            File.Exists(outputPath).Should().BeTrue();
            (await File.ReadAllTextAsync(outputPath, TestContext.Current.CancellationToken)).Should().Contain("\"schemaVersion\"");
        }
        finally
        {
            File.Delete(baselinePath);
            File.Delete(outputPath);
        }
    }

    [Fact]
    public async Task validate_against_an_incomplete_policy_returns_validation_failed()
    {
        var policyPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}-bad-policy.json");
        await File.WriteAllTextAsync(policyPath, "{ \"schemaVersion\": 1 }", TestContext.Current.CancellationToken);

        try
        {
            var result = await _tool.RunAsync($"validate --policy \"{policyPath}\"");

            result.ExitCode.Should().Be(12); // ExitCodes.ValidationFailed
        }
        finally
        {
            File.Delete(policyPath);
        }
    }

    [Fact]
    public async Task check_against_a_nonexistent_results_path_returns_unsupported_schema()
    {
        var result = await _tool.RunAsync(
            $"check --results \"{Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}-missing.json")}\" " +
            $"--baseline \"{Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}-missing.json")}\" " +
            $"--policy \"{Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}-missing.json")}\"");

        result.ExitCode.Should().Be(8); // ExitCodes.UnsupportedSchema
    }
}
using Bijecta.BenchmarkGate.BenchmarkDotNet.Validation;
using FluentAssertions;
using Xunit;

namespace Bijecta.BenchmarkGate.BenchmarkDotNet.Tests.Validation;

public class BenchmarkDotNetInputValidatorTests : IDisposable
{
    private readonly string _tempDirectory =
        Path.Combine(Path.GetTempPath(), $"bdn-input-validator-tests-{Guid.NewGuid():N}");

    public BenchmarkDotNetInputValidatorTests() => Directory.CreateDirectory(_tempDirectory);

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, recursive: true);

        GC.SuppressFinalize(this);
    }

    private string WriteFile(string name, string content)
    {
        var path = Path.Combine(_tempDirectory, name);
        File.WriteAllText(path, content);
        return path;
    }

    private const string ValidFile = """
        { "Benchmarks": [
          { "Type": "Ns.Type", "Method": "M", "Statistics": { "Mean": 100.0 } }
        ] }
        """;

    [Fact]
    public void Nonexistent_file_reports_BGV390()
    {
        var path = Path.Combine(_tempDirectory, "missing.json");

        var results = BenchmarkDotNetInputValidator.ValidatePath(path);

        results.Should().ContainSingle();
        results[0].Validation.Diagnostics.Should().ContainSingle(d => d.Descriptor.Id == "BGV390");
    }

    [Fact]
    public void Malformed_json_reports_BGV392()
    {
        var path = WriteFile("bad.json", "{ not json");

        var results = BenchmarkDotNetInputValidator.ValidatePath(path);

        results[0].Validation.Diagnostics.Should().ContainSingle(d => d.Descriptor.Id == "BGV392");
    }

    [Fact]
    public void Json_null_reports_BGV393()
    {
        var path = WriteFile("null.json", "null");

        var results = BenchmarkDotNetInputValidator.ValidatePath(path);

        results[0].Validation.Diagnostics.Should().ContainSingle(d => d.Descriptor.Id == "BGV393");
    }

    [Fact]
    public void Nonexistent_directory_reports_BGV394()
    {
        var path = Path.Combine(_tempDirectory, "does-not-exist-dir");

        var results = BenchmarkDotNetInputValidator.ValidatePath(path);

        results.Should().ContainSingle();
        results[0].Validation.Diagnostics.Should().ContainSingle(d => d.Descriptor.Id == "BGV394");
    }

    [Fact]
    public void Valid_file_produces_no_diagnostics()
    {
        var path = WriteFile("valid.json", ValidFile);

        var results = BenchmarkDotNetInputValidator.ValidatePath(path);

        results.Should().ContainSingle();
        results[0].Validation.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Semantic_problem_in_single_file_reports_BGV3xx_not_BGV39x()
    {
        var path = WriteFile("empty.json", """{ "Benchmarks": [] }""");

        var results = BenchmarkDotNetInputValidator.ValidatePath(path);

        results[0].Validation.Diagnostics.Should().ContainSingle(d => d.Descriptor.Id == "BGV300");
    }

    [Fact]
    public void Directory_validates_every_file_independently()
    {
        WriteFile("a.json", ValidFile);
        WriteFile("b.json", """{ "Benchmarks": [] }""");

        var results = BenchmarkDotNetInputValidator.ValidatePath(_tempDirectory);

        results.Should().HaveCount(2);
        results.Single(r => r.SourceFile.EndsWith("a.json", StringComparison.Ordinal)).Validation.IsValid.Should().BeTrue();
        results.Single(r => r.SourceFile.EndsWith("b.json", StringComparison.Ordinal)).Validation.Diagnostics
            .Should().ContainSingle(d => d.Descriptor.Id == "BGV300");
    }

    [Fact]
    public void Directory_cross_file_duplicate_is_attached_to_the_later_file_only()
    {
        WriteFile("a.json", ValidFile);
        WriteFile("b.json", ValidFile);

        var results = BenchmarkDotNetInputValidator.ValidatePath(_tempDirectory);

        var fileA = results.Single(r => r.SourceFile.EndsWith("a.json", StringComparison.Ordinal));
        var fileB = results.Single(r => r.SourceFile.EndsWith("b.json", StringComparison.Ordinal));

        fileA.Validation.Diagnostics.Should().NotContain(d => d.Descriptor.Id == "BGV305");
        fileB.Validation.Diagnostics.Should().ContainSingle(d => d.Descriptor.Id == "BGV305");
    }

    [Fact]
    public void All_input_diagnostic_ids_are_unique_and_distinct_from_observation_diagnostics()
    {
        var inputIds = BenchmarkDotNetInputDiagnostics.All.Select(d => d.Id);
        var observationIds = ObservationValidatorDiagnostics.All.Select(d => d.Id);

        var combined = inputIds.Concat(observationIds).ToList();
        combined.Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [MemberData(nameof(AllInputDescriptors))]
    public void All_input_diagnostic_ids_match_the_BGV39x_convention(Core.Validation.DiagnosticDescriptor descriptor)
    {
        descriptor.Id.Should().MatchRegex("^BGV39\\d$");
    }

    public static IEnumerable<object[]> AllInputDescriptors() =>
        BenchmarkDotNetInputDiagnostics.All.Select(d => new object[] { d });
}
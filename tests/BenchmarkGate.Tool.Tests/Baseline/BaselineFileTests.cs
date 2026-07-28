using System.Text;
using Bijecta.BenchmarkGate.Core.Identity;
using Bijecta.BenchmarkGate.Core.Model;
using Bijecta.BenchmarkGate.Tool.Baseline;
using FluentAssertions;
using Xunit;

namespace Bijecta.BenchmarkGate.Tool.Tests.Baseline;

public sealed class BaselineFileTests : IDisposable
{
    private readonly string _tempDirectory =
        Path.Combine(Path.GetTempPath(), $"baseline-file-tests-{Guid.NewGuid():N}");

    public BaselineFileTests()
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

    private string WriteFile(string content)
    {
        var path = PathIn($"{Guid.NewGuid():N}.json");
        File.WriteAllText(path, content, Encoding.UTF8);
        return path;
    }

    private static BenchmarkIdentity Id(string method = "Method") =>
        new("Ns.Type", method, "Default");

    private const string ValidBaseline = """
        { "schemaVersion": 2, "suite": "suite",
          "benchmarks": [
            { "identity": { "typeName": "Ns.Type", "methodName": "Method", "job": "Default" },
              "metrics": { "meanNanoseconds": 1000.0, "allocatedBytesPerOperation": 512.0 } }
          ] }
        """;

    [Fact]
    public void Missing_file_throws()
    {
        var act = () => BaselineFile.Load(PathIn("does-not-exist.json"));
        act.Should().Throw<BaselineFileException>().WithMessage("*does not exist*");
    }

    [Fact]
    public void Malformed_json_throws()
    {
        var path = WriteFile("{ not valid json");
        var act = () => BaselineFile.Load(path);
        act.Should().Throw<BaselineFileException>().WithMessage("*not valid JSON*");
    }

    [Fact]
    public void Json_null_throws()
    {
        var path = WriteFile("null");
        var act = () => BaselineFile.Load(path);
        act.Should().Throw<BaselineFileException>().WithMessage("*deserialized to null*");
    }

    [Fact]
    public void Schema_version_1_is_rejected_with_a_re_capture_message()
    {
        var path = WriteFile("""
            { "schemaVersion": 1, "suite": "suite", "benchmarks": [] }
            """);

        var act = () => BaselineFile.Load(path);

        act.Should().Throw<BaselineFileException>()
            .WithMessage("*schemaVersion 1*")
            .WithMessage("*re-run 'capture'*");
    }

    [Fact]
    public void Unsupported_future_schema_version_throws_generic_message()
    {
        var path = WriteFile("""
            { "schemaVersion": 99, "suite": "suite", "benchmarks": [] }
            """);

        var act = () => BaselineFile.Load(path);

        act.Should().Throw<BaselineFileException>().WithMessage("*schemaVersion 99*");
    }

    [Fact]
    public void Missing_suite_throws()
    {
        var path = WriteFile("""
            { "schemaVersion": 2, "benchmarks": [] }
            """);

        var act = () => BaselineFile.Load(path);

        act.Should().Throw<BaselineFileException>().WithMessage("*missing 'suite'*");
    }

    [Fact]
    public void Entry_missing_identity_throws()
    {
        var path = WriteFile("""
            { "schemaVersion": 2, "suite": "suite",
              "benchmarks": [ { "metrics": { "meanNanoseconds": 1000.0 } } ] }
            """);

        var act = () => BaselineFile.Load(path);

        act.Should().Throw<BaselineFileException>().WithMessage("*missing 'identity'*");
    }

    [Fact]
    public void Entry_missing_type_name_throws()
    {
        var path = WriteFile("""
            { "schemaVersion": 2, "suite": "suite",
              "benchmarks": [ { "identity": { "methodName": "Method" }, "metrics": { "meanNanoseconds": 1000.0 } } ] }
            """);

        var act = () => BaselineFile.Load(path);

        act.Should().Throw<BaselineFileException>().WithMessage("*missing 'typeName'*");
    }

    [Fact]
    public void Entry_missing_method_name_throws()
    {
        var path = WriteFile("""
            { "schemaVersion": 2, "suite": "suite",
              "benchmarks": [ { "identity": { "typeName": "Ns.Type" }, "metrics": { "meanNanoseconds": 1000.0 } } ] }
            """);

        var act = () => BaselineFile.Load(path);

        act.Should().Throw<BaselineFileException>().WithMessage("*missing 'methodName'*");
    }

    [Fact]
    public void Entry_missing_metrics_throws()
    {
        var path = WriteFile("""
            { "schemaVersion": 2, "suite": "suite",
              "benchmarks": [ { "identity": { "typeName": "Ns.Type", "methodName": "Method" } } ] }
            """);

        var act = () => BaselineFile.Load(path);

        act.Should().Throw<BaselineFileException>().WithMessage("*missing 'metrics'*");
    }

    [Fact]
    public void Entry_with_empty_metrics_throws()
    {
        var path = WriteFile("""
            { "schemaVersion": 2, "suite": "suite",
              "benchmarks": [ { "identity": { "typeName": "Ns.Type", "methodName": "Method" }, "metrics": {} } ] }
            """);

        var act = () => BaselineFile.Load(path);

        act.Should().Throw<BaselineFileException>().WithMessage("*missing 'metrics'*");
    }

    [Fact]
    public void Entry_missing_job_defaults_to_default()
    {
        var path = WriteFile("""
            { "schemaVersion": 2, "suite": "suite",
              "benchmarks": [ { "identity": { "typeName": "Ns.Type", "methodName": "Method" },
                                 "metrics": { "meanNanoseconds": 1000.0 } } ] }
            """);

        var baseline = BaselineFile.Load(path);

        baseline.Benchmarks.Single().Identity.Job.Should().Be("Default");
    }

    [Fact]
    public void Valid_baseline_loads_with_multi_metric_entries()
    {
        var path = WriteFile(ValidBaseline);

        var baseline = BaselineFile.Load(path);

        baseline.Suite.Should().Be("suite");
        var entry = baseline.Benchmarks.Single();
        entry.Identity.TypeName.Should().Be("Ns.Type");
        entry.Metrics.Should().ContainKey("meanNanoseconds").WhoseValue.Should().Be(1000.0);
        entry.Metrics.Should().ContainKey("allocatedBytesPerOperation").WhoseValue.Should().Be(512.0);
    }

    [Fact]
    public void Duplicate_identity_across_entries_throws()
    {
        var path = WriteFile("""
            { "schemaVersion": 2, "suite": "suite",
              "benchmarks": [
                { "identity": { "typeName": "Ns.Type", "methodName": "Method" }, "metrics": { "meanNanoseconds": 1000.0 } },
                { "identity": { "typeName": "Ns.Type", "methodName": "Method" }, "metrics": { "meanNanoseconds": 2000.0 } }
              ] }
            """);

        // BenchmarkBaseline's own constructor throws on duplicates —
        // BaselineFile.Load propagates that InvalidOperationException
        // unwrapped, since it isn't itself a file-parsing failure.
        var act = () => BaselineFile.Load(path);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Duplicate benchmark identity*");
    }

    [Fact]
    public void WriteCandidate_then_Load_round_trips_metrics_and_identity()
    {
        var path = PathIn("candidate.json");
        var observations = new List<BenchmarkObservation>
        {
            new(Id(), new Dictionary<string, double>
                {
                    [BenchmarkObservation.MeanNanosecondsMetric] = 1000.0,
                    [BenchmarkObservation.AllocatedBytesMetric] = 512.0,
                },
                MeasurementCount: 10, StandardDeviationNanoseconds: 5.0),
        };

        BaselineFile.WriteCandidate(path, "suite", observations);
        var baseline = BaselineFile.Load(path);

        var entry = baseline.Benchmarks.Single();
        entry.Identity.Should().Be(Id());
        entry.Metrics[BenchmarkObservation.MeanNanosecondsMetric].Should().Be(1000.0);
        entry.Metrics[BenchmarkObservation.AllocatedBytesMetric].Should().Be(512.0);
    }

    [Fact]
    public void WriteCandidate_writes_schema_version_2()
    {
        var path = PathIn("candidate.json");
        var observations = new List<BenchmarkObservation>
        {
            new(Id(), new Dictionary<string, double> { [BenchmarkObservation.MeanNanosecondsMetric] = 1000.0 },
                MeasurementCount: 10, StandardDeviationNanoseconds: 5.0),
        };

        BaselineFile.WriteCandidate(path, "suite", observations);

        File.ReadAllText(path).Should().Contain("\"schemaVersion\": 2");
    }

    [Fact]
    public void WriteCandidate_orders_benchmarks_by_canonical_identity()
    {
        var path = PathIn("candidate.json");
        var observations = new List<BenchmarkObservation>
        {
            new(Id("Z"), new Dictionary<string, double> { [BenchmarkObservation.MeanNanosecondsMetric] = 1000.0 }, 10, 5.0),
            new(Id("A"), new Dictionary<string, double> { [BenchmarkObservation.MeanNanosecondsMetric] = 1000.0 }, 10, 5.0),
        };

        BaselineFile.WriteCandidate(path, "suite", observations);
        var baseline = BaselineFile.Load(path);

        baseline.Benchmarks.Select(b => b.Identity.MethodName).Should().Equal("A", "Z");
    }

    [Fact]
    public void WriteCandidate_omits_parameters_when_empty()
    {
        var path = PathIn("candidate.json");
        var observations = new List<BenchmarkObservation>
        {
            new(Id(), new Dictionary<string, double> { [BenchmarkObservation.MeanNanosecondsMetric] = 1000.0 }, 10, 5.0),
        };

        BaselineFile.WriteCandidate(path, "suite", observations);

        File.ReadAllText(path).Should().NotContain("\"parameters\"");
    }
}
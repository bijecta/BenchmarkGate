using Bijecta.BenchmarkGate.BenchmarkDotNet.Parsing;
using Bijecta.BenchmarkGate.Core.Model;
using FluentAssertions;
using Xunit;

namespace Bijecta.BenchmarkGate.BenchmarkDotNet.Tests.Parsing;

public class BenchmarkDotNetResultParserTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    [Fact]
    public void Parses_supported_full_json_report_into_normalized_observations()
    {
        var observations = BenchmarkDotNetResultParser.ParseFile(FixturePath("two-benchmarks.json"));

        observations.Should().HaveCount(2);

        var mismatchScan = observations.Single(o => o.Identity.MethodName == "MismatchScan");
        mismatchScan.Identity.TypeName.Should().Be("Recon.Benchmarks.ClassificationBenchmarks");
        mismatchScan.Metrics[BenchmarkObservation.MeanNanosecondsMetric].Should().Be(4_540_000.0);
        mismatchScan.Identity.Parameters.Should().ContainKey("N").WhoseValue.Should().Be("1000000");
    }

    [Fact]
    public void Extracts_allocation_metric_when_memory_block_is_present()
    {
        var observations = BenchmarkDotNetResultParser.ParseFile(FixturePath("two-benchmarks.json"));

        var indexBuild = observations.Single(o => o.Identity.MethodName == "IndexBuild");
        indexBuild.Metrics[BenchmarkObservation.AllocatedBytesMetric].Should().Be(1024);
    }

    [Fact]
    public void Extracts_measurement_count_and_standard_deviation_for_stability()
    {
        var observations = BenchmarkDotNetResultParser.ParseFile(FixturePath("two-benchmarks.json"));

        var mismatchScan = observations.Single(o => o.Identity.MethodName == "MismatchScan");
        mismatchScan.MeasurementCount.Should().Be(15);
        mismatchScan.StandardDeviationNanoseconds.Should().Be(85_000.0);
    }

    [Fact]
    public void Extracts_job_token_from_display_info_with_no_parentheses()
    {
        // "MismatchScan: DefaultJob [N=1000000]" — job token has no
        // parenthesized parameter list.
        var observations = BenchmarkDotNetResultParser.ParseFile(FixturePath("two-benchmarks.json"));

        var mismatchScan = observations.Single(o => o.Identity.MethodName == "MismatchScan");
        mismatchScan.Identity.Job.Should().Be("DefaultJob");
    }

    [Fact]
    public void Falls_back_to_default_job_when_display_info_is_absent()
    {
        // duplicate-identity.json has no DisplayInfo field at all.
        var act = () => BenchmarkDotNetResultParser.ParseFile(FixturePath("duplicate-identity.json"));

        // Both entries resolve to job "Default", which is exactly why they
        // collide (same Type/Method/Parameters/Job) — this is what makes
        // the duplicate-identity test fixture actually duplicate.
        act.Should().Throw<BenchmarkResultParseException>()
            .WithMessage("*Duplicate benchmark identity*");
    }

    [Fact]
    public void Missing_statistics_mean_throws_typed_exception()
    {
        var act = () => BenchmarkDotNetResultParser.ParseFile(FixturePath("missing-statistics.json"));

        act.Should().Throw<BenchmarkResultParseException>()
            .WithMessage("*Statistics.Mean*");
    }

    [Fact]
    public void Duplicate_identity_within_one_file_throws_typed_exception()
    {
        var act = () => BenchmarkDotNetResultParser.ParseFile(FixturePath("duplicate-identity.json"));

        act.Should().Throw<BenchmarkResultParseException>()
            .WithMessage("*Duplicate benchmark identity*");
    }

    [Fact]
    public void Nonexistent_file_throws_typed_exception()
    {
        var act = () => BenchmarkDotNetResultParser.ParseFile(FixturePath("does-not-exist.json"));

        act.Should().Throw<BenchmarkResultParseException>();
    }

    [Fact]
    public void ParsePath_on_a_directory_parses_all_json_files_recursively()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Fixtures");

        var act = () => BenchmarkDotNetResultParser.ParsePath(directory);

        act.Should().Throw<BenchmarkResultParseException>();
    }

    [Fact]
    public void Extracts_job_token_from_display_info_with_parenthesized_parameters()
    {
        // "Type.Method: Job-SNYTAA(IterationCount=10, ...) [N=1000000]" —
        // job token is followed by a parenthesized parameter list, unlike
        // the "DefaultJob" case which has no parens.
        var observations = BenchmarkDotNetResultParser.ParseFile(FixturePath("job-with-parentheses.json"));

        var observation = observations.Single();
        observation.Identity.Job.Should().Be("Job-SNYTAA");
    }

    [Fact]
    public void Multiple_problems_in_one_file_are_all_preserved_in_the_validation_result()
    {
        var act = () => BenchmarkDotNetResultParser.ParseFile(FixturePath("duplicate-identity.json"));

        var exception = act.Should().Throw<BenchmarkResultParseException>().Which;
        exception.ValidationResult.Should().NotBeNull();
        exception.ValidationResult!.Diagnostics.Should().ContainSingle(d => d.Descriptor.Id == "BGV304");
    }

    [Fact]
    public void File_access_failures_do_not_populate_validation_result()
    {
        var act = () => BenchmarkDotNetResultParser.ParseFile(FixturePath("does-not-exist.json"));

        act.Should().Throw<BenchmarkResultParseException>().Which.ValidationResult.Should().BeNull();
    }

    [Fact]
    public void Cross_file_duplicate_identity_throws_with_BGV305()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "CrossFileDuplicate");

        var act = () => BenchmarkDotNetResultParser.ParsePath(directory);

        var exception = act.Should().Throw<BenchmarkResultParseException>().Which;
        exception.ValidationResult.Should().NotBeNull();
        exception.ValidationResult!.Diagnostics.Should().ContainSingle(d => d.Descriptor.Id == "BGV305");
    }

    [Fact]
    public void Malformed_parameter_fragment_throws_with_BGV306()
    {
        var act = () => BenchmarkDotNetResultParser.ParseFile(FixturePath("malformed-parameter.json"));

        var exception = act.Should().Throw<BenchmarkResultParseException>().Which;
        exception.ValidationResult.Should().NotBeNull();
        exception.ValidationResult!.Diagnostics.Should().ContainSingle(d => d.Descriptor.Id == "BGV306");
    }

    [Fact]
    public void Malformed_fragment_that_drops_the_only_parameter_can_collide_with_a_parameterless_benchmark()
    {
        // parameter-collision.json: two entries, same Type/Method/DisplayInfo —
        // one has Parameters "N1000000" (malformed, parses to {}), the other
        // has no Parameters at all — both resolve to the same canonical
        // identity once the malformed fragment is dropped.
        var act = () => BenchmarkDotNetResultParser.ParseFile(FixturePath("parameter-collision.json"));

        var exception = act.Should().Throw<BenchmarkResultParseException>().Which;
        exception.ValidationResult.Should().NotBeNull();
        exception.ValidationResult!.Diagnostics.Should().Contain(d => d.Descriptor.Id == "BGV306");
        exception.ValidationResult!.Diagnostics.Should().Contain(d => d.Descriptor.Id == "BGV304");
    }

    [Fact]
    public void ParsePath_on_a_directory_with_no_json_files_throws_typed_exception()
    {
        var emptyDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "EmptyDirectory");
        Directory.CreateDirectory(emptyDirectory);

        var act = () => BenchmarkDotNetResultParser.ParsePath(emptyDirectory);

        act.Should().Throw<BenchmarkResultParseException>()
            .WithMessage("*No *.json result files found*");
    }

    [Fact]
    public void ParsePath_on_a_nonexistent_path_throws_typed_exception()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "does-not-exist-at-all");

        var act = () => BenchmarkDotNetResultParser.ParsePath(path);

        act.Should().Throw<BenchmarkResultParseException>()
            .WithMessage("*Results path does not exist*");
    }

    [Fact]
    public void Document_that_deserializes_to_null_throws_typed_exception()
    {
        // null-document.json contains the literal JSON token `null`.
        var act = () => BenchmarkDotNetResultParser.ParseFile(FixturePath("null-document.json"));

        act.Should().Throw<BenchmarkResultParseException>()
            .WithMessage("*deserialized to null*");
    }

    [Fact]
    public void ParsePath_on_a_directory_with_valid_files_returns_merged_observations()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ValidMultiFile");

        var observations = BenchmarkDotNetResultParser.ParsePath(directory);

        observations.Should().HaveCount(2);
        observations.Select(o => o.Identity.MethodName).Should().BeEquivalentTo(["MethodA", "MethodB"]);
    }
}
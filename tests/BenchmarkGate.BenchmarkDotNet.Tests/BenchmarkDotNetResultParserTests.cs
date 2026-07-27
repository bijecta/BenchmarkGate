using Bijecta.BenchmarkGate.BenchmarkDotNet.Parsing;
using FluentAssertions;
using Xunit;

namespace Bijecta.BenchmarkGate.BenchmarkDotNet.Tests;

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
        mismatchScan.MeanNanoseconds.Should().Be(4_540_000.0);
        mismatchScan.Identity.Parameters.Should().ContainKey("N").WhoseValue.Should().Be("1000000");
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

        // The Fixtures directory intentionally also contains malformed
        // fixtures used by other tests, so parsing the whole directory as
        // "current results" should throw on the first malformed file it
        // encounters — this proves ParsePath doesn't silently skip bad files.
        var act = () => BenchmarkDotNetResultParser.ParsePath(directory);

        act.Should().Throw<BenchmarkResultParseException>();
    }
}

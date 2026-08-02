using Bijecta.BenchmarkGate.BenchmarkDotNet.Parsing;
using Bijecta.BenchmarkGate.BenchmarkDotNet.Validation;
using FluentAssertions;
using Xunit;

namespace Bijecta.BenchmarkGate.BenchmarkDotNet.Tests.Validation;

public class ObservationSetValidatorTests
{
    private static BdnBenchmarkDto Benchmark(string method) => new()
    {
        Type = "Ns.Type",
        Method = method,
        Parameters = "N=1",
        Statistics = new BdnStatisticsDto { Mean = 100.0 },
    };

    [Fact]
    public void No_duplicates_across_distinct_files_produces_no_diagnostics()
    {
        var documents = new List<ParsedBenchmarkDotNetDocument>
        {
            new("a.json", new BdnReportRootDto { Benchmarks = [Benchmark("A")] }),
            new("b.json", new BdnReportRootDto { Benchmarks = [Benchmark("B")] }),
        };

        var result = ObservationSetValidator.Validate(documents);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Same_identity_across_two_files_reports_BGV305_once()
    {
        var documents = new List<ParsedBenchmarkDotNetDocument>
        {
            new("a.json", new BdnReportRootDto { Benchmarks = [Benchmark("Shared")] }),
            new("b.json", new BdnReportRootDto { Benchmarks = [Benchmark("Shared")] }),
        };

        var result = ObservationSetValidator.Validate(documents);

        result.Diagnostics.Should().ContainSingle(d => d.Descriptor.Id == "BGV305");
    }

    [Fact]
    public void Duplicate_message_names_both_source_files()
    {
        var documents = new List<ParsedBenchmarkDotNetDocument>
        {
            new("a.json", new BdnReportRootDto { Benchmarks = [Benchmark("Shared")] }),
            new("b.json", new BdnReportRootDto { Benchmarks = [Benchmark("Shared")] }),
        };

        var diagnostic = ObservationSetValidator.Validate(documents).Diagnostics.Single();

        diagnostic.Message.Should().Contain("a.json");
        diagnostic.Message.Should().Contain("b.json");
    }

    [Fact]
    public void Same_identity_across_three_files_reports_BGV305_twice_not_for_every_pair()
    {
        var documents = new List<ParsedBenchmarkDotNetDocument>
        {
            new("a.json", new BdnReportRootDto { Benchmarks = [Benchmark("Shared")] }),
            new("b.json", new BdnReportRootDto { Benchmarks = [Benchmark("Shared")] }),
            new("c.json", new BdnReportRootDto { Benchmarks = [Benchmark("Shared")] }),
        };

        var result = ObservationSetValidator.Validate(documents);

        // Reported once per file after the first occurrence — not once per
        // Cartesian pair (which would be 3 for three files).
        result.Diagnostics.Should().HaveCount(2);
    }

    [Fact]
    public void Entries_with_no_constructible_identity_are_excluded_from_cross_file_comparison()
    {
        var invalid = new BdnBenchmarkDto { Type = null, Method = "M", Statistics = new BdnStatisticsDto { Mean = 1 } };
        var documents = new List<ParsedBenchmarkDotNetDocument>
        {
            new("a.json", new BdnReportRootDto { Benchmarks = [invalid] }),
            new("b.json", new BdnReportRootDto { Benchmarks = [invalid] }),
        };

        var result = ObservationSetValidator.Validate(documents);

        result.IsValid.Should().BeTrue();
    }
}
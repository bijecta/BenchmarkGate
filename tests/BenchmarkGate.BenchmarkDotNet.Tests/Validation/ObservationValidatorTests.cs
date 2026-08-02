using Bijecta.BenchmarkGate.BenchmarkDotNet.Parsing;
using Bijecta.BenchmarkGate.BenchmarkDotNet.Validation;
using FluentAssertions;
using Xunit;

namespace Bijecta.BenchmarkGate.BenchmarkDotNet.Tests.Validation;

public class ObservationValidatorTests
{
    private static BdnBenchmarkDto ValidBenchmark(string method = "Method") => new()
    {
        Type = "Ns.Type",
        Method = method,
        Parameters = "N=1",
        Statistics = new BdnStatisticsDto { Mean = 100.0, N = 10, StandardDeviation = 1.0 },
    };

    private static BdnReportRootDto ValidDocument(List<BdnBenchmarkDto>? benchmarks = null) => new()
    {
        Title = "T",
        Benchmarks = benchmarks ?? [ValidBenchmark()],
    };

    [Fact]
    public void Fully_valid_document_produces_no_diagnostics()
    {
        var result = ObservationValidator.Validate(ValidDocument());

        result.IsValid.Should().BeTrue();
        result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Null_benchmarks_reports_BGV300()
    {
        var result = ObservationValidator.Validate(new BdnReportRootDto { Title = "T", Benchmarks = null });

        result.Diagnostics.Should().ContainSingle(d => d.Descriptor.Id == "BGV300");
    }

    [Fact]
    public void Empty_benchmarks_reports_BGV300()
    {
        var result = ObservationValidator.Validate(ValidDocument(benchmarks: []));

        result.Diagnostics.Should().ContainSingle(d => d.Descriptor.Id == "BGV300");
    }

    [Fact]
    public void Missing_type_reports_BGV301()
    {
        var benchmark = ValidBenchmark();
        benchmark.Type = null;

        var result = ObservationValidator.Validate(ValidDocument([benchmark]));

        result.Diagnostics.Should().ContainSingle(d => d.Descriptor.Id == "BGV301");
    }

    [Fact]
    public void Missing_method_reports_BGV302()
    {
        var benchmark = ValidBenchmark();
        benchmark.Method = null;

        var result = ObservationValidator.Validate(ValidDocument([benchmark]));

        result.Diagnostics.Should().ContainSingle(d => d.Descriptor.Id == "BGV302");
    }

    [Fact]
    public void Missing_statistics_block_reports_BGV303()
    {
        var benchmark = ValidBenchmark();
        benchmark.Statistics = null;

        var result = ObservationValidator.Validate(ValidDocument([benchmark]));

        result.Diagnostics.Should().ContainSingle(d => d.Descriptor.Id == "BGV303");
    }

    [Fact]
    public void Missing_mean_reports_BGV303()
    {
        var benchmark = ValidBenchmark();
        benchmark.Statistics = new BdnStatisticsDto { Mean = null };

        var result = ObservationValidator.Validate(ValidDocument([benchmark]));

        result.Diagnostics.Should().ContainSingle(d => d.Descriptor.Id == "BGV303");
    }

    [Fact]
    public void Duplicate_identity_within_file_reports_BGV304()
    {
        var result = ObservationValidator.Validate(ValidDocument([ValidBenchmark(), ValidBenchmark()]));

        result.Diagnostics.Should().ContainSingle(d => d.Descriptor.Id == "BGV304");
    }

    [Fact]
    public void Invalid_identities_do_not_produce_a_duplicate_diagnostic()
    {
        var benchmark = ValidBenchmark();
        benchmark.Type = null;

        var result = ObservationValidator.Validate(ValidDocument([benchmark, benchmark]));

        result.Diagnostics.Should().OnlyContain(d => d.Descriptor.Id == "BGV301");
        result.Diagnostics.Should().HaveCount(2);
    }

    [Fact]
    public void Multiple_unrelated_failures_are_all_reported_in_one_pass()
    {
        var missingType = ValidBenchmark();
        missingType.Type = null;
        var missingMean = ValidBenchmark("Other");
        missingMean.Statistics = null;

        var result = ObservationValidator.Validate(ValidDocument([missingType, missingMean]));

        result.Diagnostics.Select(d => d.Descriptor.Id).Should().BeEquivalentTo(["BGV301", "BGV303"]);
    }

    [Fact]
    public void Diagnostic_path_uses_benchmark_index()
    {
        var benchmark = ValidBenchmark();
        benchmark.Type = null;

        var result = ObservationValidator.Validate(ValidDocument([benchmark]));

        result.Diagnostics.Should().ContainSingle(d => d.Path == "/Benchmarks/0/Type");
    }

    [Fact]
    public void All_observation_diagnostic_ids_are_unique()
    {
        ObservationValidatorDiagnostics.All.Select(d => d.Id).Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [MemberData(nameof(AllObservationDescriptors))]
    public void All_observation_diagnostic_ids_match_the_BGV3_convention(Core.Validation.DiagnosticDescriptor descriptor)
    {
        descriptor.Id.Should().MatchRegex("^BGV3\\d{2}$");
    }

    public static IEnumerable<object[]> AllObservationDescriptors() =>
        ObservationValidatorDiagnostics.All.Select(d => new object[] { d });
}
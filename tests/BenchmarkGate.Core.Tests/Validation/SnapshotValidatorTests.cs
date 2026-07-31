using Bijecta.BenchmarkGate.Core.Baseline;
using Bijecta.BenchmarkGate.Core.Validation;
using FluentAssertions;
using Xunit;

namespace Bijecta.BenchmarkGate.Core.Tests.Validation;

public class SnapshotValidatorTests
{
    private static readonly BaselineIdentityDefinition ValidIdentity = new(
        TypeName: "Ns.Type", MethodName: "Method", Job: "Default", Parameters: null);

    private static readonly BaselineEntryDefinition ValidEntry = new(
        ValidIdentity, new Dictionary<string, double> { ["meanNanoseconds"] = 100 });

    private static BaselineDocument ValidDocument(
        int? schemaVersion = 2,
        string? suite = "MySuite",
        IReadOnlyList<BaselineEntryDefinition?>? benchmarks = null) =>
        new(schemaVersion, suite, benchmarks ?? [ValidEntry]);

    [Fact]
    public void Fully_valid_document_produces_no_diagnostics()
    {
        var result = SnapshotValidator.Validate(ValidDocument());

        result.IsValid.Should().BeTrue();
        result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Missing_schema_version_reports_BGV200()
    {
        var document = new BaselineDocument(SchemaVersion: null, Suite: "MySuite", Benchmarks: [ValidEntry]);

        var result = SnapshotValidator.Validate(document);

        result.Diagnostics.Should().ContainSingle(d => d.Descriptor.Id == "BGV200");
    }

    [Fact]
    public void Unsupported_schema_version_reports_BGV201()
    {
        var result = SnapshotValidator.Validate(ValidDocument(schemaVersion: 99));

        result.Diagnostics.Should().ContainSingle(d => d.Descriptor.Id == "BGV201");
        result.Diagnostics.Should().NotContain(d => d.Descriptor.Id == "BGV200");
    }

    [Fact]
    public void Schema_version_1_reports_BGV201_with_migration_guidance()
    {
        var result = SnapshotValidator.Validate(ValidDocument(schemaVersion: 1));

        var diagnostic = result.Diagnostics.Should().ContainSingle(d => d.Descriptor.Id == "BGV201").Which;
        diagnostic.Message.Should().Contain("capture");
        diagnostic.Message.Should().Contain("schemaVersion 1");
    }

    [Fact]
    public void Missing_suite_reports_BGV202()
    {
        var document = new BaselineDocument(SchemaVersion: 2, Suite: null, Benchmarks: [ValidEntry]);

        var result = SnapshotValidator.Validate(document);

        result.Diagnostics.Should().ContainSingle(d => d.Descriptor.Id == "BGV202");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_or_whitespace_suite_reports_BGV202(string suite)
    {
        var result = SnapshotValidator.Validate(ValidDocument(suite: suite));

        result.Diagnostics.Should().ContainSingle(d => d.Descriptor.Id == "BGV202");
    }

    [Fact]
    public void Duplicate_identity_reports_BGV203()
    {
        var document = ValidDocument(benchmarks: [ValidEntry, ValidEntry]);

        var result = SnapshotValidator.Validate(document);

        result.Diagnostics.Should().ContainSingle(d => d.Descriptor.Id == "BGV203");
    }

    [Fact]
    public void Missing_identity_reports_BGV204()
    {
        var entry = ValidEntry with { Identity = null };
        var result = SnapshotValidator.Validate(ValidDocument(benchmarks: [entry]));

        result.Diagnostics.Should().ContainSingle(d => d.Descriptor.Id == "BGV204");
    }

    [Fact]
    public void Missing_type_name_reports_BGV204()
    {
        var entry = ValidEntry with { Identity = ValidIdentity with { TypeName = null } };
        var result = SnapshotValidator.Validate(ValidDocument(benchmarks: [entry]));

        result.Diagnostics.Should().ContainSingle(d => d.Descriptor.Id == "BGV204");
    }

    [Fact]
    public void Missing_method_name_reports_BGV204()
    {
        var entry = ValidEntry with { Identity = ValidIdentity with { MethodName = null } };
        var result = SnapshotValidator.Validate(ValidDocument(benchmarks: [entry]));

        result.Diagnostics.Should().ContainSingle(d => d.Descriptor.Id == "BGV204");
    }

    [Fact]
    public void Missing_metrics_reports_BGV205()
    {
        var entry = ValidEntry with { Metrics = null };
        var result = SnapshotValidator.Validate(ValidDocument(benchmarks: [entry]));

        result.Diagnostics.Should().ContainSingle(d => d.Descriptor.Id == "BGV205");
    }

    [Fact]
    public void Empty_metrics_reports_BGV205()
    {
        var entry = ValidEntry with { Metrics = new Dictionary<string, double>() };
        var result = SnapshotValidator.Validate(ValidDocument(benchmarks: [entry]));

        result.Diagnostics.Should().ContainSingle(d => d.Descriptor.Id == "BGV205");
    }

    [Fact]
    public void Null_entry_reports_BGV204_and_BGV205()
    {
        var result = SnapshotValidator.Validate(ValidDocument(benchmarks: [null]));

        result.Diagnostics.Should().Contain(d => d.Descriptor.Id == "BGV204");
        result.Diagnostics.Should().Contain(d => d.Descriptor.Id == "BGV205");
        result.Diagnostics.Should().HaveCount(2);
    }

    [Fact]
    public void Invalid_identities_do_not_produce_a_duplicate_diagnostic()
    {
        // Two entries both missing an identity would, if compared naively,
        // look like duplicate empty identities. BGV203 must not fire on
        // top of two independent BGV204s.
        var entry = ValidEntry with { Identity = null };
        var document = ValidDocument(benchmarks: [entry, entry]);

        var result = SnapshotValidator.Validate(document);

        result.Diagnostics.Should().HaveCount(2);
        result.Diagnostics.Should().OnlyContain(d => d.Descriptor.Id == "BGV204");
        result.Diagnostics.Should().NotContain(d => d.Descriptor.Id == "BGV203");
    }

    [Fact]
    public void Missing_benchmarks_collection_is_treated_as_empty_and_reports_no_diagnostics()
    {
        var document = new BaselineDocument(SchemaVersion: 2, Suite: "MySuite", Benchmarks: null);

        var result = SnapshotValidator.Validate(document);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Multiple_unrelated_failures_are_all_reported_in_one_pass()
    {
        var entry = new BaselineEntryDefinition(
            new BaselineIdentityDefinition(TypeName: "", MethodName: "", Job: null, Parameters: null),
            new Dictionary<string, double>());

        var document = new BaselineDocument(SchemaVersion: 42, Suite: "", Benchmarks: [entry]);

        var result = SnapshotValidator.Validate(document);

        result.Diagnostics.Select(d => d.Descriptor.Id).Should().BeEquivalentTo(
            ["BGV201", "BGV202", "BGV204", "BGV205"]);
    }

    [Fact]
    public void Diagnostic_order_is_deterministic_across_repeated_runs()
    {
        var document = new BaselineDocument(SchemaVersion: null, Suite: null, Benchmarks: [null]);

        var first = SnapshotValidator.Validate(document).Diagnostics.Select(d => d.Descriptor.Id).ToList();
        var second = SnapshotValidator.Validate(document).Diagnostics.Select(d => d.Descriptor.Id).ToList();

        first.Should().Equal(second);
    }

    [Fact]
    public void All_snapshot_diagnostic_ids_are_unique()
    {
        SnapshotValidatorDiagnostics.All.Select(d => d.Id).Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [MemberData(nameof(AllSnapshotDescriptors))]
    public void All_snapshot_diagnostic_ids_match_the_BGV2_convention(DiagnosticDescriptor descriptor)
    {
        descriptor.Id.Should().MatchRegex("^BGV2\\d{2}$");
    }

    public static IEnumerable<object[]> AllSnapshotDescriptors() =>
        SnapshotValidatorDiagnostics.All.Select(d => new object[] { d });
}
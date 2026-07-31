using Bijecta.BenchmarkGate.Core.Baseline;
using FluentAssertions;
using Xunit;

namespace Bijecta.BenchmarkGate.Core.Tests.Baseline;

public class BaselineCompilerTests
{
    private static readonly BaselineIdentityDefinition ValidIdentity = new(
        TypeName: "Ns.Type", MethodName: "Method", Job: null, Parameters: null);

    private static readonly BaselineEntryDefinition ValidEntry = new(
        ValidIdentity, new Dictionary<string, double> { ["meanNanoseconds"] = 100 });

    private static BaselineDocument ValidDocument(
        IReadOnlyList<BaselineEntryDefinition?>? benchmarks = null) =>
        new(BaselineFormat.CurrentSchemaVersion, "MySuite", benchmarks ?? [ValidEntry]);

    [Fact]
    public void Valid_document_compiles_to_the_expected_baseline()
    {
        var baseline = BaselineCompiler.CompileValidated(ValidDocument());

        baseline.Suite.Should().Be("MySuite");
        baseline.Benchmarks.Should().HaveCount(1);
    }

    [Fact]
    public void Missing_job_defaults_to_Default()
    {
        var baseline = BaselineCompiler.CompileValidated(ValidDocument());

        baseline.Benchmarks[0].Identity.Job.Should().Be("Default");
    }

    [Fact]
    public void Missing_parameters_default_to_empty()
    {
        var baseline = BaselineCompiler.CompileValidated(ValidDocument());

        baseline.Benchmarks[0].Identity.Parameters.Should().BeEmpty();
    }

    [Fact]
    public void Missing_benchmarks_collection_compiles_to_an_empty_baseline()
    {
        var document = new BaselineDocument(BaselineFormat.CurrentSchemaVersion, "MySuite", Benchmarks: null);

        var baseline = BaselineCompiler.CompileValidated(document);

        baseline.Benchmarks.Should().BeEmpty();
    }

    [Fact]
    public void Null_document_throws_ArgumentNullException()
    {
        var act = () => BaselineCompiler.CompileValidated(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Unsupported_schema_version_throws_ArgumentException()
    {
        var document = ValidDocument() with { SchemaVersion = 1 };

        var act = () => BaselineCompiler.CompileValidated(document);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Missing_suite_throws_ArgumentException()
    {
        var document = ValidDocument() with { Suite = null };

        var act = () => BaselineCompiler.CompileValidated(document);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Null_entry_throws_ArgumentException_not_NullReferenceException()
    {
        var document = ValidDocument(benchmarks: [null]);

        var act = () => BaselineCompiler.CompileValidated(document);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Missing_identity_throws_ArgumentException()
    {
        var entry = ValidEntry with { Identity = null };
        var document = ValidDocument(benchmarks: [entry]);

        var act = () => BaselineCompiler.CompileValidated(document);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Missing_metrics_throws_ArgumentException()
    {
        var entry = ValidEntry with { Metrics = null };
        var document = ValidDocument(benchmarks: [entry]);

        var act = () => BaselineCompiler.CompileValidated(document);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Duplicate_identity_throws_ArgumentException_via_BenchmarkBaseline()
    {
        var document = ValidDocument(benchmarks: [ValidEntry, ValidEntry]);

        var act = () => BaselineCompiler.CompileValidated(document);

        act.Should().Throw<ArgumentException>();
    }
}
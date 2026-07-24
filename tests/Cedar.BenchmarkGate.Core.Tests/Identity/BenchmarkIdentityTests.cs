using Cedar.BenchmarkGate.Core.Identity;
using FluentAssertions;
using Xunit;

namespace Cedar.BenchmarkGate.Core.Tests.Identity;

public class BenchmarkIdentityTests
{
    [Fact]
    public void Parameter_insertion_order_does_not_change_identity()
    {
        var a = new BenchmarkIdentity("Ns.Type", "Method", "Default",
            new Dictionary<string, string> { ["N"] = "1000000", ["Distribution"] = "Canonical" });

        var b = new BenchmarkIdentity("Ns.Type", "Method", "Default",
            new Dictionary<string, string> { ["Distribution"] = "Canonical", ["N"] = "1000000" });

        a.Should().Be(b);
        a.CanonicalString.Should().Be(b.CanonicalString);
    }

    [Fact]
    public void Canonical_string_matches_documented_format()
    {
        var identity = new BenchmarkIdentity("Namespace.Type", "Method", "Ci",
            new Dictionary<string, string> { ["N"] = "1000000", ["Distribution"] = "Canonical" });

        identity.CanonicalString.Should().Be("Namespace.Type.Method|job=Ci|Distribution=Canonical|N=1000000");
    }

    [Fact]
    public void Different_parameter_values_produce_different_identities()
    {
        var a = new BenchmarkIdentity("Ns.Type", "Method", "Default",
            new Dictionary<string, string> { ["N"] = "1000000" });
        var b = new BenchmarkIdentity("Ns.Type", "Method", "Default",
            new Dictionary<string, string> { ["N"] = "2000000" });

        a.Should().NotBe(b);
    }

    [Fact]
    public void Missing_and_empty_parameter_values_remain_distinguishable()
    {
        var withEmpty = new BenchmarkIdentity("Ns.Type", "Method", "Default",
            new Dictionary<string, string> { ["Label"] = "" });
        var withoutParam = new BenchmarkIdentity("Ns.Type", "Method", "Default");

        withEmpty.Should().NotBe(withoutParam);
    }

    [Fact]
    public void Type_and_method_names_preserve_case()
    {
        var a = new BenchmarkIdentity("Ns.Type", "Method", "Default");
        var b = new BenchmarkIdentity("ns.type", "method", "Default");

        a.Should().NotBe(b);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_type_name_throws(string typeName)
    {
        var act = () => new BenchmarkIdentity(typeName, "Method", "Default");
        act.Should().Throw<ArgumentException>();
    }
}

using Bijecta.BenchmarkGate.Core.Identity;
using FluentAssertions;
using Xunit;

namespace Bijecta.BenchmarkGate.Core.Tests.Identity;

public class BenchmarkIdentityComparerTests
{
    private static BenchmarkIdentity Identity(
        string typeName, string methodName, string job = "Ci", IReadOnlyDictionary<string, string>? parameters = null) =>
        new(typeName, methodName, job, parameters);

    [Fact]
    public void compare_orders_by_type_name_first()
    {
        var alpha = Identity("AlphaBenchmarks", "Run");
        var zoo = Identity("ZooBenchmarks", "Run");

        BenchmarkIdentityComparer.Instance.Compare(alpha, zoo).Should().BeNegative();
        BenchmarkIdentityComparer.Instance.Compare(zoo, alpha).Should().BePositive();
    }

    [Fact]
    public void compare_orders_by_method_name_when_type_names_are_equal()
    {
        var read = Identity("MyBenchmarks", "Read");
        var write = Identity("MyBenchmarks", "Write");

        BenchmarkIdentityComparer.Instance.Compare(read, write).Should().BeNegative();
    }

    [Fact]
    public void compare_orders_by_job_when_type_and_method_names_are_equal()
    {
        var ci = Identity("MyBenchmarks", "Run", job: "Ci");
        var nightly = Identity("MyBenchmarks", "Run", job: "Nightly");

        BenchmarkIdentityComparer.Instance.Compare(ci, nightly).Should().BeNegative();
    }

    [Fact]
    public void compare_orders_by_parameters_when_type_method_and_job_are_equal()
    {
        var small = Identity("MyBenchmarks", "Run", parameters: new Dictionary<string, string> { ["N"] = "10" });
        var large = Identity("MyBenchmarks", "Run", parameters: new Dictionary<string, string> { ["N"] = "20" });

        BenchmarkIdentityComparer.Instance.Compare(small, large).Should().BeNegative();
    }

    [Fact]
    public void compare_orders_fewer_parameters_before_more_when_the_shared_prefix_is_equal()
    {
        var fewer = Identity("MyBenchmarks", "Run", parameters: new Dictionary<string, string> { ["N"] = "10" });
        var more = Identity("MyBenchmarks", "Run",
            parameters: new Dictionary<string, string> { ["N"] = "10", ["Z"] = "extra" });

        BenchmarkIdentityComparer.Instance.Compare(fewer, more).Should().BeNegative();
    }

    [Fact]
    public void compare_returns_zero_for_identities_with_equal_components()
    {
        var first = Identity("MyBenchmarks", "Run", parameters: new Dictionary<string, string> { ["N"] = "10" });
        var second = Identity("MyBenchmarks", "Run", parameters: new Dictionary<string, string> { ["N"] = "10" });

        BenchmarkIdentityComparer.Instance.Compare(first, second).Should().Be(0);
    }

    [Fact]
    public void sorting_a_shuffled_list_produces_a_stable_canonical_order()
    {
        var identities = new[]
        {
            Identity("ZooBenchmarks", "Run"),
            Identity("AlphaBenchmarks", "Write"),
            Identity("MiddleBenchmarks", "Read"),
        };

        Array.Sort(identities, BenchmarkIdentityComparer.Instance);

        identities.Select(i => i.TypeName).Should().Equal("AlphaBenchmarks", "MiddleBenchmarks", "ZooBenchmarks");
    }
}
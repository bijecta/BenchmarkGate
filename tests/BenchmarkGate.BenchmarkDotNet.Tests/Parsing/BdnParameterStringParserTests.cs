using Bijecta.BenchmarkGate.BenchmarkDotNet.Parsing;
using FluentAssertions;
using Xunit;

namespace Bijecta.BenchmarkGate.BenchmarkDotNet.Tests;

public class BdnParameterStringParserTests
{
    // --- Null / empty / whitespace input ---

    [Fact]
    public void Returns_empty_result_when_input_is_null()
    {
        var result = BdnParameterStringParser.Parse(null);

        result.Parameters.Should().BeEmpty();
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public void Returns_empty_result_when_input_is_an_empty_string()
    {
        var result = BdnParameterStringParser.Parse(string.Empty);

        result.Parameters.Should().BeEmpty();
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public void Returns_empty_result_when_input_is_whitespace_only()
    {
        var result = BdnParameterStringParser.Parse("   ");

        result.Parameters.Should().BeEmpty();
        result.Issues.Should().BeEmpty();
    }

    // --- Valid parsing (existing behavior, must remain unchanged) ---

    [Fact]
    public void Parses_single_valid_fragment_into_one_parameter()
    {
        var result = BdnParameterStringParser.Parse("N=1000000");

        result.Parameters.Should().ContainSingle()
            .Which.Should().Be(new KeyValuePair<string, string>("N", "1000000"));
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public void Parses_multiple_valid_fragments_into_all_parameters()
    {
        var result = BdnParameterStringParser.Parse("N=1000000,Distribution=Canonical");

        result.Parameters.Should().HaveCount(2);
        result.Parameters.Should().ContainKey("N").WhoseValue.Should().Be("1000000");
        result.Parameters.Should().ContainKey("Distribution").WhoseValue.Should().Be("Canonical");
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public void Trims_whitespace_around_keys_and_values()
    {
        var result = BdnParameterStringParser.Parse(" N = 1000000 , Distribution = Canonical ");

        result.Parameters.Should().ContainKey("N").WhoseValue.Should().Be("1000000");
        result.Parameters.Should().ContainKey("Distribution").WhoseValue.Should().Be("Canonical");
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public void Splits_on_first_equals_sign_when_value_itself_contains_equals()
    {
        var result = BdnParameterStringParser.Parse("Expr=a=b");

        result.Parameters.Should().ContainKey("Expr").WhoseValue.Should().Be("a=b");
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public void Last_value_wins_for_duplicate_keys()
    {
        var result = BdnParameterStringParser.Parse("N=1,N=2");

        result.Parameters.Should().ContainSingle();
        result.Parameters.Should().ContainKey("N").WhoseValue.Should().Be("2");
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public void Ignores_empty_fragment_after_a_trailing_comma()
    {
        var result = BdnParameterStringParser.Parse("N=1000000,");

        result.Parameters.Should().ContainSingle();
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public void Ignores_empty_fragments_between_consecutive_commas()
    {
        var result = BdnParameterStringParser.Parse("N=1000000,,Distribution=Canonical");

        result.Parameters.Should().HaveCount(2);
        result.Issues.Should().BeEmpty();
    }

    // --- MissingSeparator ---

    [Fact]
    public void Records_missing_separator_issue_for_fragment_without_an_equals_sign()
    {
        var result = BdnParameterStringParser.Parse("N1000000");

        result.Parameters.Should().BeEmpty();
        result.Issues.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            FragmentIndex = 0,
            Fragment = "N1000000",
            Kind = ParameterParseIssueKind.MissingSeparator,
        });
    }

    [Fact]
    public void Records_one_missing_separator_issue_per_fragment_when_several_lack_a_separator()
    {
        var result = BdnParameterStringParser.Parse("N1000000,Distribution");

        result.Parameters.Should().BeEmpty();
        result.Issues.Should().SatisfyRespectively(
            first =>
            {
                first.Kind.Should().Be(ParameterParseIssueKind.MissingSeparator);
                first.Fragment.Should().Be("N1000000");
                first.FragmentIndex.Should().Be(0);
            },
            second =>
            {
                second.Kind.Should().Be(ParameterParseIssueKind.MissingSeparator);
                second.Fragment.Should().Be("Distribution");
                second.FragmentIndex.Should().Be(1);
            });
    }

    // --- EmptyKey ---

    [Fact]
    public void Records_empty_key_issue_for_fragment_starting_with_equals()
    {
        var result = BdnParameterStringParser.Parse("=1000000");

        result.Parameters.Should().BeEmpty();
        result.Issues.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            FragmentIndex = 0,
            Fragment = "=1000000",
            Kind = ParameterParseIssueKind.EmptyKey,
        });
    }

    [Fact]
    public void Records_empty_key_issue_when_fragment_is_only_an_equals_sign()
    {
        // separatorIndex == 0 either way; the key being empty is what's
        // classified, not the (also-empty) value.
        var result = BdnParameterStringParser.Parse("=");

        result.Parameters.Should().BeEmpty();
        result.Issues.Should().ContainSingle()
            .Which.Kind.Should().Be(ParameterParseIssueKind.EmptyKey);
    }

    // --- Mixed valid + malformed ---

    [Fact]
    public void Preserves_both_valid_parameters_and_malformed_fragments_in_order()
    {
        var result = BdnParameterStringParser.Parse("N=1000000,Junk,Distribution=Canonical,=5");

        result.Parameters.Should().HaveCount(2);
        result.Parameters.Should().ContainKey("N").WhoseValue.Should().Be("1000000");
        result.Parameters.Should().ContainKey("Distribution").WhoseValue.Should().Be("Canonical");

        result.Issues.Should().SatisfyRespectively(
            first =>
            {
                first.Kind.Should().Be(ParameterParseIssueKind.MissingSeparator);
                first.Fragment.Should().Be("Junk");
                first.FragmentIndex.Should().Be(1); // 2nd non-empty fragment
            },
            second =>
            {
                second.Kind.Should().Be(ParameterParseIssueKind.EmptyKey);
                second.Fragment.Should().Be("=5");
                second.FragmentIndex.Should().Be(3); // 4th non-empty fragment
            });
    }

    [Fact]
    public void Returns_empty_parameters_when_every_fragment_is_malformed()
    {
        var result = BdnParameterStringParser.Parse("Junk,=5,MoreJunk");

        result.Parameters.Should().BeEmpty();
        result.Issues.Should().HaveCount(3);
    }
}
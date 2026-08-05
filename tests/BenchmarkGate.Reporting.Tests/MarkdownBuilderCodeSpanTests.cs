using Bijecta.BenchmarkGate.Reporting;
using FluentAssertions;
using Xunit;

namespace Bijecta.BenchmarkGate.Reporting.Tests;

public class MarkdownBuilderCodeSpanTests
{
    [Fact]
    public void content_with_no_backticks_uses_a_single_backtick_delimiter()
    {
        MarkdownBuilder.CodeSpan("plain-text").Should().Be("`plain-text`");
    }

    [Fact]
    public void content_with_one_backtick_uses_a_double_backtick_delimiter()
    {
        MarkdownBuilder.CodeSpan("List`1").Should().Be("``List`1``");
    }

    [Fact]
    public void content_with_a_run_of_two_backticks_uses_a_triple_backtick_delimiter()
    {
        MarkdownBuilder.CodeSpan("a``b").Should().Be("```a``b```");
    }

    [Fact]
    public void content_starting_with_a_backtick_gets_a_leading_and_trailing_space()
    {
        MarkdownBuilder.CodeSpan("`leading").Should().Be("`` `leading ``");
    }

    [Fact]
    public void content_ending_with_a_backtick_gets_a_leading_and_trailing_space()
    {
        MarkdownBuilder.CodeSpan("trailing`").Should().Be("`` trailing` ``");
    }

    [Fact]
    public void empty_content_uses_a_single_backtick_delimiter_with_no_padding()
    {
        MarkdownBuilder.CodeSpan(string.Empty).Should().Be("``");
    }

    [Fact]
    public void throws_when_text_is_null()
    {
        var act = () => MarkdownBuilder.CodeSpan(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
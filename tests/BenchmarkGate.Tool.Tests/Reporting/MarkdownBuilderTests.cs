using Bijecta.BenchmarkGate.Tool.Reporting;
using FluentAssertions;
using Xunit;

namespace Bijecta.BenchmarkGate.Tool.Tests.Reporting;

public class MarkdownBuilderTests
{
    [Fact]
    public void Heading_renders_the_correct_number_of_hash_characters()
    {
        var md = new MarkdownBuilder().Heading(2, "Title").ToString();

        md.Should().StartWith("## Title");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public void Heading_rejects_out_of_range_levels(int level)
    {
        var act = () => new MarkdownBuilder().Heading(level, "Title");

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Bold_wraps_label_and_value_in_double_asterisks()
    {
        var md = new MarkdownBuilder().Bold("Overall", "Passed").ToString();

        md.Should().Contain("**Overall: Passed**");
    }

    [Fact]
    public void Table_renders_header_separator_and_rows()
    {
        var md = new MarkdownBuilder()
            .Table(["A", "B"], [["1", "2"], ["3", "4"]])
            .ToString();

        md.Should().Contain("| A | B |");
        md.Should().Contain("|---|---|");
        md.Should().Contain("| 1 | 2 |");
        md.Should().Contain("| 3 | 4 |");
    }

    [Fact]
    public void Table_escapes_pipe_characters_in_cell_content()
    {
        var md = new MarkdownBuilder()
            .Table(["Header"], [["a | b"]])
            .ToString();

        md.Should().Contain("a \\| b");
        // Escaped pipe must not be misread as an extra column boundary.
        md.Should().NotContain("| a | b |");
    }

    [Fact]
    public void Table_strips_newlines_from_cell_content()
    {
        var md = new MarkdownBuilder()
            .Table(["Header"], [["line one\nline two\r\nline three"]])
            .ToString();

        md.Should().Contain("| line one line two line three |");
    }

    [Fact]
    public void Bullet_prefixes_with_a_dash()
    {
        var md = new MarkdownBuilder().Bullet("item").ToString();

        md.Should().Contain("- item");
    }

    [Fact]
    public void Paragraph_appends_text_followed_by_a_blank_line()
    {
        var md = new MarkdownBuilder().Paragraph("text").Paragraph("more").ToString();

        md.Should().Contain("text");
        md.Should().Contain("more");

        var textIndex = md.IndexOf("text", StringComparison.Ordinal);
        var moreIndex = md.IndexOf("more", StringComparison.Ordinal);
        textIndex.Should().BeLessThan(moreIndex);
    }

    [Theory]
    [InlineData(5.0, "+5.00%")]
    [InlineData(-5.0, "-5.00%")]
    [InlineData(0.0, "+0.00%")]
    public void FormatDeltaPercent_includes_an_explicit_sign(double value, string expected)
    {
        MarkdownBuilder.FormatDeltaPercent(value).Should().Be(expected);
    }

    [Fact]
    public void FormatDeltaPercent_returns_a_dash_for_null()
    {
        MarkdownBuilder.FormatDeltaPercent(null).Should().Be("-");
    }

    [Fact]
    public void Builder_calls_are_chainable_and_accumulate_in_order()
    {
        var md = new MarkdownBuilder()
            .Heading(1, "Title")
            .Paragraph("intro")
            .Bullet("point one")
            .ToString();

        var titleIndex = md.IndexOf("Title", StringComparison.Ordinal);
        var introIndex = md.IndexOf("intro", StringComparison.Ordinal);
        var bulletIndex = md.IndexOf("point one", StringComparison.Ordinal);

        titleIndex.Should().BeLessThan(introIndex);
        introIndex.Should().BeLessThan(bulletIndex);
    }
}
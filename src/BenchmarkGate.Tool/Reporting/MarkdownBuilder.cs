using System.Globalization;
using System.Text;

namespace Bijecta.BenchmarkGate.Tool.Reporting;

/// <summary>
/// A small, fluent Markdown document builder covering exactly what BenchmarkGate's
/// reports need: headings, bold text, tables, and bullet lists. Not a
/// general-purpose Markdown/CommonMark library — if report needs grow
/// significantly beyond this, reconsider pulling in a real one (e.g.
/// ap0llo/markdown-generator) instead of growing this file indefinitely.
/// </summary>
public sealed class MarkdownBuilder
{
    private readonly StringBuilder _sb = new();

    public MarkdownBuilder Heading(int level, string text)
    {
        if (level is < 1 or > 6) throw new ArgumentOutOfRangeException(nameof(level));
        _sb.Append('#', level).Append(' ').AppendLine(text);
        _sb.AppendLine();
        return this;
    }

    public MarkdownBuilder Bold(string label, string value)
    {
        _sb.Append("**").Append(label).Append(": ").Append(value).AppendLine("**");
        _sb.AppendLine();
        return this;
    }

    public MarkdownBuilder Paragraph(string text)
    {
        _sb.AppendLine(text);
        _sb.AppendLine();
        return this;
    }

    /// <summary>
    /// Writes a GitHub-flavored Markdown table. Cell content is escaped
    /// (pipes and newlines) so a benchmark identity or explanation string
    /// containing '|' can never silently corrupt the table structure.
    /// </summary>
    public MarkdownBuilder Table(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows)
    {
        _sb.Append("| ").AppendJoin(" | ", headers.Select(Escape)).AppendLine(" |");
        _sb.Append('|').AppendJoin('|', headers.Select(_ => "---")).AppendLine("|");

        foreach (var row in rows)
        {
            _sb.Append("| ").AppendJoin(" | ", row.Select(Escape)).AppendLine(" |");
        }

        _sb.AppendLine();
        return this;
    }

    public MarkdownBuilder Bullet(string text)
    {
        _sb.Append("- ").AppendLine(text);
        return this;
    }

    public MarkdownBuilder BlankLine()
    {
        _sb.AppendLine();
        return this;
    }

    public override string ToString() => _sb.ToString();

    private static string Escape(string value) =>
        value.Replace("|", "\\|", StringComparison.Ordinal)
             .Replace("\n", " ", StringComparison.Ordinal)
             .Replace("\r", "", StringComparison.Ordinal);

    /// <summary>
    /// Formats nanoseconds as ms/µs/ns using invariant culture — shared by
    /// every reporter so unit thresholds stay consistent in one place.
    /// </summary>
    public static string FormatNanoseconds(double? nanoseconds)
    {
        if (nanoseconds is not { } value) return "-";
        return value >= 1_000_000
            ? string.Create(CultureInfo.InvariantCulture, $"{value / 1_000_000:F3} ms")
            : value >= 1_000
                ? string.Create(CultureInfo.InvariantCulture, $"{value / 1_000:F3} \u00b5s")
                : string.Create(CultureInfo.InvariantCulture, $"{value:F3} ns");
    }

    public static string FormatDeltaPercent(double? deltaPercent) =>
        deltaPercent is { } delta
            ? string.Create(CultureInfo.InvariantCulture, $"{(delta >= 0 ? "+" : "")}{delta:F2}%")
            : "-";
}

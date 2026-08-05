using System.Globalization;
using System.Text;

namespace Bijecta.BenchmarkGate.Reporting;

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
    /// Formats a relative percentage delta with an explicit sign, invariant
    /// culture. Not metric-specific (unlike raw values), so it stays here
    /// rather than moving to Core.Evaluation.MetricFormatters.
    /// </summary>
    public static string FormatDeltaPercent(double? deltaPercent) =>
        deltaPercent is { } delta
            ? string.Create(CultureInfo.InvariantCulture, $"{(delta >= 0 ? "+" : "")}{delta:F2}%")
            : "-";

    /// <summary>
    /// Wraps <paramref name="text"/> in a CommonMark-safe inline code span:
    /// the backtick delimiter is chosen one character longer than the
    /// longest run of consecutive backticks in <paramref name="text"/>, so
    /// content containing backticks can never prematurely close the span.
    /// (.NET reflection names generic types with a literal backtick — e.g.
    /// <c>List`1</c> — so this is a real case for benchmark type names, not
    /// a hypothetical one.) Per CommonMark, a single leading or trailing
    /// space is added when the content itself starts or ends with a
    /// backtick, so the delimiter and the content's own backtick don't
    /// visually run together.
    /// </summary>
    public static string CodeSpan(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var longestBacktickRun = 0;
        var currentRun = 0;
        foreach (var ch in text)
        {
            if (ch == '`')
            {
                currentRun++;
                longestBacktickRun = Math.Max(longestBacktickRun, currentRun);
            }
            else
            {
                currentRun = 0;
            }
        }

        var delimiter = new string('`', longestBacktickRun + 1);
        var needsPadding = text.StartsWith('`') || text.EndsWith('`');

        return needsPadding ? $"{delimiter} {text} {delimiter}" : $"{delimiter}{text}{delimiter}";
    }
}
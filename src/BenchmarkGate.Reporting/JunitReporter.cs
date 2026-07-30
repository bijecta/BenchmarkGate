using System.Xml;
using System.Xml.Linq;
using Bijecta.BenchmarkGate.Storage.FileSystem;
using Bijecta.BenchmarkGate.Core.Evaluation;
namespace Bijecta.BenchmarkGate.Reporting;

/// <summary>
/// Writes a standard JUnit XML report, consumed natively by GitHub Actions,
/// GitLab, and most CI test-result UIs. One &lt;testcase&gt; per
/// (benchmark, metric) pair, so a CI UI surfaces exactly which metric
/// failed rather than only "this benchmark failed" — consistent with the
/// per-metric row approach in ConsoleReporter/MarkdownReporter.
/// </summary>
public static class JunitReporter
{
    /// <remarks>
    /// Benchmarks with no metric decisions (New/Missing/Unstable) still get
    /// exactly one &lt;testcase&gt; each, named after the benchmark identity
    /// alone, so they aren't silently dropped from the report. Regressed,
    /// Missing, and Unstable always render as &lt;failure&gt;. Warning only
    /// renders as &lt;failure&gt; when <paramref name="failOnWarning"/> is true,
    /// so the JUnit report's pass/fail signal matches the process exit code
    /// (SuiteDecision.GetExitCode) instead of contradicting it.
    /// </remarks>
    public static void Write(string path, SuiteDecision decision, string suite, bool failOnWarning)
    {
        var testCases = decision.Benchmarks
            .OrderBy(b => b.Identity.CanonicalString, StringComparer.Ordinal)
            .SelectMany(b => BuildTestCases(b, failOnWarning))
            .ToList();

        var failureCount = testCases.Count(tc => tc.Failure is not null);

        var testsuite = new XElement("testsuite",
            new XAttribute("name", suite),
            new XAttribute("tests", testCases.Count),
            new XAttribute("failures", failureCount),
            new XAttribute("errors", 0),
            testCases.Select(tc => tc.Element));

        var document = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            testsuite);

        WriteDocument(path, document);
    }

    private static IEnumerable<(XElement Element, XElement? Failure)> BuildTestCases(
        BenchmarkDecision benchmark, bool failOnWarning)
    {
        if (benchmark.Metrics.Count == 0)
        {
            yield return BuildTestCase(
                name: benchmark.Identity.CanonicalString,
                status: benchmark.Status,
                explanation: benchmark.Explanation,
                failOnWarning);
            yield break;
        }

        foreach (var metric in benchmark.Metrics)
        {
            yield return BuildTestCase(
                name: $"{benchmark.Identity.CanonicalString} [{metric.MetricName}]",
                status: metric.Status,
                explanation: metric.Explanation,
                failOnWarning);
        }
    }

    private static (XElement Element, XElement? Failure) BuildTestCase(
        string name,
        BenchmarkGateStatus status,
        string explanation,
        bool failOnWarning)
    {
        var isFailure = status switch
        {
            BenchmarkGateStatus.Regressed or BenchmarkGateStatus.Missing or BenchmarkGateStatus.Unstable => true,
            BenchmarkGateStatus.Warning => failOnWarning,
            _ => false,
        };

        var testcase = new XElement("testcase",
            new XAttribute("name", name),
            new XAttribute("classname", name));

        XElement? failure = null;
        if (isFailure)
        {
            failure = new XElement("failure",
                new XAttribute("message", explanation),
                new XAttribute("type", status.ToString()));
            testcase.Add(failure);
        }

        return (testcase, failure);
    }

    private static void WriteDocument(string path, XDocument document)
    {
        var settings = new XmlWriterSettings
        {
            Indent = true,
            Encoding = System.Text.Encoding.UTF8,
        };

        using var stringWriter = new Utf8StringWriter();
        using (var xmlWriter = XmlWriter.Create(stringWriter, settings))
        {
            document.WriteTo(xmlWriter);
        }

        try
        {
            AtomicFileWriter.Write(path, stringWriter.ToString());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ReportWriteException(path, "Failed to write JUnit XML report.", ex);
        }
    }

    /// <summary>
    /// StringWriter defaults to UTF-16 in its Encoding property. This
    /// override keeps the XML declaration's stated encoding truthful,
    /// since the real bytes written to disk (via AtomicFileWriter) are
    /// UTF-8 without BOM.
    /// </summary>
    private sealed class Utf8StringWriter : StringWriter
    {
        public override System.Text.Encoding Encoding => System.Text.Encoding.UTF8;
    }
}
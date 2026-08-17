using System.Text.Json;
using Bijecta.BenchmarkGate.BenchmarkDotNet.Validation;
using Bijecta.BenchmarkGate.Core.Model;

namespace Bijecta.BenchmarkGate.BenchmarkDotNet.Parsing;

public static class BenchmarkDotNetResultParser
{
    public static IReadOnlyList<BenchmarkObservation> ParseFile(string path)
    {
        var (document, _) = DeserializeAndValidate(path);
        return CompileObservations(document);
    }

    public static IReadOnlyList<BenchmarkObservation> ParsePath(string path)
    {
        if (File.Exists(path))
            return ParseFile(path);

        if (!Directory.Exists(path))
            throw new BenchmarkResultParseException(path, "Results path does not exist.");

        var files = Directory.GetFiles(path, "*.json", SearchOption.AllDirectories);
        if (files.Length == 0)
            throw new BenchmarkResultParseException(path, "No *.json result files found under directory.");

        var parsedDocuments = new List<ParsedBenchmarkDotNetDocument>();
        foreach (var file in files.OrderBy(f => f, StringComparer.Ordinal))
        {
            var (document, _) = DeserializeAndValidate(file);
            parsedDocuments.Add(new ParsedBenchmarkDotNetDocument(file, document));
        }

        var setValidation = ObservationSetValidator.Validate(parsedDocuments);
        if (!setValidation.IsValid)
            throw BenchmarkResultParseException.FromValidationResult(path, setValidation);

        return parsedDocuments.SelectMany(p => CompileObservations(p.Document)).ToList();
    }

    private static (BdnReportRootDto Document, string SourceFile) DeserializeAndValidate(string path)
    {
        if (!File.Exists(path))
            throw new BenchmarkResultParseException(path, "Result file does not exist.");

        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (IOException ex)
        {
            throw new BenchmarkResultParseException(path, "Could not read result file.", ex);
        }

        BdnReportRootDto? root;
        try
        {
            root = JsonSerializer.Deserialize<BdnReportRootDto>(json);
        }
        catch (JsonException ex)
        {
            throw new BenchmarkResultParseException(path, "Result file has invalid JSON syntax or structure.", ex);
        }

        if (root is null)
            throw new BenchmarkResultParseException(path, "Result file deserialized to null.");

        var validation = ObservationValidator.Validate(root);
        if (!validation.IsValid)
            throw BenchmarkResultParseException.FromValidationResult(path, validation);

        return (root, path);
    }

    private static List<BenchmarkObservation> CompileObservations(BdnReportRootDto document) =>
        (document.Benchmarks ?? []).Select(CompileObservation).ToList();

    private static BenchmarkObservation CompileObservation(BdnBenchmarkDto benchmark)
    {
        // Trusts ObservationValidator has already confirmed Type, Method,
        // Statistics.Mean, and parameter-fragment well-formedness (BGV306)
        // — this method does not re-check them, matching
        // PolicyCompiler/BaselineCompiler's CompileValidated pattern (one
        // implementation of each rule, not two).
        var identity = IdentityFactory.Create(benchmark).Identity
            ?? throw new InvalidOperationException(
                "CompileObservation called with an entry that did not pass ObservationValidator.");

        var mean = benchmark.Statistics!.Mean!.Value;
        var metrics = new Dictionary<string, double> { [BenchmarkObservation.MeanNanosecondsMetric] = mean };

        if (benchmark.Memory?.BytesAllocatedPerOperation is { } bytesAllocated)
        {
            metrics[BenchmarkObservation.AllocatedBytesMetric] = bytesAllocated;
        }

        var measurementCount = benchmark.Statistics?.N ?? 0;
        var standardDeviation = benchmark.Statistics?.StandardDeviation ?? 0;

        return new BenchmarkObservation(identity, metrics, measurementCount, standardDeviation);
    }
}
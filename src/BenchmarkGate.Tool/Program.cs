// System.CommandLine 2.0 GA API (per ADR-0002). Note this is meaningfully
// different from the older beta4 API seen in some tutorials: SetAction
// (not SetHandler), ParseResult passed directly (no InvocationContext),
// no CommandLineBuilder/middleware.

using System.CommandLine;
using Bijecta.BenchmarkGate.Core.Evaluation;
using Bijecta.BenchmarkGate.Tool.Commands;

var resultsOption = new Option<string>("--results") { Required = true };
var baselineOption = new Option<string>("--baseline") { Required = true };
var thresholdOption = new Option<double>("--threshold-percent") { DefaultValueFactory = _ => 10.0 };
var minAbsChangeOption = new Option<double>("--minimum-absolute-change-ns") { DefaultValueFactory = _ => 0.0 };
var markdownOption = new Option<string?>("--markdown");
var jsonOption = new Option<string?>("--json");
var quietOption = new Option<bool>("--quiet");

var checkCommand = new Command("check", "Evaluate BenchmarkDotNet results against an approved baseline.")
{
    resultsOption, baselineOption, thresholdOption, minAbsChangeOption, markdownOption, jsonOption, quietOption,
};

checkCommand.SetAction(parseResult => CheckCommand.Run(
    parseResult.GetValue(resultsOption)!,
    parseResult.GetValue(baselineOption)!,
    parseResult.GetValue(thresholdOption),
    parseResult.GetValue(minAbsChangeOption),
    parseResult.GetValue(markdownOption),
    parseResult.GetValue(jsonOption),
    parseResult.GetValue(quietOption),
    Console.Out,
    Console.Error));

var captureResultsOption = new Option<string>("--results") { Required = true };
var outputOption = new Option<string>("--output") { Required = true };
var suiteOption = new Option<string>("--suite") { DefaultValueFactory = _ => "default" };
var overwriteOption = new Option<bool>("--overwrite");

var captureCommand = new Command("capture", "Write a baseline candidate from BenchmarkDotNet results.")
{
    captureResultsOption, outputOption, suiteOption, overwriteOption,
};

captureCommand.SetAction(parseResult => CaptureCommand.Run(
    parseResult.GetValue(captureResultsOption)!,
    parseResult.GetValue(outputOption)!,
    parseResult.GetValue(suiteOption)!,
    parseResult.GetValue(overwriteOption),
    Console.Out,
    Console.Error));

var rootCommand = new RootCommand("BenchmarkGate — a local-first performance regression gate for BenchmarkDotNet.")
{
    checkCommand,
    captureCommand,
};

try
{
    return rootCommand.Parse(args).Invoke();
}
catch (Exception ex)
{
    // Boundary catch-all per master spec section 9: expected failures are
    // handled as typed exceptions inside each command (mapped to specific
    // exit codes there); anything reaching here is unexpected.
    Console.Error.WriteLine($"Unexpected internal error: {ex.Message}");
    Console.Error.WriteLine(ex.StackTrace);
    return ExitCodes.InternalError;
}

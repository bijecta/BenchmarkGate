// System.CommandLine 2.0 GA API (per ADR-0002). Note this is meaningfully
// different from the older beta4 API seen in some tutorials: SetAction
// (not SetHandler), ParseResult passed directly (no InvocationContext),
// no CommandLineBuilder/middleware.

using System.CommandLine;
using Bijecta.BenchmarkGate.Core.Evaluation;
using Bijecta.BenchmarkGate.Tool.Commands;

var resultsOption = new Option<string>("--results") { Required = true };
var baselineOption = new Option<string>("--baseline") { Required = true };
var policyOption = new Option<string>("--policy") { Required = true };
var markdownOption = new Option<string?>("--markdown");
var jsonOption = new Option<string?>("--json");
var junitOption = new Option<string?>("--junit");
var failOnWarningOption = new Option<bool>("--fail-on-warning");
var quietOption = new Option<bool>("--quiet");

var checkCommand = new Command("check", "Evaluate BenchmarkDotNet results against an approved baseline.")
{
    resultsOption, baselineOption, policyOption, markdownOption, jsonOption, junitOption, failOnWarningOption, quietOption,
};

checkCommand.SetAction(parseResult => CheckCommand.Run(
    parseResult.GetValue(resultsOption)!,
    parseResult.GetValue(baselineOption)!,
    parseResult.GetValue(policyOption)!,
    parseResult.GetValue(markdownOption),
    parseResult.GetValue(jsonOption),
    parseResult.GetValue(junitOption),
    parseResult.GetValue(failOnWarningOption),
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

var validatePolicyOption = new Option<string?>("--policy");
var validateBaselineOption = new Option<string?>("--baseline");
var validateResultsOption = new Option<string?>("--results");
var validateJsonOption = new Option<string?>("--json");
var validateQuietOption = new Option<bool>("--quiet");

var validateCommand = new Command("validate", "Validate a policy, baseline, or observations file without evaluating it.")
{
    validatePolicyOption, validateBaselineOption, validateResultsOption, validateJsonOption, validateQuietOption,
};

validateCommand.SetAction(parseResult => ValidateCommand.Run(
    parseResult.GetValue(validatePolicyOption),
    parseResult.GetValue(validateBaselineOption),
    parseResult.GetValue(validateResultsOption),
    parseResult.GetValue(validateJsonOption),
    parseResult.GetValue(validateQuietOption),
    Console.Out,
    Console.Error));

var compareResultsOption = new Option<string>("--results") { Required = true };
var compareBaselineOption = new Option<string>("--baseline") { Required = true };
var compareFormatOption = new Option<string>("--format") { DefaultValueFactory = _ => "console" };
var compareOutputOption = new Option<string?>("--output");
var compareQuietOption = new Option<bool>("--quiet");

var compareCommand = new Command("compare", "Compare BenchmarkDotNet results against a baseline. Policy-free — no pass/fail verdict.")
{
    compareResultsOption, compareBaselineOption, compareFormatOption, compareOutputOption, compareQuietOption,
};

compareCommand.SetAction(parseResult => CompareCommand.Run(
    parseResult.GetValue(compareResultsOption)!,
    parseResult.GetValue(compareBaselineOption)!,
    parseResult.GetValue(compareFormatOption)!,
    parseResult.GetValue(compareOutputOption),
    parseResult.GetValue(compareQuietOption),
    Console.Out,
    Console.Error));


var rootCommand = new RootCommand("BenchmarkGate — a local-first performance regression gate for BenchmarkDotNet.")
{
    checkCommand,
    compareCommand,
    captureCommand,
    validateCommand,
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
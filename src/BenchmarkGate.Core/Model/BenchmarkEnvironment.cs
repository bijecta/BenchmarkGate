namespace Bijecta.BenchmarkGate.Core.Model;

/// <summary>
/// Benchmark execution-environment metadata captured for a single BenchmarkDotNet report
/// (ADR-0006 Decision 2). Every property is independently nullable: a property is
/// <see langword="null"/> when the source document did not provide that specific field, distinct
/// from the whole environment being absent (see <see cref="BenchmarkRun.Environment"/>, which is
/// <see langword="null"/> when no environment document was supplied at all). This type owns no
/// parsing, validation, or comparison behavior — those are added by later issues in the v0.5.0
/// chain.
/// </summary>
/// <param name="BenchmarkDotNetCaption">The BenchmarkDotNet product caption reported in the source document (e.g. "BenchmarkDotNet").</param>
/// <param name="BenchmarkDotNetVersion">The BenchmarkDotNet version string that produced the report.</param>
/// <param name="OsVersion">The operating system version string reported for the run.</param>
/// <param name="ProcessorName">The processor name reported for the run.</param>
/// <param name="PhysicalProcessorCount">The number of physical processors reported for the run.</param>
/// <param name="PhysicalCoreCount">The number of physical cores reported for the run.</param>
/// <param name="LogicalCoreCount">The number of logical cores reported for the run.</param>
/// <param name="RuntimeVersion">The .NET runtime version string reported for the run.</param>
/// <param name="Architecture">The processor architecture reported for the run, normalized into a Core-owned value by the BenchmarkDotNet adapter.</param>
/// <param name="HasAttachedDebugger">Whether a debugger was attached to the process during the run, if reported.</param>
/// <param name="HasRyuJit">Whether the RyuJIT compiler was in use during the run, if reported.</param>
/// <param name="Configuration">The build configuration reported for the run (e.g. "RELEASE").</param>
/// <param name="DotNetCliVersion">The .NET CLI (SDK) version string reported for the run.</param>
/// <param name="ChronometerFrequencyHertz">The chronometer frequency, in hertz, reported for the run.</param>
/// <param name="HardwareTimerKind">The hardware timer substrate reported for the run, normalized into a Core-owned value by the BenchmarkDotNet adapter.</param>
public sealed record BenchmarkEnvironment(
    string? BenchmarkDotNetCaption,
    string? BenchmarkDotNetVersion,
    string? OsVersion,
    string? ProcessorName,
    int? PhysicalProcessorCount,
    int? PhysicalCoreCount,
    int? LogicalCoreCount,
    string? RuntimeVersion,
    BenchmarkArchitecture? Architecture,
    bool? HasAttachedDebugger,
    bool? HasRyuJit,
    string? Configuration,
    string? DotNetCliVersion,
    long? ChronometerFrequencyHertz,
    BenchmarkHardwareTimerKind? HardwareTimerKind);
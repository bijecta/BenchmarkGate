namespace Bijecta.BenchmarkGate.Core.Model;

/// <summary>
/// One member per captured field of <see cref="BenchmarkEnvironment"/> (ADR-0006 Decision 2),
/// used to identify a specific dimension of environment metadata independent of its runtime
/// value — e.g. when reporting which dimension caused a coherence or compatibility outcome.
/// </summary>
public enum EnvironmentDimension
{
    /// <summary>Corresponds to <see cref="BenchmarkEnvironment.BenchmarkDotNetCaption"/>.</summary>
    BenchmarkDotNetCaption,

    /// <summary>Corresponds to <see cref="BenchmarkEnvironment.BenchmarkDotNetVersion"/>.</summary>
    BenchmarkDotNetVersion,

    /// <summary>Corresponds to <see cref="BenchmarkEnvironment.OsVersion"/>.</summary>
    OsVersion,

    /// <summary>Corresponds to <see cref="BenchmarkEnvironment.ProcessorName"/>.</summary>
    ProcessorName,

    /// <summary>Corresponds to <see cref="BenchmarkEnvironment.PhysicalProcessorCount"/>.</summary>
    PhysicalProcessorCount,

    /// <summary>Corresponds to <see cref="BenchmarkEnvironment.PhysicalCoreCount"/>.</summary>
    PhysicalCoreCount,

    /// <summary>Corresponds to <see cref="BenchmarkEnvironment.LogicalCoreCount"/>.</summary>
    LogicalCoreCount,

    /// <summary>Corresponds to <see cref="BenchmarkEnvironment.RuntimeVersion"/>.</summary>
    RuntimeVersion,

    /// <summary>Corresponds to <see cref="BenchmarkEnvironment.Architecture"/>.</summary>
    Architecture,

    /// <summary>Corresponds to <see cref="BenchmarkEnvironment.HasAttachedDebugger"/>.</summary>
    HasAttachedDebugger,

    /// <summary>Corresponds to <see cref="BenchmarkEnvironment.HasRyuJit"/>.</summary>
    HasRyuJit,

    /// <summary>Corresponds to <see cref="BenchmarkEnvironment.Configuration"/>.</summary>
    Configuration,

    /// <summary>Corresponds to <see cref="BenchmarkEnvironment.DotNetCliVersion"/>.</summary>
    DotNetCliVersion,

    /// <summary>Corresponds to <see cref="BenchmarkEnvironment.ChronometerFrequencyHertz"/>.</summary>
    ChronometerFrequencyHertz,

    /// <summary>Corresponds to <see cref="BenchmarkEnvironment.HardwareTimerKind"/>.</summary>
    HardwareTimerKind
}
namespace Bijecta.BenchmarkGate.Core.Model;

/// <summary>
/// Processor architecture reported for a benchmark's execution environment, normalized from
/// BenchmarkDotNet's own architecture vocabulary (BenchmarkDotNet.Environments.Platform) into a
/// Core-owned type so persisted history is never coupled to a BenchmarkDotNet dependency.
/// Mapping into this enum is the BenchmarkDotNet adapter's responsibility, not Core's.
/// </summary>
public enum BenchmarkArchitecture
{
    /// <summary>Platform-agnostic build target ("AnyCPU"). Retained for source-vocabulary fidelity; not expected on a running process's reported architecture.</summary>
    AnyCpu,

    /// <summary>32-bit x86.</summary>
    X86,

    /// <summary>64-bit x86 (x64/AMD64).</summary>
    X64,

    /// <summary>32-bit ARM.</summary>
    Arm,

    /// <summary>64-bit ARM.</summary>
    Arm64,

    /// <summary>WebAssembly.</summary>
    Wasm,

    /// <summary>IBM Z (s390x).</summary>
    S390x,

    /// <summary>LoongArch, 64-bit.</summary>
    LoongArch64,

    /// <summary>32-bit ARMv6.</summary>
    Armv6,

    /// <summary>PowerPC 64-bit, little-endian.</summary>
    Ppc64le,

    /// <summary>RISC-V, 64-bit.</summary>
    RiscV64,

    /// <summary>
    /// A recognized-but-unmapped architecture value. Reserved for forward compatibility:
    /// persisted history can outlive the BenchmarkGate version that wrote it, so a future
    /// BenchmarkDotNet architecture this version's adapter doesn't recognize maps here instead
    /// of failing deserialization or being coerced into an unrelated known value.
    /// </summary>
    Unknown
}
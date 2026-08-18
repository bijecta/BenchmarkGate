namespace Bijecta.BenchmarkGate.Core.Model;

/// <summary>
/// Hardware timer substrate used by BenchmarkDotNet's chronometer, normalized from
/// BenchmarkDotNet's own vocabulary (Perfolizer.Horology.HardwareTimerKind) into a Core-owned
/// type. <see cref="Unknown"/> is a real BenchmarkDotNet-emitted sentinel (confirmed from a live
/// 0.15.8 fixture, not a placeholder) representing "recognized but semantically unavailable
/// evidence" — an ordinary known value for environment coherence checks, but never a
/// compatibility Match. What <see cref="Unknown"/> means for compatibility comparison is out of
/// scope here; that's added by the comparison logic in a later v0.5.0 issue.
/// </summary>
public enum BenchmarkHardwareTimerKind
{
    /// <summary>Operating system timer.</summary>
    System,

    /// <summary>Time Stamp Counter.</summary>
    Tsc,

    /// <summary>ACPI power management timer.</summary>
    Acpi,

    /// <summary>High Precision Event Timer.</summary>
    Hpet,

    /// <summary>
    /// BenchmarkDotNet's own producer-defined sentinel for a chronometer frequency it could not
    /// attribute to a specific known timer substrate. Recognized and preserved as a known value,
    /// not a parse failure.
    /// </summary>
    Unknown
}
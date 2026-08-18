using Bijecta.BenchmarkGate.Core.Model;
using FluentAssertions;
using Xunit;

namespace Bijecta.BenchmarkGate.Core.Tests.Model;

public sealed class BenchmarkEnvironmentTests
{
    [Fact]
    public void RecordEquality_AllFieldsEqual_AreEqual()
    {
        var left = CreateFullEnvironment();
        var right = CreateFullEnvironment();

        left.Should().Be(right);
    }

    [Fact]
    public void RecordEquality_OneFieldDiffers_AreNotEqual()
    {
        var left = CreateFullEnvironment();
        var right = CreateFullEnvironment() with { ProcessorName = "Different Processor" };

        left.Should().NotBe(right);
    }

    [Fact]
    public void AllProperties_CanBeNull()
    {
        var environment = new BenchmarkEnvironment(
            BenchmarkDotNetCaption: null,
            BenchmarkDotNetVersion: null,
            OsVersion: null,
            ProcessorName: null,
            PhysicalProcessorCount: null,
            PhysicalCoreCount: null,
            LogicalCoreCount: null,
            RuntimeVersion: null,
            Architecture: null,
            HasAttachedDebugger: null,
            HasRyuJit: null,
            Configuration: null,
            DotNetCliVersion: null,
            ChronometerFrequencyHertz: null,
            HardwareTimerKind: null);

        environment.BenchmarkDotNetCaption.Should().BeNull();
        environment.Architecture.Should().BeNull();
        environment.HardwareTimerKind.Should().BeNull();
        environment.ChronometerFrequencyHertz.Should().BeNull();
    }

    [Fact]
    public void With_ProducesNewInstance_LeavesOriginalUnchanged()
    {
        var original = CreateFullEnvironment();

        var modified = original with { HardwareTimerKind = BenchmarkHardwareTimerKind.Tsc };

        original.HardwareTimerKind.Should().Be(BenchmarkHardwareTimerKind.Unknown);
        modified.HardwareTimerKind.Should().Be(BenchmarkHardwareTimerKind.Tsc);
    }

    private static BenchmarkEnvironment CreateFullEnvironment() =>
        new(
            BenchmarkDotNetCaption: "BenchmarkDotNet",
            BenchmarkDotNetVersion: "0.15.8",
            OsVersion: "Windows 11 (10.0.26100.8875/24H2/2024Update/HudsonValley)",
            ProcessorName: "Intel Core Ultra 7 155H",
            PhysicalProcessorCount: 1,
            PhysicalCoreCount: 16,
            LogicalCoreCount: 22,
            RuntimeVersion: ".NET 10.0.10 (10.0.10, 10.0.1026.32716)",
            Architecture: BenchmarkArchitecture.X64,
            HasAttachedDebugger: false,
            HasRyuJit: true,
            Configuration: "RELEASE",
            DotNetCliVersion: "10.0.302",
            ChronometerFrequencyHertz: 10_000_000,
            HardwareTimerKind: BenchmarkHardwareTimerKind.Unknown);
}
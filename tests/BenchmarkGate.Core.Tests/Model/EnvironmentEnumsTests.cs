using System;
using Bijecta.BenchmarkGate.Core.Model;
using FluentAssertions;
using Xunit;

namespace Bijecta.BenchmarkGate.Core.Tests.Model;

public sealed class EnvironmentEnumsTests
{
    [Fact]
    public void BenchmarkHardwareTimerKind_HasExpectedMembers()
    {
        var members = Enum.GetNames<BenchmarkHardwareTimerKind>();

        members.Should().Equal("System", "Tsc", "Acpi", "Hpet", "Unknown");
    }

    [Fact]
    public void BenchmarkArchitecture_HasExpectedMembers()
    {
        var members = Enum.GetNames<BenchmarkArchitecture>();

        members.Should().Equal(
            "AnyCpu", "X86", "X64", "Arm", "Arm64", "Wasm",
            "S390x", "LoongArch64", "Armv6", "Ppc64le", "RiscV64", "Unknown");
    }

    [Fact]
    public void EnvironmentCompatibilityRole_HasExactlyThreeMembers()
    {
        var members = Enum.GetNames<EnvironmentCompatibilityRole>();

        members.Should().Equal("Filter", "Advisory", "None");
    }

    [Fact]
    public void EnvironmentDimension_HasOneMemberPerBenchmarkEnvironmentProperty()
    {
        var dimensionCount = Enum.GetValues<EnvironmentDimension>().Length;
        var propertyCount = typeof(BenchmarkEnvironment).GetProperties().Length;

        dimensionCount.Should().Be(15);
        dimensionCount.Should().Be(propertyCount);
    }
}
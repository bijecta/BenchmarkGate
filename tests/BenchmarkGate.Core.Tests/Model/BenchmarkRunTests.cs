using System.Collections.Generic;
using Bijecta.BenchmarkGate.Core.Model;
using FluentAssertions;
using Xunit;

namespace Bijecta.BenchmarkGate.Core.Tests.Model;

public sealed class BenchmarkRunTests
{
    [Fact]
    public void Environment_Null_MeansNoEnvironmentDocumentSupplied()
    {
        var run = new BenchmarkRun(Environment: null, Observations: new List<BenchmarkObservation>());

        run.Environment.Should().BeNull();
    }

    [Fact]
    public void Environment_PresentButAllFieldsNull_IsDistinctFromNoEnvironment()
    {
        var partialEnvironment = new BenchmarkEnvironment(
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

        var runWithoutEnvironment = new BenchmarkRun(Environment: null, Observations: new List<BenchmarkObservation>());
        var runWithPartialEnvironment = new BenchmarkRun(Environment: partialEnvironment, Observations: new List<BenchmarkObservation>());

        runWithoutEnvironment.Environment.Should().BeNull();
        runWithPartialEnvironment.Environment.Should().NotBeNull();
        runWithoutEnvironment.Should().NotBe(runWithPartialEnvironment);
    }

    [Fact]
    public void Observations_PreservesReadOnlyListReference()
    {
        IReadOnlyList<BenchmarkObservation> observations = new List<BenchmarkObservation>();

        var run = new BenchmarkRun(Environment: null, Observations: observations);

        run.Observations.Should().BeSameAs(observations);
    }
}
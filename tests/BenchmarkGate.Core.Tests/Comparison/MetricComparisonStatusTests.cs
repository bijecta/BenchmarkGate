using Bijecta.BenchmarkGate.Core.Comparison;
using FluentAssertions;
using Xunit;

namespace Bijecta.BenchmarkGate.Core.Tests.Comparison;

public class MetricComparisonStatusTests
{
    [Fact]
    public void defines_exactly_the_six_documented_status_values()
    {
        Enum.GetNames<MetricComparisonStatus>().Should().BeEquivalentTo(
        [
            nameof(MetricComparisonStatus.Comparable),
            nameof(MetricComparisonStatus.MissingReferenceMetric),
            nameof(MetricComparisonStatus.MissingCandidateMetric),
            nameof(MetricComparisonStatus.UnitMismatch),
            nameof(MetricComparisonStatus.InvalidReferenceValue),
            nameof(MetricComparisonStatus.InvalidCandidateValue),
        ]);
    }

    [Fact]
    public void unit_mismatch_remains_a_defined_status_despite_being_reserved_and_unproducible_today()
    {
        // BenchmarkComparisonEngine never returns this value — see its
        // deferral note — but the status stays part of the schema so a
        // future engine change (once source-unit metadata exists) doesn't
        // need a breaking enum change.
        Enum.IsDefined(MetricComparisonStatus.UnitMismatch).Should().BeTrue();
    }
}
using Bijecta.BenchmarkGate.Core.Comparison;
using FluentAssertions;
using Xunit;

namespace Bijecta.BenchmarkGate.Core.Tests.Comparison;

public class PercentDeltaCalculatorTests
{
    [Fact]
    public void calculate_when_reference_and_candidate_are_both_zero_returns_reference_zero_and_candidate_zero()
    {
        var result = PercentDeltaCalculator.Calculate(0d, 0d);

        result.Status.Should().Be(PercentDeltaStatus.ReferenceZeroAndCandidateZero);
        result.Value.Should().BeNull();
    }

    [Theory]
    [InlineData(0d, 5d)]
    [InlineData(0d, -5d)]
    public void calculate_when_reference_is_zero_and_candidate_is_nonzero_returns_reference_zero(
        double reference, double candidate)
    {
        var result = PercentDeltaCalculator.Calculate(reference, candidate);

        result.Status.Should().Be(PercentDeltaStatus.ReferenceZero);
        result.Value.Should().BeNull();
    }

    [Fact]
    public void calculate_when_reference_is_nan_returns_invalid_reference()
    {
        var result = PercentDeltaCalculator.Calculate(double.NaN, 10d);

        result.Status.Should().Be(PercentDeltaStatus.InvalidReference);
        result.Value.Should().BeNull();
    }

    [Fact]
    public void calculate_when_reference_is_positive_infinity_returns_invalid_reference()
    {
        var result = PercentDeltaCalculator.Calculate(double.PositiveInfinity, 10d);

        result.Status.Should().Be(PercentDeltaStatus.InvalidReference);
        result.Value.Should().BeNull();
    }

    [Fact]
    public void calculate_when_reference_is_negative_infinity_returns_invalid_reference()
    {
        var result = PercentDeltaCalculator.Calculate(double.NegativeInfinity, 10d);

        result.Status.Should().Be(PercentDeltaStatus.InvalidReference);
        result.Value.Should().BeNull();
    }

    [Fact]
    public void calculate_when_candidate_is_nan_and_reference_is_valid_returns_invalid_candidate()
    {
        var result = PercentDeltaCalculator.Calculate(10d, double.NaN);

        result.Status.Should().Be(PercentDeltaStatus.InvalidCandidate);
        result.Value.Should().BeNull();
    }

    [Fact]
    public void calculate_when_candidate_is_infinity_and_reference_is_valid_returns_invalid_candidate()
    {
        var result = PercentDeltaCalculator.Calculate(10d, double.PositiveInfinity);

        result.Status.Should().Be(PercentDeltaStatus.InvalidCandidate);
        result.Value.Should().BeNull();
    }

    [Fact]
    public void calculate_when_reference_is_zero_and_candidate_is_invalid_returns_invalid_candidate()
    {
        // Validity is checked before zero handling: a zero reference does
        // not short-circuit an invalid candidate into ReferenceZero.
        var result = PercentDeltaCalculator.Calculate(0d, double.NaN);

        result.Status.Should().Be(PercentDeltaStatus.InvalidCandidate);
        result.Value.Should().BeNull();
    }

    [Fact]
    public void calculate_when_reference_and_candidate_are_both_invalid_returns_invalid_reference()
    {
        // Reference-side invalidity wins on a tie: deterministic,
        // left-to-right, baseline integrity established before candidate.
        var result = PercentDeltaCalculator.Calculate(double.NaN, double.PositiveInfinity);

        result.Status.Should().Be(PercentDeltaStatus.InvalidReference);
        result.Value.Should().BeNull();
    }

    [Fact]
    public void calculate_when_candidate_is_greater_than_reference_returns_positive_percent()
    {
        var result = PercentDeltaCalculator.Calculate(100d, 150d);

        result.Status.Should().Be(PercentDeltaStatus.Calculated);
        result.Value.Should().Be(50d);
    }

    [Fact]
    public void calculate_when_candidate_is_less_than_reference_returns_negative_percent()
    {
        var result = PercentDeltaCalculator.Calculate(100d, 75d);

        result.Status.Should().Be(PercentDeltaStatus.Calculated);
        result.Value.Should().Be(-25d);
    }

    [Fact]
    public void calculate_when_candidate_equals_reference_and_both_are_nonzero_returns_zero_percent()
    {
        var result = PercentDeltaCalculator.Calculate(42d, 42d);

        result.Status.Should().Be(PercentDeltaStatus.Calculated);
        result.Value.Should().Be(0d);
    }

    [Fact]
    public void calculate_when_reference_is_negative_and_candidate_moves_toward_zero_returns_correct_percent()
    {
        var result = PercentDeltaCalculator.Calculate(-100d, -50d);

        result.Status.Should().Be(PercentDeltaStatus.Calculated);
        result.Value.Should().Be(-50d);
    }
}
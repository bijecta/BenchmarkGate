using Bijecta.BenchmarkGate.Core.Comparison;
using FluentAssertions;
using Xunit;

namespace Bijecta.BenchmarkGate.Core.Tests.Comparison;

/// <summary>
/// Enforces #30's "no pass/fail/stability concept anywhere in this model"
/// rule as a compile-independent, review-independent check: SuiteDecision's
/// vocabulary (Passed/Warning/Regressed/Unstable) must never appear as a
/// type name or enum member name anywhere in Core.Comparison.
/// </summary>
public class ComparisonModelVocabularySurfaceTests
{
    private static readonly string[] ForbiddenTerms = ["Passed", "Warning", "Regressed", "Unstable"];

    private static IEnumerable<Type> ComparisonModelTypes =>
        typeof(ComparisonResult).Assembly
            .GetTypes()
            .Where(t => t.IsPublic && t.Namespace == typeof(ComparisonResult).Namespace);

    public static IEnumerable<object[]> AllComparisonModelTypeCases() =>
        ComparisonModelTypes.Select(t => new object[] { t });

    public static IEnumerable<object[]> ComparisonModelEnumTypeCases() =>
        ComparisonModelTypes.Where(t => t.IsEnum).Select(t => new object[] { t });

    [Theory]
    [MemberData(nameof(AllComparisonModelTypeCases))]
    public void comparison_model_type_name_does_not_contain_suite_decision_vocabulary(Type type)
    {
        foreach (var term in ForbiddenTerms)
        {
            type.Name.Should().NotContain(term,
                $"{type.Name} is part of the policy-free comparison model and must not reuse SuiteDecision's pass/fail vocabulary");
        }
    }

    [Theory]
    [MemberData(nameof(ComparisonModelEnumTypeCases))]
    public void comparison_model_enum_members_do_not_contain_suite_decision_vocabulary(Type enumType)
    {
        foreach (var memberName in Enum.GetNames(enumType))
        {
            foreach (var term in ForbiddenTerms)
            {
                memberName.Should().NotContain(term,
                    $"{enumType.Name}.{memberName} is part of the policy-free comparison model and must not reuse SuiteDecision's pass/fail vocabulary");
            }
        }
    }

    [Fact]
    public void comparison_namespace_contains_at_least_the_expected_enum_types()
    {
        // Guards against the theory cases above silently covering zero
        // types if the namespace filter or assembly reference ever breaks.
        var enumNames = ComparisonModelTypes.Where(t => t.IsEnum).Select(t => t.Name).ToList();

        enumNames.Should().Contain(
        [
            nameof(BenchmarkComparisonStatus),
            nameof(MetricComparisonStatus),
            nameof(OptimizationDirection),
            nameof(ChangeDirection),
            nameof(PercentDeltaStatus)
        ]);
    }
}
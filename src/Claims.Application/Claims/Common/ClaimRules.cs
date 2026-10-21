using FluentValidation;

namespace Claims.Application.Claims.Common;

/// <summary>Shared request validation for claim actions (400 before the domain is reached).</summary>
public static class ClaimRules
{
    public const int TextMaxLength = 2000;
    public const int ReasonMaxLength = 1000;

    /// <summary>Greater than zero, at most 2 decimal places (all six currencies use 2 minor units).</summary>
    public static IRuleBuilderOptions<T, decimal> ValidAmount<T>(this IRuleBuilder<T, decimal> rule) =>
        rule.GreaterThan(0).PrecisionScale(18, 2, ignoreTrailingZeros: true);

    public static IRuleBuilderOptions<T, string> ValidText<T>(this IRuleBuilder<T, string> rule, int maxLength) =>
        rule.NotEmpty().MaximumLength(maxLength);
}

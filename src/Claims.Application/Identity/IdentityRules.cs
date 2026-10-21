using FluentValidation;

namespace Claims.Application.Identity;

/// <summary>Shared validation rules for account fields (register and create-user).</summary>
public static class IdentityRules
{
    public const int PasswordMinLength = 10;

    public static IRuleBuilderOptions<T, string> ValidEmail<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(256).EmailAddress();

    // Length only, no composition rules (CLAUDE.md). Common passwords are rejected by Identity.
    public static IRuleBuilderOptions<T, string> ValidPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MinimumLength(PasswordMinLength).MaximumLength(128);

    public static IRuleBuilderOptions<T, string> ValidDisplayName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(100);
}

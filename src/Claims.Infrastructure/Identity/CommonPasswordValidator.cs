using Microsoft.AspNetCore.Identity;

namespace Claims.Infrastructure.Identity;

/// <summary>
/// Rejects a short list of the most common passwords that pass the length rule.
/// Cheap stand-in for a breached-password check (README: use a breach corpus with more time).
/// </summary>
internal sealed class CommonPasswordValidator : IPasswordValidator<ApplicationUser>
{
    private static readonly HashSet<string> Common = new(StringComparer.OrdinalIgnoreCase)
    {
        "1234567890", "12345678910", "0123456789", "0987654321", "1111111111",
        "password123", "password1234", "password12", "passw0rd123", "qwertyuiop",
        "qwerty1234", "qwerty12345", "1q2w3e4r5t", "abcdefghij", "abc1234567",
        "iloveyou123", "letmein123", "welcome123", "admin12345", "changeme123",
    };

    public Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user, string? password) =>
        Task.FromResult(password is not null && Common.Contains(password)
            ? IdentityResult.Failed(new IdentityError { Code = "PasswordTooCommon", Description = "This password is too common." })
            : IdentityResult.Success);
}

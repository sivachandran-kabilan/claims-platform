using Claims.Application.Common.Paging;

namespace Claims.Application.Identity;

public sealed record UserResponse(Guid Id, string Email, string DisplayName, string Role);

public sealed record NewUser(string Email, string Password, string DisplayName, string Role);

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAtUtc);

/// <summary>User store port, implemented with ASP.NET Core Identity in Infrastructure.</summary>
public interface IIdentityService
{
    /// <summary>Creates the user with exactly one role. Duplicate email → ConflictException; rejected password → ValidationException.</summary>
    Task<UserResponse> CreateUserAsync(NewUser user, CancellationToken cancellationToken);

    /// <summary>Returns the user when the password is correct and the account is not locked out; otherwise null.</summary>
    Task<UserResponse?> CheckCredentialsAsync(string email, string password, CancellationToken cancellationToken);

    Task<UserResponse?> GetUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<PagedResult<UserResponse>> ListUsersAsync(int page, int pageSize, string? role, CancellationToken cancellationToken);
}

/// <summary>Issues access tokens. Implemented in the API next to token validation.</summary>
public interface ITokenGenerator
{
    AccessToken Create(UserResponse user);
}

public static class EmailAddress
{
    public static string Normalize(string email) => email.Trim().ToLowerInvariant();
}

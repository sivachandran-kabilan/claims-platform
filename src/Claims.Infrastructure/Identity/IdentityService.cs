using Claims.Application.Common.Exceptions;
using Claims.Application.Common.Paging;
using Claims.Application.Identity;
using Claims.Infrastructure.Persistence;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Claims.Infrastructure.Identity;

internal sealed class IdentityService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ClaimsDbContext db) : IIdentityService
{
    // Hash checked for unknown emails so both failure paths do the same expensive work (timing).
    private static string? s_dummyHash;

    public async Task<UserResponse> CreateUserAsync(NewUser newUser, CancellationToken cancellationToken)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = newUser.Email,
            Email = newUser.Email,
            DisplayName = newUser.DisplayName,
        };

        // User row and role row commit together; no user can exist without a role.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var created = await userManager.CreateAsync(user, newUser.Password);
        if (!created.Succeeded)
        {
            throw ToException(created);
        }

        var roleAdded = await userManager.AddToRoleAsync(user, newUser.Role);
        if (!roleAdded.Succeeded)
        {
            throw new InvalidOperationException($"Could not assign role {newUser.Role}.");
        }

        await transaction.CommitAsync(cancellationToken);
        return new UserResponse(user.Id, newUser.Email, newUser.DisplayName, newUser.Role);
    }

    public async Task<UserResponse?> CheckCredentialsAsync(string email, string password, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            s_dummyHash ??= userManager.PasswordHasher.HashPassword(new ApplicationUser(), Guid.NewGuid().ToString());
            userManager.PasswordHasher.VerifyHashedPassword(new ApplicationUser(), s_dummyHash, password);
            return null;
        }

        // Counts failures and enforces lockout (5 attempts, IdentityOptions).
        var result = await signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            return null;
        }

        return await GetUserAsync(user.Id, cancellationToken);
    }

    public Task<UserResponse?> GetUserAsync(Guid userId, CancellationToken cancellationToken) =>
        UsersWithRole()
            .Where(x => x.User.Id == userId)
            .Select(x => new UserResponse(x.User.Id, x.User.Email!, x.User.DisplayName, x.Role))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<PagedResult<UserResponse>> ListUsersAsync(
        int page, int pageSize, string? role, CancellationToken cancellationToken)
    {
        var query = UsersWithRole();
        if (role is not null)
        {
            query = query.Where(x => x.Role == role);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.User.Email)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new UserResponse(x.User.Id, x.User.Email!, x.User.DisplayName, x.Role))
            .ToListAsync(cancellationToken);

        return new PagedResult<UserResponse>(items, page, pageSize, total);
    }

    // Each user has exactly one role. Filter and sort on these columns, then project last
    // (EF cannot translate filters applied after projecting into a record constructor).
    private IQueryable<UserWithRole> UsersWithRole() =>
        from user in db.Users.AsNoTracking()
        join userRole in db.UserRoles on user.Id equals userRole.UserId
        join r in db.Roles on userRole.RoleId equals r.Id
        select new UserWithRole { User = user, Role = r.Name! };

    private sealed class UserWithRole
    {
        public required ApplicationUser User { get; init; }
        public required string Role { get; init; }
    }

    private static Exception ToException(IdentityResult result)
    {
        if (result.Errors.Any(e => e.Code is "DuplicateEmail" or "DuplicateUserName"))
        {
            return new ConflictException("An account with this email already exists.");
        }

        var failures = result.Errors.Select(e => new ValidationFailure(FieldFor(e.Code), e.Description));
        return new ValidationException(failures);
    }

    private static string FieldFor(string code) => code switch
    {
        _ when code.StartsWith("Password", StringComparison.Ordinal) => nameof(NewUser.Password),
        "InvalidEmail" or "InvalidUserName" => nameof(NewUser.Email),
        _ => string.Empty,
    };
}
